# 99-Strength v2 — "Whole Plan" Refactor

You are working on **github.com/JoeR96/99-strength**, my A2S2 (Average to Savage 2) tracker. Read `AGENTS.md` (root) and `src/A2S.Web/AGENTS.md` before touching anything, and follow their rules: Clean Architecture + CQRS (MediatR), thin handlers, zero-dependency Domain, `Result<T>`, factory methods only, internal mutation via aggregate roots. Backend is .NET 10 / EF Core / Postgres, frontend React 19 + Vite + Tailwind + ShadCN + TanStack Query, auth via Clerk, Hevy ACL in `A2S.Integration.Hevy`. There are ~930 existing tests — they must stay green.

## Goal

Turn the app from "A2S2 progression calculator" into the **one place I run my whole training plan from, on my phone**: lifting (A2S2, synced to Hevy), cardio, mobility/correctives, daily habits with tap-to-increment counters, weekly goals, and weight-loss checkpoints. Ship a **ready-made suggested plan** (below) I can start with one tap.

Out of scope: protein/calorie tracking, step counting, and anything involving MacroFactor (it has no usable API — if a feature would need it, drop that feature).

### Two of my other projects you must research and fold in yourself

- **Terran CC widget** — my existing habit widget. It already tracks habits, including my **target number of walks**. Find it yourself (my GitHub `JoeR96`, any memory/notes you have access to, local repos) and work out how it stores data. Then either sync 99-strength's Habits with it, or migrate its habits into 99-strength so it can be retired — recommend one in the Phase 1 plan with the trade-offs. Take the walks target and any other habit targets from it rather than inventing them.
- **Cook's assistant** — my separate recipe app, with its own plan. Find the repo under `JoeR96`, pull it, read it, and plan merging the part that matters here into 99-strength: a **Meals** section limited to meals I'll eat on this cut (no macro tracking), plus a **shopping list** built from the selected meals that I can export (share sheet / copy / CSV). Keep it as its own phase after the core plan ships, and don't break the cook's assistant app itself.

---

## 0. Secrets — Hevy API key

My Hevy API key is in Bitwarden. Session token:

```
BW_SESSION=<<PASTE BITWARDEN SESSION HERE>>
```

