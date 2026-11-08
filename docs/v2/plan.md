# 99-Strength v2 "Whole Plan" — implementation plan

> **For agentic workers:** this is the phase plan the brief asks for. Once approved, each phase gets its own task-level plan under `docs/superpowers/plans/` (written with `superpowers:writing-plans`) and is executed with `superpowers:subagent-driven-development` or `superpowers:executing-plans`. Do not start Phase 2 before Joe approves this document.

**Goal:** turn 99-Strength from an A2S2 progression calculator into the one phone app Joe runs his whole training plan from: lifting synced to Hevy, cardio, mobility, habits, weekly goals, weight checkpoints, and a ready-made "Cut to 85 — Nov 2026" plan he can start with one tap.

**Architecture:** keep the existing `Workout` aggregate and its A2S2 maths untouched. Add new aggregates beside it (`TrainingPlan`, `CardioProtocol`, `MobilityRoutine`, `HabitDefinition`/`HabitLog`, `BodyWeightEntry`) under the same Clean Architecture + CQRS rules. Move all Hevy traffic server-side with stored Hevy ids, and make the frontend a phone-first PWA with an offline queue for habits and completions. Every date that a human sees is a `DateOnly` in Europe/London, produced through an injected `TimeProvider`.

**Tech stack:** .NET 10, EF Core 10, Postgres 16, MediatR, FluentValidation, Polly, ASP.NET Core Data Protection; React 19, Vite 7, Tailwind 4, TanStack Query, `vite-plugin-pwa` (Workbox), `idb` for IndexedDB, Clerk; xUnit + Testcontainers, Vitest, Playwright, Storybook 10 + MSW.

