# 99 Strength v3: "Coach in my pocket"

You are working on **github.com/JoeR96/99-strength**. v2 (the "Whole Plan" refactor, `docs/archive/v2/brief.md`) is
built: training plan, A2S2 via Hevy, cardio protocols, mobility routines, habits, push nudges, offline
habit queue, meals. v3 adds the missing functionality and an accessibility audit. It turns the app from a tracker into something that **plans, progresses and reminds me
of everything**, the way a PT would, across lifting, cardio and mobility.

Read `AGENTS.md`, `src/A2S.Web/AGENTS.md` and `docs/archive/v2/audit-after.md` (the live walkthrough and its
open defects) before touching anything. Follow the existing rules: Clean Architecture with CQRS, thin handlers, a
Domain project with no dependencies, `Result<T>`, factory methods, mutation only through aggregate roots, local dates in Europe/London.
All existing .NET and Vitest tests must stay green.

---

## 0. Decisions already made, so don't reopen them

| Topic | Decision | Why |
|---|---|---|
| Native app | **Not now.** Stay a PWA; web push already works for the installed iOS app (16.4+). | Expo/native is parked. |
| Hosting | **No change.** Frontend stays on Vercel, API on Dokploy. | Out of scope for v3. |
| Hevy | **99 Strength is the gym app; Hevy is the record.** I open 99 Strength at the gym, work through today's checklist, and it sends the finished workout to Hevy. | One app to open. Logging directly in Hevy keeps working as a fallback (the existing pull/match). See § 3a. |
| Progression maths | **A2S2, RPS and MinimalSets maths don't change.** | Any change needs my approval first (§ 8). |
| Specific routines | **Build generic engines, not my routines.** | I'll author my own mobility, stretch and yoga routines and cardio tracks in the app. Seed only what's needed for tests and Storybook. The existing "Cut to 85" seed stays as editable data. |

---

## 1. Phase 1: audit (deliverables first, then stop for my approval)

### 1a. Repo and docs cleanup → `docs/v3/cleanup.md` and a PR

The README has already been rewritten as user-facing, and dev content moved to `docs/development.md`.
Keep both accurate as you build. Then:

- `src/A2S.Web/README.md` is Vite template boilerplate. Replace it with a three-line pointer to `src/A2S.Web/AGENTS.md`.
- Done: `docs/superpowers/` and `docs/v2/` are in `docs/archive/`; the Clerk iOS notes are in `docs/development.md`.
- `research/`: done. Validation data and spreadsheet moved to `docs/reference/a2s2/`; the rest archived.
- Done: screenshots removed from the tree; generators now write to the gitignored `artifacts/`.
- Done: `cooks-assistant-recipes.ts` was not used by any code; it is archived with the rest of v2.
- Root `package.json` says `a2s-workout-tracker`, 0.9.0, ISC, no author. Rename it and fix the description. Drop
  scripts nothing uses (check `setup:*` and `e2e*` against CI and the docs).
- Remove stale theme vocabulary: "Golden Twilight", `gold`/`navy` classes and `rgba(255,212,10…)` in
  `LoginPage.tsx` and `lib/chartTheme.ts`. The theme is the orange "Arcade Minimal".
- Check `AGENTS.md` against the code. Its command and query tables are missing most v2 commands (Habits,
  Cardio, Mobility, Plans, Push, Meals, Hevy pull). List anything that's wrong and fix it.

### 1b. Accessibility and UI audit → `docs/v3/a11y-audit.md`

The last full axe pass (July 2026) predates v2. No v2 screen behind sign-in has been checked live, because
the walkthrough couldn't create a test user (defect 2).

**Unblock signed-in testing first.** Use Clerk Testing Tokens (`@clerk/testing` with Playwright) or turn off bot
protection on the dev instance. Then write the signed-in walkthrough scripts that `audit-after.md` says don't exist yet.

