# 99-Strength v2 audit

Date: 8 November 2026. Audited commit: `394883f` (branch `feature/storybook-showcase`, PR #24, 8 commits ahead of `master` at `347d6bc`; the two differ only in Storybook, MSW mocks and theme fixes). Audited by reading the code, running the test suites, and capturing every page at a 390 × 844 phone viewport from the Storybook build. Screenshots are in `docs/v2/screens/` (one viewport shot per page, plus `--full` for pages taller than a screen, and `capture-log.json` with page heights and tap-target counts).

**How the screenshots were taken.** The Playwright capture ran against the static Storybook with MSW mock data (a lifter in week 11 of a 21-week programme), using the repo's own `scripts/storybook-harness.mjs`. A live walkthrough against the real API was not possible: the repo has no `.env.test` with a Clerk test user (the gitignored file `tools/audit-capture.mjs` expects), and creating one on the Clerk dev instance is something only you can do. The journeys below are therefore narrated from the code paths, with the Storybook screenshots as visual evidence. Phase 9 should repeat them live once a test user exists.

## 1. Current feature map

### 1.1 Pages (frontend)

| Route | What it does | Tests | Verdict |
|---|---|---|---|
| `/sign-in`, `/sign-up` | Clerk widgets inside `AuthShell` | none | Works |
| `/dashboard` | Welcome, three stat tiles, current programme card with **Start W{n} D{n}**, next-week preview, exercise tracking charts, personal records | Vitest + story | Works. 11,500 px tall on a phone; the start button is below the fold |
| `/workout` | Week overview: block pills, programme progress, per-day cards with Sync / Edit / Pull / Substitute, undo | story only | Works, but a day card has **no Start button**. The only ways into a session are the dashboard button or a Hevy pull |
| `/workout/session/:day` | Set logging, AMRAP prompt, complete day, completion summary | child components only; the page and its 484-line hook have no tests | Works with bugs (see 2.3) |
| `/setup` | 4-step wizard: welcome, template, exercises, review | story only; file is 619 lines against a 500 cap | Works. Training maxes are hard-coded in the templates, the wizard never asks for yours |
| `/programs` | List, activate, delete programmes | Vitest + story | Works. Delete is a hard delete |
| `/exercises` | Hevy exercise catalogue with filters and per-exercise history | Vitest (3 failing on `master`) + story | Works |
| `/hevy` | API key entry, list and delete routines | story only | Works. Key can be shown unmasked |
| `/hevy/data` | Workouts pulled from Hevy, paginated, charts | Vitest + story | Works |
| `/settings` | **Seed Test Data** and JSON export only | Vitest + story | Half-built and dangerous: seed deletes every workout you own then fabricates 68 completions, with no environment gate. No real settings exist |
| `/history` | Calendar, summary, session detail, one-rep max, CSV export | helpers tested, page not | Works |
| `/simulate` | Projection and NDJSON streaming of simulated completions | Vitest + story | Works, but it is a dev tool sitting in the main nav |

Navigation: a top bar with 9 links behind a hamburger below 1024 px. There is no bottom tab bar and no mobile-specific navigation. The open menu (`screens/dashboard--menu-open.png`) lists Simulator, Hevy, Hevy Data and Settings alongside the real pages.

### 1.2 API endpoints and handlers (backend)

All controllers are `[Authorize]`. "Works" means reachable, used by the UI, and covered by integration tests.

| Endpoint | Handler | Verdict |
|---|---|---|
| `POST /workouts`, `GET /workouts`, `GET /workouts/current`, `POST /workouts/{id}/activate`, `DELETE /workouts/{id}` | CreateWorkout, GetAllWorkouts, GetCurrentWorkout, SetActiveWorkout, DeleteWorkout | Works |
| `POST /workouts/{id}/days/{day}/complete`, `POST /workouts/{id}/undo-last-completion` | CompleteDay, UndoCompletion | Works. This is the A2S2 engine and it is well tested |
| `PUT .../exercises`, `PUT .../substitute`, `DELETE .../exercises/{id}`, `PUT .../working-weight`, `POST .../confirm-weight`, `POST .../confirm-working-weight` | UpdateExercises, SubstituteExercise, RemoveExercise, UpdateWorkingWeight, ConfirmStartingWeight, ConfirmWorkingWeight | Works (the last three have handler tests only) |
| `PUT /workouts/{id}/block-sequence`, `GET .../weeks/{w}/days/{d}/plan` | UpdateBlockSequence, GetWeekPlan | Works. `GetWeekPlan` is the documented single source of truth for planned sets, and **nothing in the frontend calls it** |
| `GET /workouts/{id}/simulate` | SimulateWorkout | Works |
| `GET /workouts/{id}/simulate/stream` | SimulateAndCompleteDay in a loop | Half-built: commented "dev-only" but not gated, writes real completions in production, no tests |
| `POST /workouts/{id}/progress-week` | ProgressWeek | **Dead.** It requires every day complete, but `CompleteDay` already rolls the week on the last day, so the precondition can never hold (`Workout.cs:256` vs `:689`) |
| `POST .../retrofix-tm` | RetrofixLinearTm | Half-built: no UI, no API test |
| `GET /workouts/exercises/library`, `.../{name}/history`, `GET /workouts/history` | GetExerciseLibrary, GetExerciseHistory, GetWorkoutHistory | Works. The library's muscle-group filter has no effect because every row is tagged "General" |
| `GET /users/me` | GetUserById | Works |
| `GET /users/{id}` | GetUserById | Works but has **no ownership check**: any signed-in user can read any user's email |
| `POST /users` | CreateUser | Dead or risky: creates a user with a random id not linked to Clerk. Real users come from `AutoProvisionUserMiddleware` |
| `PUT`/`GET /users/me/hevy-api-key` | repository directly | Half-built: no MediatR, no tests, returns the key in plaintext |
| `POST /hevy/sync/routine` | SyncRoutineToHevy | Half-built: the frontend never calls it; requires a header key and ignores the stored one |
| `GET /hevy/validate`, `GET/POST/PUT/DELETE /hevy/{resource}` | raw HTTP proxy | Works. **This proxy is what the app actually uses for Hevy** |
| `GET /hevy/data/workouts`, `.../exercises/{templateId}/history` | raw HTTP | Works, lightly tested |

Frontend calls with no backend: `PUT /workouts/{id}/hevy-folder` and `POST /workouts/{id}/hevy-synced-routine` (`api/workouts.ts:120,132`) both 404. The DTO fields `hevyRoutineFolderId` and `hevySyncedRoutines` exist only in the TypeScript types. As a result **no Hevy routine, workout or folder id is ever saved**; only `Exercise.HevyExerciseTemplateId` is.

### 1.3 Hevy flows as they stand

- **Push.** Done in the browser through the proxy: `hevySyncService.ts` creates folders, exercise templates, routines (per day, and the whole week) and completed workouts. Routines are deduplicated by **title string**, folders by title. A failed page fetch during the dedupe lookup makes a routine look absent, so a duplicate is created.
- **Pull.** Manual, from a day card. `hevySyncPull.ts` pages 5 × 10 workouts, matches by title ("week N day N" or weekday name), and hands the result to the session page through router state. Nothing is saved server-side.
- **Backend service.** `HevyIntegrationService` has push, pull, delete and validate methods; only `SyncRoutineForDayAsync` is reachable and the frontend does not use it. The rest is dead.
- **Resilience.** Polly retries 5xx and timeouts three times (2, 4, 8 s) and has one circuit breaker shared by every user. POSTs are retried, so a timed-out create can produce duplicates. **429 is not handled** and `Retry-After` is ignored. 401 and 403 are flattened to `null` or "Failed to create routine". `BrokenCircuitException` is not caught, so an open circuit becomes a 500.
- **Key handling.** `User.HevyApiKey` is plaintext in Postgres (`varchar(256)`, no converter). `GET /users/me/hevy-api-key` returns it, `HevyProvider` fetches it on mount, keeps it in React state and sends it back as `X-Hevy-Api-Key` on every proxy call. In Development, EF sensitive-data logging writes the `UPDATE "Users"` statement with the key to the console and `logs/app-.log`. `SyncRoutineToHevyCommand` is a record containing the key, so its `ToString()` prints it.
- **Hevy's public API today** (from `https://api.hevyapp.com/docs/`, read on 8 Nov 2026): header `api-key`; `page` and `pageSize` (max 10); workouts GET/POST/PUT plus `GET /v1/workouts/events?since=` for incremental updates and deletes; routines GET/POST/PUT (POST can return 403); routine folders; exercise templates GET/POST (custom types include `duration`, `distance_duration`, `weight_duration`); `body_measurements` GET/POST/PUT keyed by date with `weight_kg`; routine and workout sets support `duration_seconds` and `distance_meters`. **There are no webhook endpoints in the public spec** and no documented rate limit. The code sends `pageSize` in the service but `page_size` in the data controller; only the former matches the spec.

### 1.4 Data, dates and events

- **Migrations:** three (`InitialCreate`, `AddHevyApiKeyToUser`, `SeedCableCorePallofPress`). Startup refuses to run with pending migrations; the Docker entrypoint applies them with an EF bundle. `AGENTS.md` still says the app migrates on startup and that deletes are soft. Both are out of date.
- **Exercise library:** 445 entries seeded from `exercise-library.json` only when the table is empty. The JSON has no muscle-group or compound fields, so every row gets `MuscleGroup = "General"` and `IsCompound = true`. The real Hevy muscle group is buried in the description text (`"Hevy: quadriceps"`, 20 distinct values). The volume budget in the brief cannot be built on this as-is.
- **Dates:** every timestamp is `DateTime.UtcNow`; there is no `TimeZoneInfo`, `DateOnly`, `TimeProvider` or clock abstraction anywhere. **The app has no concept of "today."** Progress is event-driven: the current day is the next incomplete day and the week rolls when the last day completes. Day 1 is hard-coded to "Monday" in five places (one backend, four frontend) and those weekday names feed the Hevy titles the pull relies on. The frontend calendar groups by local midnight, Hevy start times use `toISOString()`, and export filenames use UTC dates.
- **Domain events:** nine events are raised and dispatched after save, and **nothing handles them**. `UnitOfWork` transactions are unused. `AuthorizedWorkoutBehavior` covers two commands; the rest hand-roll the ownership check.
- **Errors:** handlers catch `InvalidOperationException`, but the domain throws `BusinessRuleViolationException`, so rule violations fall into the generic catch and lose their 422 mapping.

### 1.5 Tests and tooling, as measured

| Suite | Result on `master` |
|---|---|
| .NET (`dotnet test A2S.sln`) | Domain 794 passed, Application 186 passed, Infrastructure 90 passed, **API 96 passed / 77 failed**. See Appendix A: the API failures are one seeding bug, not 77 bugs |
| Vitest | 258 passed, **3 failed** in `ExerciseLibraryPage.test.tsx`: "useHevy must be used within a HevyProvider" |
| Playwright (`tests/e2e`, 63 tests) | Not run. It needs `VITE_CLERK_PUBLISHABLE_KEY` (no `.env` exists), stubs `**/clerk**` with `{}` which would break clerk-js itself, and mocks `/workouts/active` while the app calls `/workouts/current`. Treat as unverified |
| Storybook | 117 stories, every page covered, MSW-backed, 390 px harness and axe script already exist. **This is the most reliable UI tooling in the repo** |

Dead code worth deleting (no production importers): `HevySyncButton`, `useHevySync`, `useAuth`, `hevyExerciseMapping.ts` (452 lines), `TrainingMaxInput`, `DayColumnsView` and its barrel, `ui/switch.tsx`, the `progressWeek`, `confirmWorkingWeight` and `getWeekPlan` API wrappers, six re-exported Hevy helpers, `Application/Exceptions/ValidationException.cs`, and four backend Hevy service methods. `utils/weekParameters.ts` and `workoutCalculations.ts` are marked deprecated but are still what the session page uses to compute sets.

## 2. UX walkthrough on a 390 px phone

Numbers in brackets are screenshot files under `docs/v2/screens/`.

### 2.1 First sign-in

`sign-in--default.png`. Clerk's card renders on the dark shell. All nine interactive elements are under 44 px tall. After sign-in you land on the dashboard. The user is auto-provisioned by middleware, so there is no onboarding step and nothing tells you what to do first.

**Friction:** no welcome or "start a plan" moment; the first screen is a stats dashboard with zeros.

### 2.2 Starting a programme

`dashboard--no-program.png` then `setup-wizard--*.png`. Eight taps and no typing gets you from "Start Program" to "Start W1 D1" via a template: Start Program, Start from Template, Next, pick a template, Next, Next (exercise step), Create, Start. The exercise step (`setup-wizard--exercises.png`, 4,300 px tall) lists all 429 library exercises with 437 of its 490 tap targets under 44 px; it is skippable, and skipping it creates a programme with no exercises.

**Friction:** training maxes are baked into the templates (OHP 65 kg and so on) so a new user starts on someone else's numbers; the three templates are all 4-day lifting-only; "Create" drops you on the dashboard rather than into the first session; no schedule or start date is asked for, because the model has none.

### 2.3 Doing today's session and completing a day

`workout-session--bench-day.png`, `workout-session--logging-sets.png`, `features-workout-completion-summary` (see showcase). Entry is the dashboard's **Start W11 D2** button, 1,900 px down the page. The session page stacks a sticky nav (64 px) and a sticky header, then one card per exercise with a 12-column grid of 32 px inputs and Done/Log buttons (51 of 68 targets under 44 px). Targets are computed client-side with deprecated helpers rather than fetched from `GetWeekPlan`. Progress is saved to `localStorage` after the first logged set, which is good.

Completing: **Complete Workout** is disabled until every set is logged, so there is no honest way to finish a partial session. If any exercise still has an unconfirmed starting weight, a confirmation modal fires one request per exercise before the completion request. Then the completion summary appears with an optional **Send to Hevy**, and if the week rolled over it silently rebuilds next week's Hevy routines.

**Bugs found in this flow:** Undo navigates to `/workout/day/…`, which does not exist, and falls through to the dashboard (`useWorkoutSession.ts:312`). The "skip progression" decision from the Hevy reconciliation modals is never sent to the API (`workoutReconciliationHandlers.ts:93,131` vs the payload at `useWorkoutSession.ts:380`), so the toast promising it is untrue. "Delete" and "skip" in the missing-exercise modal do the same thing. The discrepancy "update" always writes the training max in kilograms. A resumed session reuses a fresh `workoutStartTime`, so Hevy gets a wrong start time. The Hevy title for a completed workout is built after the refetch, so after a rollover it is probably stamped with the new week.

### 2.4 Week rollover and deload

There is no calendar, so "rollover" means "complete the last day." The week overview (`workout--mid-program.png`) then shows the new week; the backend already handles blocks, deload weeks 7/14/21 and the block sequence, and this is the best-tested part of the system. The deload is visible only as different set counts and percentages; nothing says "deload week" on the session page.

**Friction:** you cannot see the week on a calendar or know which real date a session belongs to; the overview page is 8,700 px tall on a phone with the useful part (today's card) in the middle.

### 2.5 Missing a week

Nothing happens. Because progress is event-driven, a missed week is invisible: the programme waits. That is forgiving, but it means the app cannot show streaks, "behind schedule", or shift a plan, and the brief's Pause and Maintenance phases have nothing to attach to.

### 2.6 Substituting an exercise

From a day card or the session's swap icon. Permanent substitution opens a two-step modal (pick exercise, configure progression) and is handled by a well-tested command. On a phone the modal footers were previously found to overflow at 390 px (July audit); the showcase branch fixed the worst, but the modals still have no stories, so they are unverified here.

### 2.7 Syncing to Hevy

Settings for Hevy live on `/hevy` (`hevy--connected.png`), not on Settings. You paste a key (shown unmasked on demand), then per-day **Sync** or week **Sync Week** push routines; **Pull** fetches a completed workout by title match and sends you into the session with up to three reconciliation modals (substitutions, weight discrepancies, missing exercises). Nothing is stored about what was synced, so a reinstall or second device has no idea which routines exist and will re-create them when a title lookup fails.

**Friction:** two Hevy pages plus Exercises all compete for the same job; the pull is manual and title-based; nothing matches a Hevy workout to a date; errors surface as generic toasts.

### 2.8 Cross-cutting phone problems

- No bottom navigation; every page change is hamburger, scroll, tap.
- Pages are tall single columns: dashboard 11.5k px, programme overview 8.7k px, exercise library 36k px.
- Tap targets: 35 of 39 on the dashboard, 50 of 53 on history, 51 of 68 in the session are under 44 px.
- `workout--no-program.png` shows horizontal overflow: the two buttons run off both edges of the screen.
- The hamburger overlay is a `fixed inset-0` child of a nav with `backdrop-blur`, so its scrim covers only the bar (`dashboard--menu-open.png` shows the page content still visible and tappable below the menu).
- Number inputs lack `inputMode`, so iOS shows the full keyboard.
- No install prompt, manifest, icons or offline behaviour. Closing the tab mid-session is survivable only thanks to the `localStorage` draft.

## 3. Gap list against the brief

### 3.1 Nothing exists yet for

TrainingPlan, scheduled sessions, phases (Cut / Maintenance / Pause / Deload), checkpoints, CardioProtocol (4×4, C25K, LISS), MobilityRoutine with standing alternatives, HabitDefinition and HabitLog, minimum versions, streaks, WeeklyGoals, BodyWeight entries and trend, muscle-group tagging, volume budget, session time estimate, local-date handling in Europe/London, Hevy pull-and-match into `CompleteDay`, unmatched inbox, PWA manifest and service worker, IndexedDB queue, bottom tab bar, Today screen, web push, suggested plan seed, onboarding, Meals and shopping list.

### 3.2 Exists but must change

| Area | Why |
|---|---|
| `HevyApiKey` | plaintext at rest, returned by an endpoint, held in the browser, logged in Development |
| Hevy resilience | no 429 or `Retry-After`, POST retries, shared circuit, no timeout policy, 401/403 flattened |
| Hevy state | routine, folder and workout ids never persisted; dedupe by title; two frontend endpoints 404 |
| Hevy client ownership | pushing from the browser means every device re-derives state. v2 moves push and pull server-side |
| Exercise library | no usable muscle groups; parse the `Hevy:` tag out of descriptions or re-seed from `scripts/hevy-exercises.json` with `primary_muscle_group` |
| Dates | add a `TimeProvider`, `DateOnly` for local dates, and an explicit Europe/London conversion at the edges |
| Session page | fetch planned sets from `GetWeekPlan`; allow partial completion; fix the five bugs in 2.3 |
| Navigation | replace the 9-link hamburger with Today / Plan / Habits / Progress / Settings and demote Simulator, Hevy Data and Seed to a developer area |
| Settings | remove or gate the destructive seed; make Settings hold Hevy key, units, notifications |
| `GET /users/{id}` | ownership check or removal |
| `/simulate/stream`, `POST /users` | gate or remove |
| `AGENTS.md` | fix the soft-delete and auto-migrate claims; document the proxy as the real Hevy path |

### 3.3 Things the brief does not mention that you should decide on

1. **Protein in Terran CC.** Terran CC has a Protein 0/180 g habit. The brief says protein tracking is out of scope but also says to carry over every Terran CC habit. The plan assumes **drop Protein**; say if you want it kept as a plain checkbox.
2. **Hevy has no webhooks** in its public API. The plan uses `GET /v1/workouts/events?since=` on app open, on pull-to-refresh, and from a background job every 15 minutes while a session is scheduled.
3. **Hevy body measurements.** The API accepts a weight per date. Weigh-ins could be mirrored to Hevy so its charts match. Proposed as optional in Phase 6.
4. **Push from the server, not the phone.** Routines should be created by the API with stored ids. That changes who holds the Hevy key (the server only) and removes the key from the browser entirely.
5. **Training maxes at onboarding.** The suggested plan needs your four maxes; the wizard currently has no step for them.
6. **Production database location.** The repo has no CI and the API's host is not in the knowledge base. Back-up and migration rehearsal need the connection details. See open questions in the plan.
7. **Deload visibility and "today" on the session page.** Small, but it is the difference between the app and a spreadsheet.
8. **Clerk in an installed iOS PWA.** Known failure mode: the sign-in redirect opens Safari, which does not share storage with the home-screen app. Needs a spike before committing to the PWA path.
9. **The e2e suite is probably not runnable.** The plan replaces it with Storybook-driven Playwright for UI and a seeded-database Playwright run for journeys, rather than repairing the Clerk stubs.
10. **Three Vitest failures already on `master`.** Fix in Phase 2 so "930 tests green" is true before anything else lands.

## Appendix A: test runs

Run on 8 Nov 2026 from a clean worktree of `master` (`347d6bc`), Docker Desktop 27.3.1, Testcontainers `postgres:16-alpine`.

| Suite | Passed | Failed | Note |
|---|---|---|---|
| A2S.Domain.Tests | 794 | 0 | |
| A2S.Application.Tests | 186 | 0 | |
| A2S.Infrastructure.Tests | 90 | 0 | first attempt failed entirely because Docker Desktop's engine had wedged; a restart fixed it |
| A2S.Api.Tests | 96 | 77 | every failure is `400 Failed to create workout: Exercise template 'Squat (Barbell)' was not found in the exercise library` (also `Lat Pulldown (Cable)`, `Bent Over Row (Dumbbell)`, `Overhead Press (Smith Machine)`, `Triceps Dip (Assisted)`) |
| Vitest | 258 | 3 | `ExerciseLibraryPage.test.tsx`: `useHevy must be used within a HevyProvider` |

**Why the API tests fail on a fresh database.** The test host runs `Database.Migrate()` and then the app's startup seeder. Migration `20260615210530_SeedCableCorePallofPress` inserts one row into `ExerciseDefinitions`, and `ExerciseDefinitionSeeder.SeedAsync` returns early when `ExerciseDefinitions.AnyAsync()` is true. So on any fresh database the library contains exactly one exercise, Pallof Press, and every workout creation that names a real template fails. Production is unaffected only because its table was fully seeded before June. The same bug would bite a new developer machine, a disaster-recovery restore from an empty schema, and the Phase 7 onboarding seed. Fix in Phase 2: make the seeder upsert by name instead of guarding on "any row". The brief's "~930 tests" matches Domain plus Application plus a share of Vitest; the API suite has evidently not been run on a fresh database for some time.

## Appendix B: Terran CC and CooksAssistant research summary

See `docs/v2/plan.md` sections 3 and 10 for the decisions. Raw findings: Terran CC keeps habits in SQLite at `%APPDATA%\terran-cc\tcc.db`, tables `habits(id, name, icon, unit, type count|amount|check, target, step, order, archived)` and `habit_logs(habit_id, date, value)`; five habits (Walks 2, Water 3000 ml step 250, Protein 180 g step 20, Creatine + supplements check, Standing desk 4), 24 logged days between 17 Oct and 2 Nov 2026, streak rule "every habit hit its target". CooksAssistant's useful code is on branch `feature/import-to-bdtv-factory` (meal plans, shopping lists, a generator with trim-and-lowercase dedupe, ten cut-friendly seed recipes); `main` has a broken single-recipe list path, no CSV export, and a Clerk-keyed database whose live schema is uncertain.