**Spec:** `docs/v2/brief.md` (Joe's brief, with the Bitwarden session left as its placeholder). Audit: `docs/v2/audit.md`.

## Global constraints (from the brief and `AGENTS.md`)

- Clean Architecture + CQRS: thin handlers, zero-dependency Domain, `Result<T>`, factory methods only, internal mutation via aggregate roots.
- The existing 1,243 .NET tests (794 Domain, 186 Application, 90 Infrastructure, 173 API) plus 261 Vitest tests stay green. On a fresh database 77 API tests and 3 Vitest tests already fail on `master` (audit Appendix A); Phase 2 fixes both before anything else lands.
- A2S2 progression maths does not change. Any change there stops for Joe.
- Never write the Hevy key or Bitwarden session to a committed file, fixture, log line, commit message, PR description or chat output.
- EF migrations are reversible and safe on existing data. Back up production before the first v2 migration and rehearse every migration against a copy.
- All human-facing dates are local dates in Europe/London. Week = Monday to Sunday.
- Small PRs per phase on feature branches off `master`. Never push to `master` directly.
- Out of scope: protein and calorie tracking, step counting, anything needing MacroFactor.
- Stop and ask before deleting an existing feature, changing A2S2 maths, or touching the production database.

## Review focus (inputs the brief implies but no single task owns)

1. **Completing a session at 23:59 vs 00:01 local on the night the clocks change** (25 Oct 2026, 28 Mar 2027): the session must land on the local date the lifter experienced, not the UTC one. Pinned in Phase 3 `LocalClock` tests and Phase 6 matcher tests.
2. **The same Hevy workout arriving twice** (events endpoint replays, two devices refreshing): one completion, one habit tick, no duplicate routine. Pinned in Phase 6 idempotency tests keyed on `hevyWorkoutId`.
3. **An offline tap queue replayed after the day has rolled over**: the tap must be written to the date it was made, not the date it synced. Pinned in Phase 4 queue tests (each queued op carries its `localDate`).
4. **A Pause phase that starts mid-week with two sessions already done**: those sessions count, the rest are not failures, and the A2S2 week does not advance until the phase ends. Pinned in Phase 3 `TrainingPlan` phase tests.
5. **The suggested plan started on a date that is not a Monday** (first run on a Wednesday): start date snaps to the coming Monday and nothing is marked missed before it. Pinned in Phase 7 seed tests.

---

## Decisions this plan makes

| Decision | Choice | Why |
|---|---|---|
| Terran CC habits | **Migrate into 99-Strength and retire habits in Terran CC**, not sync | Terran CC's store is a SQLite file on the desktop PC behind a loopback-only daemon (`127.0.0.1:47821`). The phone cannot reach it without a tailnet relay, and a two-way sync would need conflict rules for 24 rows of data. Migration is a one-off import. Terran CC keeps tickets, to-dos and the timer; its habit widget can later read 99-Strength's `/habits/today` if Joe wants it back on the desktop |
| Terran CC targets carried over | Walks 2/day (min 1), Standing desk 4/day (min 2), Creatine + supplements checkbox. Water becomes the brief's 6 × 532 ml bottle counter (min 4) rather than 3000 ml in 250 ml steps. **Protein 180 g is dropped** because the brief rules protein tracking out of scope | Open question 2 if Joe disagrees |
| Hevy pull mechanism | Poll `GET /v1/workouts/events?since=<cursor>` on app open, on pull-to-refresh, and from a server job every 15 minutes while a session is scheduled today | Hevy's public API has no webhooks (spec read 8 Nov 2026). The events endpoint returns updates and deletes newest-first, max 10 per page, so a per-user cursor makes each poll cheap |
| Who talks to Hevy | **The server only.** Routines, folders, templates and workouts are created by the API, and their ids are stored. The browser never sees the key again | Fixes the plaintext-key-in-browser problem and the lost-sync-state problem at once. The `/hevy/{resource}` proxy stays for the Exercises page read paths until Phase 6 retires it |
| Key encryption | EF value converter backed by ASP.NET Core Data Protection, keys persisted to the database (`PersistKeysToDbContext`) so a redeploy does not lose them. New column `HevyApiKeyProtected`; a startup backfill encrypts legacy rows; the plaintext column is dropped one phase later | Reversible: until the drop, the old column still exists and the backfill is idempotent |
| Local dates | `TimeProvider` injected everywhere; a `LocalClock` service converts to `DateOnly` in `Europe/London`; completion records gain `LocalDate`; habits and body weight are keyed by `DateOnly` | Removes the five hard-coded "Day 1 = Monday" sites and gives streaks a real calendar |
| Offline queue | IndexedDB via `idb`, one `ops` store of `{id, kind, localDate, payload, createdAt}`; replay in order with last-write-wins per (habit, date) on the server; a `pending` badge on the tab bar | Brief allows LWW; it keeps the server rule trivial |
| Cardio in Hevy | Push as routines using custom exercise templates of type `duration` / `distance_duration` with `duration_seconds` and `distance_meters` sets; interval work (4×4) as alternating sets with notes | Spec supports these fields on routine sets |
| Muscle groups | Re-seed `ExerciseDefinition.MuscleGroup` from the `Hevy: <group>` token already in each description (20 values incl. `cardio`), and add `SecondaryMuscleGroups` from `scripts/hevy-exercises.json`; map Hevy groups to the brief's seven budget buckets | No invented data; an idempotent update migration |
| Playwright strategy | Storybook-driven Playwright for UI states (already works), plus a seeded-database Playwright run for journeys that needs a Clerk **test user** (open question 1). The existing Clerk-stubbing e2e suite is retired rather than repaired | It almost certainly cannot run today (audit 1.5) |
| Meals | Re-model in EF Core: `Meal` with structured ingredients and `ShoppingList`/`ShoppingListItem`; import the ten cut-friendly seed recipes from CooksAssistant's `feature/import-to-bdtv-factory` branch as a JSON fixture; copy the share/copy export logic; add CSV. Never read CooksAssistant's database | Its live schema is uncertain, Clerk-keyed and has no migrations; copying keeps that app safe |

---

## Phase 2 — Secrets, encryption and ground-clearing (PR 1)

Branch `feat/v2-secrets`. Small enough to merge in a day. No new product features.

**Backend**
- Add `UserSecretsId` to `A2S.Api.csproj`; document `dotnet user-secrets set Hevy:LiveTestApiKey <key>` in README. Load the Bitwarden item "Hevy" into user-secrets locally; nothing is committed.
- `HevyApiKeyConverter : ValueConverter<string?, string?>` using `IDataProtectionProvider.CreateProtector("User.HevyApiKey")`; `services.AddDataProtection().PersistKeysToDbContext<A2SDbContext>()`.
- Migration `AddHevyApiKeyProtected`: add column; `HevyApiKeyBackfillHostedService` encrypts rows where protected is null and plaintext is not, logs a count only. Migration `DropHevyApiKeyPlaintext` is written now but applied in Phase 6 after a week in production.
- `GET /users/me/hevy-api-key` returns `{ hasKey: bool, lastFour: string }`; `PUT` unchanged; `DELETE` added. Remove the key from `HttpContext.Items` and from `SyncRoutineToHevyCommand`; handlers resolve it through `IHevyKeyResolver.ForUserAsync(UserId)`.
- Serilog: `Destructure.ByTransforming<User>` to mask, `MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Warning)` in Development, and `EnableSensitiveDataLogging` off.
- `GET /users/{id}`: ownership check (403 otherwise). `POST /users` removed. `/simulate/stream` and the seed endpoint behind `IHostEnvironment.IsDevelopment()`.
- Fix `AGENTS.md` (hard delete, migration bundle, proxy is the real Hevy path).
- `ExerciseDefinitionSeeder.SeedAsync` upserts by name (insert missing rows, never delete) instead of returning when any row exists, so a fresh database gets the full library even after the Pallof Press migration. This alone turns the 77 failing API tests green; a Testcontainers test pins it: migrate, seed, assert 445 rows and that `Squat (Barbell)` exists.

**Frontend**
- `HevyProvider` stops fetching the key; `hevyApi` stops sending `X-Hevy-Api-Key`; the proxy resolves the stored key server-side. Settings page gets a masked key field with Replace / Remove.
- Fix the three `ExerciseLibraryPage` tests (wrap in `HevyProvider`). Remove the destructive seed button from Settings (keep the JSON export).

**Tests:** converter round-trip; backfill idempotent on a Testcontainers DB with a legacy row; `GET /users/me/hevy-api-key` never contains the key; a Serilog sink test asserting the key string never appears in captured output when `PUT` runs in Development; ownership 403; one live smoke test `HevyLiveSmokeTests.ValidateKey` skipped unless `Hevy:LiveTestApiKey` is present.

**Exit:** all suites green, including the three Vitest fixes. **Before merging:** Joe runs the production backup (open question 1) and the backfill is rehearsed against the copy.

## Phase 3 — Domain expansion and migrations (PRs 2–4)

Branch `feat/v2-domain-plan`, `feat/v2-domain-habits`, `feat/v2-domain-volume`. Pure backend; the UI arrives in Phase 4.

**PR 2 — time and plan**
- `A2S.Domain/Common/LocalClock.cs`: `LocalDate Today()`, `LocalDate ToLocalDate(DateTime utc)`, `(DateTime startUtc, DateTime endUtc) BoundsOf(LocalDate)`; built on `TimeProvider` and `TimeZoneInfo.FindSystemTimeZoneById("Europe/London")`. `WorkoutActivity` gains `LocalDate CompletedOn` (backfilled from `CompletedAt` in a migration).
- `TrainingPlan` aggregate: `Create(UserId, name, LocalDate startDate, WeeklyTemplate)`, `ScheduledSession` slots per `DayOfWeek` with kinds `Lift(DayNumber)`, `Cardio(CardioProtocolId)`, `Mobility(RoutineId)`, `Rest`; `Phase(PhaseType Cut|Maintenance|Pause|Deload, LocalDate from, LocalDate to)`; `Checkpoint(LocalDate date, Weight target, string note)`; `LinkWorkout(WorkoutId)`; `SessionFor(LocalDate)`; `IsPaused(LocalDate)`.
- `PlanCalendarService`: given a plan and completions, returns per-date status `Planned | Done | Missed | Paused | Rest`, with Pause dates never `Missed`, and `WeeklyGoals ForWeek(LocalDate anyDay)` (lifts planned vs done, cardio, mobility, flexible meal, alcohol).
- `CardioProtocol` aggregate with three factories: `NorwegianFourByFour()` (10 + 4×(4+3) + 5 = 38 min), `CouchToFiveK(weekIndex, repeatWeek)` (9 weeks of fixed data), `Liss(minutes 30–40)`; `CardioSession` log `{duration, avgHr?, rpe?, distance?, notes}`.
- `MobilityRoutine` aggregate: ordered `Drill(name, sets, dose: Reps|Seconds|Breaths, sideBias Left|Right|None, cues, standingAlternative)`; `MobilityCompletion(LocalDate, perDrill?: bool[])`.
- Commands/queries: `CreateTrainingPlan`, `UpdateSchedule`, `AddPhase`, `AddCheckpoint`, `LogCardioSession`, `CompleteMobility`, `GetToday`, `GetPlanCalendar(month)`, `GetWeeklyGoals`.
- Migration `AddTrainingPlan` (new tables only) + `AddCompletedOnLocalDate` (additive column with backfill).

**PR 3 — habits and body weight**
- `HabitDefinition(UserId, name, HabitType Counter|Numeric|Checkbox, target, minimum, unit, order, archived)`; `HabitLog(HabitId, LocalDate, value)`; `HabitService.Streak(habits, logs, today)` with the never-miss-twice rule and the minimum-version rule (a day counts when every active habit reached at least its minimum); `Increment`, `Decrement` (floor 0), `SetValue`, `Toggle`. Over-target is allowed and reported as `value/target`.
- `BodyWeightEntry(UserId, LocalDate, Weight)`; `TrendService`: 7-day moving average and EWMA (α = 0.1), `PaceAgainst(checkpoints, startWeight)` returning `OnPace | Behind | Ahead` from the straight line between neighbouring checkpoints.
- Commands/queries: `UpsertHabitLog` (idempotent per habit+date with LWW on `clientTimestamp`), `CreateHabit`, `ArchiveHabit`, `ImportHabits` (used by Phase 5), `RecordBodyWeight`, `GetHabitsToday`, `GetHabitHistory(range)`, `GetWeightTrend`.
- Migration `AddHabitsAndBodyWeight`.

**PR 4 — volume budget and time estimate**
- Migration `BackfillMuscleGroups`: parse `Hevy: <group>` from `ExerciseDefinition.Description`; add `SecondaryMuscleGroups` (jsonb) from `scripts/hevy-exercises.json`; map to budget buckets `{chest, back, shoulders, quads, hamstrings, glutes, core}`.
- `VolumeBudgetService.HardSetsPerGroup(week plan) : IReadOnlyDictionary<Bucket,int>` and `Warnings(caps)`; default caps chest 10, back 14, shoulders 10, quads 10, hamstrings 10, glutes 12, core 12 stored on `TrainingPlan`.
- `SessionTimeEstimator.Estimate(day) = warmUpMinutes(10) + Σ sets × (restSeconds + 40 s)` with supersets sharing rest; warning over 55 minutes.
- Query `GetDayBudget(workoutId, day)`; `UpdateExercises` and `SubstituteExercise` responses include the warnings.

**Tests (highlights):** `LocalClock` at 2026-10-24T23:30Z, 2026-10-25T00:30Z, 2027-03-27T23:30Z, 2027-03-28T00:30Z, 2026-12-31T23:59Z; Pause phase mid-week; checkpoint pace against the brief's dates (95 → 92 by 7 Dec, 91 by 21 Dec, hold to 3 Jan, 88 by 1 Feb, 85 by 8 Mar 2027); streak across a month boundary with one miss and with two; counter floor; time estimate within ±10% of a hand calculation for Upper A; caps fire when a fourth chest accessory is added. Every migration tested up and down against a Testcontainers copy seeded from a production-shaped fixture.

## Phase 4 — Today / Habits PWA UI (PRs 5–7)

Branch `feat/v2-pwa-shell`, `feat/v2-today`, `feat/v2-offline`.

**PR 5 — shell:** `vite-plugin-pwa` with manifest (name, 192/512 + maskable icons, apple-touch-icon, `display: standalone`, theme colour from `--color-background`), Workbox precache for the app shell and runtime `NetworkFirst` for `/api/v1/habits/*`; bottom tab bar **Today / Plan / Habits / Progress / Settings** with 56 px targets; move Simulator, Hevy Data and Programs under Settings → Developer; delete dead code listed in audit 1.5; `inputMode="decimal"` on every numeric input; fix the hamburger scrim bug by removing the hamburger. **Clerk iOS standalone spike** comes first: install on an iPhone from the Vercel preview, sign in, force-quit, reopen. If the session is lost, switch Clerk to `routerPush` in-app routes and `signInUrl` on the same origin, and document the result in `docs/v2/clerk-ios.md`.
**PR 6 — Today and Habits:** `TodayPage` answers "what do I do now": session card (kind, exercises or protocol, **Start** → session page, or **Open in Hevy** deep link `hevy://routine/<id>` with web fallback), habit counters as 64 px `+` buttons with `−` behind a long-press, weigh-in field, weekly goal rings, streak chip. `HabitsPage`: list, history heatmap, edit targets and minimums. `ProgressPage`: weight trend vs checkpoints with on-pace label, volume per bucket this week. Session page: planned sets from `GetWeekPlan`, partial completion allowed, deload banner, fix the five bugs from audit 2.3.
**PR 7 — offline:** `idb` queue, `useOfflineMutation` wrapping habit taps and `CompleteDay`; pending badge; replay on `online` and on app open; Vitest for queue ordering and LWW; a Playwright test that goes offline, taps water twice, comes back online and sees 2/6 on the server fixture.
**PR 7b — notifications and install docs:** web push with VAPID keys in user-secrets and the Dokploy environment, a `PushSubscription` table, a server job that sends "time to train" at the scheduled session's start time and a habit nudge at 20:00 local when any habit is still under its minimum; the Settings page detects support (`'PushManager' in window`, and on iOS only when `navigator.standalone` is true on 16.4+) and otherwise shows the install steps instead of a broken toggle. README gains "Install on iPhone" (Share → Add to Home Screen) and "Install on Android" (Chrome → Install app) sections with screenshots.

**Exit:** logging a bottle of water is one tap from app open; Lighthouse installable; axe clean on every new story.

## Phase 5 — Terran CC migration (PR 8)

Branch `feat/v2-terran-import`. `tools/import-terran-habits.mjs` reads `tcc.db` (or `tcc habit --json` per date) and posts to `POST /habits/import` with the mapping in the decisions table; the 24 historical days are imported so the streak is honest. After Joe confirms the import, he archives the five habits in Terran CC with `tcc habit edit --archived` (his call; this plan does not touch Terran CC's repo). Test: import is idempotent; a re-run changes nothing.

## Phase 6 — Hevy push and pull, server-side (PRs 9–11)

Branch `feat/v2-hevy-links`, `feat/v2-hevy-pull`, `feat/v2-hevy-inbox`.

- **Links:** `HevyLink(UserId, kind Routine|Folder|Template|Workout, localKey, hevyId, syncedAt)` table; push is idempotent on `localKey` (`plan:{planId}:w{week}:d{day}`); routines named `"{Plan} · W{week} {Upper A}"`; **Mobility A drills are the first exercises** of each lifting routine; cardio routines use duration/distance templates; `HevyOptions` gains `TimeoutSeconds = 15`; Polly: retry only idempotent GETs, honour `Retry-After` on 429, circuit per API key, map 401/403/429 to `Result` errors with user-facing messages ("Hevy says the key is invalid", "Hevy Pro is required", "Hevy is rate limiting, try again in N s").
- **Pull:** `HevyPullCursor(UserId, lastEventAt)`; `PullHevyEvents` command walks `/v1/workouts/events?since=` and upserts `HevyWorkoutSnapshot`; `WorkoutMatcher` matches by `routine_id` → stored link, else by local date + title; a matched lifting workout maps sets to `CompleteDayCommand` (AMRAP = last `normal`/`failure` set of the linked exercise) exactly as manual entry; cardio and mobility mark their sessions done. Dedupe key `hevyWorkoutId`; a second event for the same id updates the snapshot and never re-completes.
- **Inbox:** unmatched workouts listed on Today; `AssignHevyWorkout(workoutId → scheduled session or "extra")` in one tap; graceful offline and failed-sync states.
- Optional: `MirrorWeighInToHevy` posting `body_measurements` (409 means update with PUT).

**Tests:** contract tests against recorded fixtures under `tests/A2S.Integration.Hevy.Tests/Fixtures/` (routine push, events pull, matching, dedupe, inbox, 401/403/429/5xx, timeout); one live smoke test as in Phase 2. The plaintext key column is dropped in PR 11.

## Phase 7 — Suggested plan seed and onboarding (PR 12)

Branch `feat/v2-suggested-plan`. `SuggestedPlans/CutTo85Nov2026.cs` builds: the weekly template from the brief (Mon Upper A … Sun Rest), rule "no 4×4 the day before Lower A or B" as a validation warning, the four lifting days with A2S2 main lifts and the listed accessories/supersets (using existing RepsPerSet/Linear strategies, side bias and "extra set left"), Mobility A and B with standing alternatives, the three cardio protocols, the five habits, the checkpoints, caps and the 55-minute flag. Onboarding: first run shows "Start this plan"; the only inputs are four training maxes (bench, squat, OHP, deadlift) and a start weight (default 95 kg); start date = coming Monday. Tests: seed matches the brief line by line (a table-driven test), estimate ≤ 55 min for each day, caps not exceeded by the seed itself.

## Phase 8 — Test hardening (PR 13)

Everything in brief §6 not already covered: 21-week run with AMRAP above/at/below target checked against `research/A2S2_Validation_Data.md`; deload 7/14/21; custom block sequences; restart after 21; undo after rollover and of the first completion; double-tap complete; substitution mid-block; missing one/two weeks; out-of-order and two-a-day sessions; Pause/Maintenance; DST and New Year; Lighthouse PWA check in CI; Storybook axe. Add a GitHub Actions workflow that runs Domain, Application, Vitest and Storybook on every PR, and Infrastructure/API (Testcontainers) on `ubuntu-latest`.

## Phase 9 — UX re-walkthrough (PR 14)

Repeat audit §2 live on a 390 px viewport with a Clerk test user, save to `docs/v2/screens-after/`, and write `docs/v2/audit-after.md` comparing tap counts and friction points one by one.

## Phase 10 — Meals and shopping list (PRs 15–16, after the core ships)

`Meal` aggregate (title, mealType, `Ingredient{name, quantity, unit}[]`, steps, tags, cutFriendly); `ShoppingList` + items with `checked` and `sourceMealIds`; `GenerateShoppingList(mealIds)` normalises (trim, lower-case) and sums quantities per (name, unit); export as text (share sheet via `navigator.share`, clipboard fallback), copy, and CSV `name,quantity,unit,checked`. Seed: the ten recipes from CooksAssistant's branch as `meals-seed.json`, ingredients parsed by hand once. UI: Meals tab under Plan, "this week's meals" picker, list with checkboxes. CooksAssistant is not modified.

---

## Rollout and safety

1. **Backup before Phase 2 merges:** `pg_dump --format=custom` of production to Joe's machine and to the VPS; restore into a local Postgres 16 container and run every v2 migration up and down against it before each phase merges.
2. **Deploy order per phase:** migrate (bundle in the entrypoint already does this), deploy API, deploy Vercel frontend. Additive-only migrations until Phase 6's column drop.
3. **Feature flags:** `Features:V2Shell` gates the new tab bar so the old nav can come back with a config change during Phase 4.
4. **Rollback:** each phase is one PR; revert the PR and run the migration's `Down` from the bundle.

## Open questions for Joe (answers unblock Phase 2)

1. **Where is the production API and Postgres?** The frontend is on Vercel at `99-strength.enjoeneer.dev`; the API has a Dockerfile and compose file but no CI and no knowledge-base entry. I need host access or a `pg_dump` from you before the first migration.
2. **Protein habit:** drop it (my default), or keep as a plain checkbox?
3. **Clerk test user:** can you create one on the dev instance and put its email and password in the gitignored `src/A2S.Web/.env.test`? Without it the live walkthroughs in Phases 4 and 9 cannot run.
4. **Retire Terran CC habits** after the import, or keep logging in both for a week?
5. **Custom Hevy exercise templates** for the mobility drills and cardio protocols will be created on your Hevy account by the server (the API cannot delete them). OK?
6. **Units:** kilograms only in v2? The code carries lbs everywhere, and dropping it simplifies every new screen.
7. **Standing-desk minimum:** I set 2; confirm.
8. **Mirror weigh-ins to Hevy body measurements?** Optional in Phase 6.

## What approval means

Approving this document approves the decisions table, the phase order and the open-question defaults where you give no other answer. Each phase then gets a task-level plan with tests named per task, and a PR you review.