**Standard:** WCAG 2.2 AA. Pay particular attention to the 2.2 additions this app is exposed to:
- 2.4.11 *Focus not obscured*: the fixed bottom tab bar and sticky header must not hide a focused input,
  a sheet's buttons or a toast.
- 2.5.7 *Dragging movements*: drill, exercise and schedule reordering needs a non-drag alternative (up and down buttons).
- 2.5.8 *Target size*: the house rule is 44 px, stricter than the 24 px minimum.
- 3.3.8 *Accessible authentication*: paste and password managers must work on sign-in.

**Matrix:** every route, sheet and modal × 320, 390 and 430 px portrait, 844 px landscape, and 1440 px.
Automated axe on all of it. Manual checks: VoiceOver on a real iPhone in the **installed** app, keyboard only on desktop,
iOS Larger Text and 200 % zoom, Reduce Motion, and the **on-screen keyboard open on every form**.

**Known suspects to confirm or clear.** I've seen UI clash on phone, especially on login:
- `LoginPage`/`SignUpPage` use `min-h-screen` with `overflow-hidden` and a vertically centred card. On iOS
  that's 100vh, which ignores the keyboard and browser chrome, so the card and Continue button can end up under the
  keyboard or clipped. Use `min-h-dvh` and top-align on small heights.
- There's no `env(safe-area-inset-top)` on sign-in and sign-up (the app shell has it), so in standalone mode
  the content can sit under the status bar.
- 800 px decorative blur blobs are absolutely positioned around the card. Check overflow and paint cost.
- The production login shows Clerk's **"Development mode"** footer, which means production runs on a Clerk
  development instance (§ 2).
- Clerk's own logo link is 48 × 14 px, and `/setup` has an unfocusable scrollable region (from the July audit).
- Bottom sheets with the keyboard open (cardio log, weigh-in, habit edit): no content may end up behind the keyboard or the tab bar.
- Charts (weight trend, exercise history, rings) need a text or table alternative.

Each finding gets: the route or story, a screenshot, the WCAG criterion, a severity, and a proposed fix. Then fix
them in PRs grouped by component. **Make it a CI gate:** the Storybook a11y run fails on serious or critical issues,
and the signed-in axe run executes in CI against Testcontainers with a test user.

### 1c. Open v2 defects

Pull the open items from `audit-after.md` into the plan: 2 (test user), 3 (blank screen offline, high),
10 (Data Protection key ring stored unencrypted next to the data it protects), 11 (MediatR licence), 12 (`/health` doesn't
touch the DB), 14 (log noise), 16 (a single 1.36 MB JS chunk with no source map).

Then write `docs/v3/plan.md` with phases, PRs and test lists, and **stop for my approval**.

---

## 2. Platform fixes (no hosting changes)

Hosting stays as it is. Only fix what blocks v3 or the audit:

- **Offline blank screen (defect 3):** render the cached shell and last-known data when `clerk.browser.js` can't load. The gate
  should only block network calls, not the whole UI.
- **Test user (defect 2):** Clerk Testing Tokens or bot protection off on the dev instance (needed for § 1b).
- Production shows Clerk's "Development mode" footer: list what moving to a Clerk production instance involves. I'll decide.
- Defects 12 (health check touches the DB) and 16 (route-level code splitting, source maps). 10, 11 and 14 go on a backlog list.

---

## 3a. Gym mode: the app wraps Hevy (top priority after Phase 1)

At the gym I only open 99 Strength. Today's session is a checklist; when I finish, the app writes the
completed workout to Hevy.

**What exists:** in-app set logging for lifts (`WorkoutSession`, `/workout/session/:day`) and a v1
browser-side "Send to Hevy" on `CompletionSummary` (`createCompletedWorkoutInHevy` via the proxy).
`HevyIntegrationService` can already create workouts server-side. Reuse these rather than rebuilding.

**Build:**