- Find the item with `bw list items --search "hevy" --session "$BW_SESSION"` (it's named something like "hevy api key"), then read the value with `bw get`. If more than one item matches, stop and ask me which.
- **Never** write the key or the session into any committed file, test fixture, log line, commit message, PR description or chat output. Load it locally via `dotnet user-secrets` / an untracked `.env.local`, and use it only for live smoke tests against Hevy.
- `User.HevyApiKey` is currently stored in plaintext. Encrypt it at rest (ASP.NET Core Data Protection or an EF value converter), add a migration that encrypts existing rows, and make sure it's never returned by any API response or logged by Serilog.
- Hevy's API needs a Hevy Pro account and has rate limits — handle 401/403/429 with clear UI messages and Polly backoff.

---

## 1. Audit first (deliverable: `docs/v2/audit.md`)

Before changing code, write up:

1. **Current feature map** — every page, command, query, and Hevy flow, with what works, what's half-built, and what's dead.
2. **UX walkthrough as a real user** — narrate these journeys step by step on a 390px-wide phone viewport (use Playwright screenshots, save to `docs/v2/screens/`): first sign-in → starting a programme → doing today's session → completing a day → week rollover → deload → missing a week → substituting an exercise → syncing to Hevy. List every friction point: taps needed, dead ends, unclear states, jargon, tiny tap targets, things that need a desktop.
3. **Gap list** against this brief, and anything you think I've missed.

Then propose the plan in `docs/v2/plan.md` and **stop for my approval** before Phase 2.

---

## 2. Domain expansion

Add new aggregates and leave the existing `Workout` aggregate's A2S2 logic alone (it already handles the 21-week, 3-block, deload-every-7th-week structure, block sequencing, undo and audit).

- **TrainingPlan** (aggregate root): owns a weekly schedule template, start date, phases, and links to one active A2S2 `Workout`.
  - `ScheduledSession` slots per weekday: `Lift(DayNumber)`, `Cardio(CardioProtocolId)`, `Mobility(RoutineId)`, `Rest`.
  - **Phases** with date ranges and type: `Cut`, `Maintenance` (e.g. Christmas), `Pause` (holiday/away — the A2S2 week doesn't advance and missed sessions aren't counted as failures), `Deload`.
  - **Checkpoints**: target date + target trend weight + note.
- **CardioProtocol**: Norwegian 4×4 (4 × 4 min at 85–95% HRmax / 3 min active recovery, warm-up and cool-down), Couch to 5K (the full 9-week progression, with "repeat this week" support), LISS (incline walk/bike, duration target). Log duration, optional avg HR, RPE, distance, notes.
- **MobilityRoutine**: ordered list of drills, each with sets × reps/seconds/breaths, side bias (e.g. "extra set left"), cues, and a **standing alternative** (I sometimes can't get on the floor at the gym). Completion is a single tap for the whole routine, with optional per-drill ticks.
- **Habits** (`HabitDefinition` + `HabitLog` per local date):
  - Types: `Counter` (target N, tap `+1` / `−1` — e.g. water bottles 0/6, walks 0/N), `Numeric` (enter a value, e.g. morning weight), `Checkbox` (done/not done).
  - A **minimum version** per habit for bad days (e.g. water min 4, walks min 1) so a 30% day still counts and the streak survives.
  - Streaks with a **"never miss twice"** rule: one missed day doesn't break the streak, two in a row does.
- **WeeklyGoals**: derived from the plan (4 lifts, 2–3 cardio, mobility N times, 1 planned flexible meal, ≤1 alcohol occasion) and shown as progress rings for the current Mon–Sun week.
- **BodyWeight entries** (manual morning weigh-in) with a 7-day moving average / exponentially smoothed trend, plotted against checkpoints.
- **Training volume budget** — I tend to over-train some muscle groups. Tag every exercise in the library with primary/secondary muscle groups, compute **hard sets per muscle group per week** across lifting + accessories, and warn (don't block) when a group goes over its cap or a session goes over its time budget. Show this when I add, swap or reorder exercises.
- **Session time estimate** per lifting day (sets × (rest + ~40s work) + warm-up). Target ≤ 55 min including warm-up.

All dates are **local dates in Europe/London**. Day boundaries, streaks and week rollover must be correct across BST/GMT changes.

---

## 3. Hevy integration

Hevy is the logging source of truth during workouts. Extend the existing ACL:

- Push each lifting day as a Hevy routine (existing flow) and include the **warm-up correctives as the first exercises** of the routine, so they're done in Hevy without opening this app.
- Push cardio days as Hevy routines using Hevy's duration/distance exercise types where possible.
- **Pull** completed Hevy workouts (poll on app open + manual refresh; use webhooks if Hevy supports them for my plan) and auto-match them to the scheduled session for that date: A2S2 sets/AMRAP results flow into `CompleteDay` exactly as manual entry would, and cardio/mobility mark their sessions done.
- Idempotent: re-syncing never duplicates routines or completions. Store Hevy IDs and use them as dedupe keys.
- If a Hevy workout doesn't match a planned session (e.g. I trained on a rest day, or did Upper on a Lower day), show it in an "unmatched" inbox where I can assign it in one tap.
- Graceful offline/failed-sync states in the UI.

---

## 4. Mobile PWA

- `vite-plugin-pwa`: manifest, icons (192/512 + maskable + apple-touch-icon), standalone display, theme colour, offline app shell.
- **Offline-first habits and session completion**: taps write to IndexedDB immediately and sync when online, with a visible pending-sync badge. Define the conflict rule (last-write-wins per habit/day is fine) and test it.
- Check Clerk auth works in iOS standalone mode — redirects and session persistence are the usual failure points.
- Phone-first layout: bottom tab bar (**Today / Plan / Habits / Progress / Settings**), 44px+ tap targets, one-handed use, no horizontal scroll, dark mode.
- Notifications: web push for "time to train" and habit nudges where supported. On iOS this only works for an installed PWA on 16.4+, so detect and degrade gracefully.
- Document the install steps for iOS and Android in the README.

**"Today" screen is the heart of the app.** It must answer "what do I do now?" in one glance: today's session (Lift/Cardio/Mobility/Rest) with a big start button (deep link to Hevy or open the routine), the habit counters as large `+` buttons (water 3/6 → tap → 4/6), today's weigh-in field, the weekly goal rings, and the streak. Target: logging a water bottle is **one tap from app open**.

---

## 5. Suggested plan — seed it, offer "Start this plan" on first run

Seed as a template called **"Cut to 85 — Nov 2026"**, editable after starting. Start date: the coming Monday. Short and effective: main lift via A2S2 + 3 accessories, supersets where possible, ≤ 55 min.

### Weekly schedule

| Day | Session |
|---|---|
| Mon | Upper A |
| Tue | Lower A |
| Wed | Cardio — Norwegian 4×4 (outdoors, hills) + Mobility B (25 min) |
| Thu | Upper B |
| Fri | Lower B |
| Sat | Cardio — C25K run or LISS 30–40 min |
| Sun | Rest — walk only |

Rule: no 4×4 the day before Lower A or B.

### Lifting (A2S2 progression on the main lift; accessories use the existing reps-per-set / linear strategies)

Every lifting day starts with **Mobility A** as the warm-up (counted automatically when the day is completed).

**Upper A** — Bench press (A2S2) · Chest-supported row 3×8–10 · Half-kneeling single-arm landmine press 3×8/side, start left · Superset: single-arm cable row 3×10/side (extra set left) + face pulls 3×15

**Lower A** — Squat (A2S2, pause the last rep of each set) · RDL 3×8 · Rear-foot-elevated split squat 3×8/side, start left, extra set left · Superset: side plank 3×30s/side (extra left) + Pallof press 3×10/side

**Upper B** — Overhead press (A2S2) · Lat pulldown 3×8–10 · Incline DB press 3×10 · Superset: seal/meadows row 3×10 + wall slides with lift-off 2×10

**Lower B** — Deadlift (A2S2) · Leg press 3×10 · Single-leg RDL 3×8/side · Superset: Copenhagen plank (short lever) 3×20s/side + dead bug 3×8/side

Default weekly hard-set caps (editable): chest 10, back 14, shoulders 10, quads 10, hamstrings 10, glutes 12, core 12. Flag any day estimated over 55 min.

### Cardio

- **Norwegian 4×4**: 10 min warm-up, 4 × 4 min at 85–95% HRmax with 3 min active recovery between, 5 min cool-down (~38 min).
- **C25K**: the standard 9 weeks with "repeat week" allowed. Note: on grass/track/treadmill where possible.
- **LISS fallback** for any cardio slot: incline walk 30–40 min at conversational pace.

### Mobility (left-side compensation pattern, upper/lower cross)

**Mobility A — daily 10-min warm-up**
1. 90/90 breathing — 2 × 5 breaths *(standing: back-to-wall breathing, pelvis tucked, ribs down)*
2. Quadruped hip shift — 2 × 5 breaths *(standing: wall-supported hip shift)*
3. Open books — 2 × 5/side, extra set opening the left *(standing: side-on wall rotation in split stance, hips square)*
4. Band walks + glute bridges — 2 × 10 each
5. Wall slides with lift-off — 2 × 10

**Mobility B — 25-min standalone (Wed + any rest day)**
Mobility A, then: bird dog 3 × 5/side (extra right-arm/left-leg) · side plank extra set on the left · push-up plus or cable serratus punch 3 × 12 · slow calf raises left emphasis 3 × 10 · reverse plank regression progression (bench-supported → floor, 3 × 15–30s) · calf + pec stretches to finish.

### Habits (daily)

| Habit | Type | Target | Minimum |
|---|---|---|---|
| Water (532ml bottle) | Counter | 6 | 4 |
| Walks | Counter | from Terran CC | 1 |
| Morning weigh-in | Numeric (kg) | logged | logged |
| Mobility A done | Checkbox | ✓ | ✓ |
| Creatine (3–5g) | Checkbox | ✓ | ✓ |

Plus any other habits already in Terran CC, carried over with their existing targets.

### Checkpoints (trend weight)

| Date | Target | Note |
|---|---|---|
| 7 Dec 2026 | 92 kg | 4 cutting weeks |
| 21 Dec 2026 | 91 kg | Cut pauses after this |
| 22 Dec – 3 Jan | Maintenance phase | Hold, don't gain. A2S2 continues if training; habits still tracked |
| 1 Feb 2027 | 88 kg | |
| 8 Mar 2027 | 85 kg | Goal |

Starting weight 95 kg. Show "on pace / behind / ahead" against a straight line between checkpoints.

---

## 6. Testing — every cycle and edge case

Backend (xUnit, Testcontainers) and frontend (Vitest), plus **Playwright e2e on a mobile viewport** for the main journeys. At minimum:

**A2S2 cycle**
- Full 21-week run (all three blocks) with simulated AMRAP results above, at and below target; TM changes match `research/A2S2_Validation_Data.md` and the spreadsheet in `research/`.
- Deload weeks 7/14/21, custom block sequences, programme restart after week 21.
- Undo after week rollover, undo of the first-ever completion, double-tap complete (idempotency).
- Substitution mid-block keeps progression history correct.

**Schedule & calendar**
- Missing one session, a whole week, and two weeks; completing sessions out of order; two sessions on one day; training on a rest day.
- Pause and Maintenance phases: the A2S2 week doesn't advance during Pause, resumes cleanly, and checkpoints shift or stay as configured.
- BST→GMT (last Sunday of October) and GMT→BST transitions, a session completed at 23:59 vs 00:01, and New Year week rollover.

**Habits**
- Counter can't go below 0; going over target is allowed and shown; minimum-version logic; the "never miss twice" streak rule across week and month boundaries.
- Offline taps queued then synced; conflicting edits from two devices.

**Hevy**
- Contract tests against recorded fixtures (no live key in CI) for routine push, workout pull, matching, dedupe, unmatched inbox, 401/403/429/5xx and timeouts.
- One opt-in live smoke test using the key from section 0, skipped unless the secret is present locally.

**Volume budget**: cap warnings fire correctly when adding, swapping or reordering exercises; time estimates are within ±10% of a hand calculation.

**PWA**: Lighthouse PWA/installability checks pass; app shell loads offline; habits page works offline.

---

## 7. Working agreement

- Small, reviewable PRs per phase on feature branches. Never push to `main` directly.
- EF migrations must be reversible and safe on existing data (my live A2S2 workout history must survive untouched — back it up before the first migration and test the migration against a copy).
- Update `AGENTS.md`, the README and Storybook stories as you go.
- Stop and ask me before: deleting any existing feature, changing A2S2 progression maths, or anything that touches my production database.

**Order:** Phase 1 audit + plan (including the Terran CC and cook's assistant research) → (my approval) → secrets/encryption → domain + migrations → Today/Habits PWA UI → Terran CC sync or migration → Hevy push/pull → suggested plan seed + onboarding → test hardening → final UX re-walkthrough with screenshots compared against the Phase 1 audit → Meals + shopping list from cook's assistant (separate PRs).