- **One checklist for the whole session, in order:** warm-up drills (tick each, or "all done"), then
  each lift with its planned sets pre-filled (weight, target reps; tap a set to confirm, edit reps or
  weight if different; the AMRAP set asks for reps), then any attached finisher (prescription shown,
  log duration, level, floors, effort), then "Finish".
- **Gym-proof UI:** big targets, one hand, works with no signal (sets saved on the phone first, through
  the offline queue), screen stays awake (Wake Lock), and a rest timer between sets that starts
  automatically from the plan's rest seconds, with a sound when it ends.
- **Finish = two things at once:** complete the session in the app (same `CompleteDay` path as now, so
  A2S2 progression and undo are unchanged; cardio/mobility through `CompleteSession`) **and** post the
  workout to Hevy **from the server** (`POST /v1/workouts`, key never in the browser) with real start
  and end times, sets typed correctly (warm-up / normal / failure for AMRAP), and the finisher as Stair
  Machine (Floors).
- **No double counting:** store the returned Hevy workout id on the completion (a `HevyLink`), so when
  the 15-minute pull sees that workout it recognises it as already done. Pinned by a test: post, pull,
  exactly one completion and one progression.
- **Retry safely:** if the Hevy post fails or I'm offline, the session is still complete in the app and
  the post is queued and retried; the Today card shows "Not in Hevy yet" until it succeeds. Retrying
  must never create a second Hevy workout (look up by start time and title before re-posting).
- **Partial sessions:** "Finish early" posts what was done; skipped exercises keep their progression
  untouched, as today.
- Remove the v1 browser-side send once the server path ships.
- Walks, runs and home routines use the same pattern: start from Today, log or play, finish, and it is
  posted to Hevy (Walking / Treadmill / Running / Stretching / Yoga).
- Pushing routines *into* Hevy becomes optional (a setting), since I no longer start workouts there.

## 3. Progression schemes: keep everything working

Existing strategies that must behave exactly as now: **A2S2 Linear/RTF** (AMRAP delta table, blocks, deloads,
custom block sequences, restart), **Reps Per Set**, **Minimal Sets**, **Couch to 5K weeks** (repeat-week).

- Before v3 code lands, add **golden tests**: a full 21-week A2S2 simulation checked against `A2S2_Validation_Data.md`,
  and an RPS and a MinimalSets scenario covering success, maintained and failed. Snapshot the outputs and fail on any diff.
- **Prove parity between the Hevy path and the manual path:** the same workout entered by hand and pulled from a Hevy fixture
  must give identical TM changes, set changes and audit entries.
- Undo still restores the exact previous state for every strategy, including the new cardio ones.

---

## 4. Cardio progression (the new engine)

I wear a WHOOP and don't want to log much, but each cardio track must **progress automatically**, the
way A2S2 does for lifts. Treat cardio like lifting: a *track* holds progression state, and each session it
prescribes is evaluated afterwards.

### 4a. Domain

- `CardioTrack` (aggregate root): name, modality (`Walk`, `Run`, `StairClimber`, `Bike`, `Row`, `Other`),
  progression strategy, current state, status (active/paused/completed), history, and an audit trail like `ProgressionAuditEntry`.
  Each scheduled cardio slot points at a track (or, as today, a fixed `CardioProtocol`).
- `CardioPrescription` (value object): what to do today, e.g. "7 min continuous @ level 9" or "C25K wk 4".
  It's derived from the track's state and is what Today shows and what gets pushed to Hevy.
- `CardioLog` (exists): add `IntensitySetting` (stair level, treadmill incline %, speed), `Floors`/`Steps`,
  `MaxHeartRate`, `Zone5Minutes` alongside the existing zone 4–5 minutes, a `PainFlag` (none/niggle/stop, with a body area), and `Source` (manual/Hevy/WHOOP).
- **Effort input:** three big buttons, **Easy / Solid / Brutal**, stored as RPE 4 / 7 / 9. Exact RPE is optional.
- `UserProfile.MaxHeartRate` (entered manually, or read from WHOOP later) and zone boundaries matching WHOOP's
  (% of max HR: Z1 50–60, Z2 60–70, Z3 70–80, Z4 80–90, Z5 90–100). Let me edit them.

### 4b. Strategies (each is a domain class with unit tests, like the lifting strategies)

1. **`WeekProgramStrategy`**: a fixed list of weeks, each a session template. This generalises today's
   C25K. **Gate:** move to the next week only when every planned session that week was completed, none was
   *Brutal*, and there's no pain flag. Otherwise suggest a repeat; I confirm or override. Two `niggle` flags in
   one week, or any `stop`, suggests dropping back a week.
2. **`DoubleProgressionStrategy`**: progress a *volume variable* (minutes, or interval rounds) through a range,
   then step an *intensity variable* (level, incline, speed) and reset the volume to the bottom of the range.
   **Gate:** advance one step after N qualifying sessions (default 2). A session qualifies when it's completed as
   prescribed, isn't *Brutal* for easy tracks (or meets the zone target for hard ones), and has no pain flag. A *Brutal* rating on an easy track, or a missed
   prescription, holds; two misses in a row step back once.
3. **`StageStrategy`**: an ordered list of stages, each with its own strategy and an exit condition, e.g.
   "continuous 5→10 min, then intervals". Moving to the next stage is a suggestion I accept.
4. **Benchmarks:** any track can schedule a test every N weeks (default 4), like "floors in 5 min, all out" or
   "5 K time" or "continuous run minutes". Results go to a PR history and a chart, the same way AMRAPs do for lifts.

### 4c. Templates to ship (as templates I can start and edit, not hard-coded routines)

| Track | Strategy | Defaults (all editable) | Progress metric |
|---|---|---|---|
| **Walk builder** (easy, zone 2) | Stage: ① duration 20→45 min, +5 min a step; ② hold 45 min and add incline +1 % a step (or pace) up to 10 % | Qualifies if *Easy* (I can talk in full sentences), and avg HR ≤ the Z2 ceiling when HR is logged | minutes, incline, avg HR at the same workload going down over time |
| **Walk → run** | Week program: existing C25K 9 weeks, then a double-progression "5 K builder" (continuous minutes +10 % a step up to 35 min) | Gate as above. Note "treadmill or grass preferred" | weeks done, continuous minutes, 5 K benchmark |
| **Stair finisher** (after lifting, zone 4–5) | Stage: ① continuous 5→10 min at one level, +1 min a step, then level +1 and back to 5 min; ② intervals 1 min hard / 1 min easy, 4→8 rounds, then hard level +1 | Qualifies if completed, logged Z4–5 minutes ≥ 50 % of work time (when HR is present), and no pain | level × minutes, floors in a fixed time, Z5 minutes |
| Norwegian 4×4 (exists) | fixed protocol, no progression | — | Z4–5 minutes, avg HR |

Walking and running are **separate tracks**: I want to build a walking base and a running progression side by side.
A completed walk session also ticks the **Walks** habit counter, with no double entry.

### 4d. Guardrails (warnings, never blocks)

- Weekly zone 4–5 minutes against `HighIntensityTarget` (exists). The stair finisher and 4×4 both count. Warn above the max.
- No hard cardio (4×4, stair intervals) the day before Lower A/B. Generalise `PlanScheduleRules`.
- Finishers default to **upper-body days**. Warn when one is attached to a lower day.
- On a **red** readiness day (§ 5d), Today suggests swapping hard cardio for the easy version or skipping it.
- Short safety text in the track info sheet: build up gradually, and stop for chest pain, dizziness or unusual breathlessness.
  Keep it to one line, not a wall of disclaimers.

### 4e. Hevy and data sources

- Push: the stair finisher is appended as the **last exercise** of the lift's Hevy routine using `Stair Machine (Floors)`
  (`floors_duration`), with the prescribed duration and level in the notes. Walks and runs use `Walking`/`Treadmill`/`Running`
  (`distance_duration`). Check template ids against `scripts/hevy-exercises.json` and the `HevyTemplateCatalogue`.
- Pull: when a matched Hevy workout contains those exercises, create the `CardioLog` (duration, floors, distance, and RPE if the set has
  one) and run the track's evaluation, idempotently, keyed on the Hevy workout id plus exercise index, exactly as lifting works.
- **WHOOP (optional phase, after the core):** WHOOP's developer API (v2, OAuth2, self-serve app) exposes workouts with
  per-zone durations, strain and avg/max HR, plus recovery scores and webhooks. Build `A2S.Integration.Whoop` as an ACL like
  Hevy's: OAuth connect in Settings with tokens encrypted at rest, pull workouts via webhook or polling, match them to the day's
  cardio log by time overlap, and fill in HR and zone minutes. Until then, HR and zone minutes are optional manual fields.

---

## 5. Mobility, stretching and yoga (generic)

I do correctives at the gym and stretch/yoga routines at home (lately follow-along flows like Man Flow Yoga).
Build the tools for me to **create these myself in the app.**

### 5a. Routine builder

- Extend `MobilityRoutine` with a **kind**: `WarmUp`, `Corrective`, `Stretch`, `YogaFlow`, `FollowAlong`.
- **Drill/pose library** (per user, reusable across routines): name, kind of dose (reps, seconds, breaths), cues, optional image or
  video URL, standing alternative, whether it's per side, and the side to add an extra set to (all exist on `MobilityDrill`. Lift them into a library and have
  routines reference library items while allowing overrides per routine).
- Routine editor on phone: add from the library, set the sets and dose, reorder with **up/down buttons** (WCAG 2.5.7) and optional drag,
  duplicate, and an estimated total time.
- `FollowAlong` routines are just a title, an external link (a YouTube video or another app's session) and an expected duration.
  Starting one opens the link and logs the time when I come back. **Don't copy any commercial programme's sequences or content
  into the app or seed data.** I'll enter my own.

### 5b. Player (home use, offline)

- A full-screen step-through player: the current pose in large type, a countdown for timed holds and breaths (configurable seconds per breath),
  automatic side switching for per-side drills, transition and rest gaps, pause/skip/back, and a "next up" preview.
- Audio cues through Web Audio (start and switch beeps), with optional spoken cues via `speechSynthesis`. **iOS has no Vibration API**,
  so don't depend on haptics. Use Screen Wake Lock (supported in iOS home-screen apps since 18.4) and degrade gracefully.
- Works fully offline once loaded. Completion goes through the existing offline queue.
- The gym version stays as is: warm-up drills pushed as the first exercises of the Hevy routine.

### 5c. Mobility progression (light)

- Optional per-drill progression with `DoubleProgressionStrategy` reused: hold time +5 s or reps +1 after N comfortable sessions, up to a cap.
- **Progression chains:** a drill can have ordered regressions and progressions (e.g. bench-supported → floor). Moving up the chain
  is a suggestion when the top of the current level's range has been hit.
- **Assessments:** I define simple tests (a hold time, a reach or ROM measure, a side-to-side comparison as left vs right numbers),
  scheduled every N weeks with a chart. Left/right asymmetry is a first-class view, since I'm working on one-sided compensation.

### 5d. Readiness (feeds § 4d and § 6)

One-tap daily readiness on Today: **Green / Yellow / Red** (my WHOOP recovery colour), filled in automatically if WHOOP is connected. Use it
only to suggest swaps (hard cardio → easy, add a mobility session). It never changes A2S2 maths.

---

## 6. Scheduling: "I need a PT"

Everything should land on a schedule and remind me, without me planning each week by hand.

- **Time of day on slots:** add `TimeSlot` to `ScheduledSession`: `Morning`, `Midday`, `Evening`, a specific time, or **`AfterSession`**
  (attached to another slot, used for finishers and post-lift stretches). Multiple slots per day already work.
- **Rules engine:** generalise `PlanScheduleRules` into composable rules with warnings: no hard cardio the day before a lower day, at least one full
  rest day, high-intensity weekly band, mobility at least N times a week, a finisher only attached to a lift. Add a "Fix it" action that proposes a
  valid alternative layout.
- **Missed sessions:** when a slot is missed, suggest the next day it fits without breaking rules (one tap to move it). Respect Pause phases.
- **Weekly review (the coach):** every Sunday evening, generate a review: done vs planned, PRs and benchmarks, what progressed (TM changes, cardio steps,
  C25K week, mobility levels), what's coming (deload week, benchmark due, checkpoint date), and any suggested adjustments. Shown as
  a card on Today and pushed as a notification. Build it with rules, not an LLM. Leave a seam for an LLM summary later.
- **Calendar feed:** a read-only ICS subscription URL (`webcal://…/calendar/{token}.ics`), with a revocable token like the API tokens, so the plan shows in
  the iOS Calendar with alerts. It works even when push isn't enabled.

---

## 7. Notifications

Extend the existing Web Push nudge service (it has `SessionStart` and `HabitsUnderMinimum`):

| Nudge | When |
|---|---|
| Session start | at each slot's time (now per slot, not one per day) |
| Finisher | when the parent lift is completed (manual or Hevy pull): "Finisher: 7 min stair @ L9" |
| Evening mobility/yoga | the evening slot's time, with a deep link that opens the player |
| Habits under minimum | exists |
| Weekly review | Sunday at a set time |
| Heads-up | the day before a deload week, a benchmark, or a checkpoint |

- Per-kind toggles, quiet hours, a daily cap (default 4), and each kind at most once per day (the `PushNudgeLog` pattern).
- Every notification deep-links into the right screen in the installed app.
- iOS: permission is requested only from a button tap, and only in the installed app (Settings already detects this). Evaluate
  Declarative Web Push (iOS 18.4+) for reliability and the Badging API to show undone items for today.
- A **real-iPhone checklist** in `docs/development.md`: install, enable, receive each kind, tap through, and confirm it survives a restart.

---

## 8. Testing

- Unit tests for every new strategy, rule and evaluator, covering edges: a pain flag on the last qualifying session, a benchmark in a deload
  week, a stage transition followed by undo, a track paused during a Pause phase, zone data missing (HR-based criteria must
  fall back to effort only).
- Hevy fixtures for the stair finisher and walk/run pulls: dedupe, re-pull and edits.
- Scheduler: slot moves across BST/GMT changes, attached finishers when the parent moves, ICS output validated against RFC 5545 with a parser.
- Player: Vitest with fake timers for side switching, pause and resume, and offline completion.
- Playwright on a mobile viewport, signed in: author a routine, play it, log a stair finisher, see the next prescription
  step up, receive the weekly review card.
- The a11y gate from § 1b, plus Lighthouse installability.

## 9. Working agreement

- Small PRs per phase on feature branches. Never push to `master`.
- Additive, reversible EF migrations. Back up production before the first one, and test migrations against a restored copy.
- Stop and ask me before deleting features, changing progression maths, or touching the production DB.
- Keep `README.md` (user-facing), `docs/development.md` and `AGENTS.md` up to date in the same PR as the change.

**Order:** Phase 1 audit and cleanup (1a–1c) → my approval → platform fixes (§ 2) → golden progression tests → **gym mode (§ 3a)** →
a11y fixes and the CI gate → cardio engine and templates → Hevy cardio push/pull → mobility builder,
library and player → scheduling (slots, rules, missed-session moves, ICS) → notifications → weekly review → WHOOP (optional) → final live walkthrough with
screenshots compared against the Phase 1 audit.

## 10. Defaults where I haven't decided (change them in the plan if you disagree)

- Finisher days: upper days only to start.
- Walk builder: treadmill incline. Runs: treadmill or grass.
- Effort scale: Easy/Solid/Brutal. Exact RPE optional.
- WHOOP: manual fields first, API after the core ships.
