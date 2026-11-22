# 99-Strength

Strength training program tracker implementing Greg Nuckols' Average to Savage 2 (A2S2) periodization program. Users configure exercises with progression strategies, complete training days with AMRAP performance, and the system auto-adjusts weights/sets/reps based on performance.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, C# 13, EF Core 10, MediatR (CQRS), Serilog |
| Frontend | React 19, TypeScript 5.9, Vite 7.2, Tailwind CSS 4.1, ShadCN UI |
| State | TanStack Query (server state), React context (local state) |
| Auth | Clerk (JWT Bearer backend, `@clerk/clerk-react` frontend) |
| Database | PostgreSQL 16 (Testcontainers for tests) |
| Testing | xUnit 2.9, NSubstitute 5.3, FluentAssertions 8.8 (backend); Vitest (frontend) |

## Architecture

Clean Architecture with CQRS via MediatR. Dependencies point inward only.

```
A2S.Api (Presentation) → A2S.Application → A2S.Domain (no dependencies)
A2S.Infrastructure → A2S.Application → A2S.Domain
A2S.Integration.Hevy → A2S.Application (ACL for Hevy API)
```

| Project | Purpose |
|---------|---------|
| `A2S.Api` | Controllers, middleware, DTOs, startup config |
| `A2S.Application` | Commands, queries, validators, behaviors, DTOs |
| `A2S.Domain` | Aggregates, entities, value objects, events, enums, domain services |
| `A2S.Infrastructure` | EF Core, repositories, seed data, persistence configs |
| `A2S.Integration.Hevy` | Hevy API anti-corruption layer (Polly resilience) used by `SyncRoutineToHevy`. Most Hevy traffic from the frontend goes through `HevyProxyController` / `HevyDataController` (`/api/v1/hevy/*`), which attach the user's stored key server-side |
| `A2S.Web` | React frontend (separate — see `src/A2S.Web/AGENTS.md`) |

### Rules

- **Secrets never leave the server**: the Hevy API key is encrypted at rest with ASP.NET Core Data Protection (key ring in the `DataProtectionKeys` table). Clients only ever see `{ hasKey, lastFour }`. Never add the key to a response, a log line, a test fixture or `HttpContext.Items`.
- **Non-production endpoints** (simulation, debugging aids) are marked `[NonProductionOnly]` and removed from routing in Production.
- **Personal API tokens are deny-by-default**: a `nns_pat_…` bearer token (the desktop habit widget) only reaches endpoints marked `[AllowApiToken]`; everything else answers 403 (`ApiTokenScopeMiddleware`). Only the token's SHA-256 hash is stored and the plaintext is returned once, on create — never log it. Marking a new route means updating the pinned list in `ApiTokenAllowListTests`.

- **Handlers are thin orchestrators**: load aggregate → call domain methods → persist. No domain logic in Application layer.
- **Domain has zero external dependencies**: No framework references, no I/O.
- **Result pattern**: Commands/queries return `Result<T>` for error handling — no exceptions for business rules.
- **Aggregate factory methods**: Use `Workout.Create(...)`, `Exercise.CreateWithLinearProgression(...)`, etc. — never `new` directly.
- **Internal mutation**: Exercise mutation methods are `internal` — only callable within Domain assembly. Workout aggregate root orchestrates all state changes.

## Domain Model

### Aggregates

**Workout** (aggregate root) — owns all children:

```
Workout (AggregateRoot<WorkoutId>)
├── Exercise (Entity<ExerciseId>) — 1:N
│   └── ExerciseProgression (Entity<ExerciseProgressionId>) — 1:1, TPH
│       ├── LinearProgressionStrategy
│       ├── RepsPerSetStrategy
│       └── MinimalSetsStrategy
├── WorkoutActivity (VO) — completed day records
├── ProgressionAuditEntry (VO) — audit trail
└── BlockSequence (List<int>) — block ordering
```

**User** (aggregate root) — `Aggregates/User/User.cs`. Also holds `MaxHeartRate` and editable `HeartRateZonePercents` (WHOOP default Z1–Z5 = 50/60/70/80/90 % of max) exposed as the `HeartRateZones` VO.

**CardioTrack** (aggregate root) — `Aggregates/Cardio/CardioTrack.cs`. A cardio progression track:

```
CardioTrack (AggregateRoot<CardioTrackId>)
├── Stages: List<TrackStage> (VO) — each a WeekProgram or DoubleProgression with its own settings
├── State: CardioTrackState (VO) — stage index, week / volume / intensity, counters, pending suggestion
├── Sessions: List<CardioTrackSession> (VO) — every judged session, keyed for idempotency
├── Audit: List<CardioTrackAuditEntry> (VO) — action, reason, prescription before → after
├── Benchmarks: List<BenchmarkResult> (VO) — PR history; Benchmark: TrackBenchmark (every N weeks)
└── LastSnapshot: CardioTrackSnapshot (VO) — one-level undo of the last evaluation, decision or edit
```

Created from `CardioTrackTemplates` (walk-builder, walk-to-run, stair-finisher) as editable copies. Persisted as jsonb columns (`CardioTracks` table).

**CardioProtocol** (aggregate root) — fixed Norwegian 4×4 / Couch to 5K weeks / LISS; unchanged. `CouchToFiveKWeek(n)` exposes a week's intervals to templates.. Holds the Hevy API key only in protected form (`HevyApiKeyProtected`, via `ISecretProtector`); `RevealHevyApiKey` is the single way to read it and the value must never be logged, persisted elsewhere or returned by an endpoint.

**ExerciseDefinition** — reference data entity (exercise library, read-only)

### Strongly-Typed IDs

| ID | Type | Backing |
|----|------|---------|
| `WorkoutId` | `readonly record struct` | `Guid` |
| `ExerciseId` | `readonly record struct` | `Guid` |
| `ExerciseProgressionId` | `readonly record struct` | `Guid` |
| `ExerciseDefinitionId` | `readonly record struct` | `Guid` |
| `UserId` | `readonly record struct` | `string` (Clerk ID) |
| `CardioTrackId` | `readonly record struct` | `Guid` |

### Value Objects

| VO | Purpose |
|----|---------|
| `Weight` | Weight + unit (kg/lbs). Immutable. Arithmetic ops. |
| `TrainingMax` | TM value + unit. Calculates working weight from intensity %. |
| `TrainingMaxAdjustment` | TM change (percentage/absolute/none). |
| `RepRange` | Min/max rep range for RPS exercises (e.g., 8–12). |
| `PlannedSet` | Set number, weight, target reps, isAmrap flag. |
| `CompletedSet` | Actual reps performed per set. Delta calculation. |
| `ExercisePerformance` | Completed sets + metadata for a single exercise. |
| `ProgressionSnapshot` | Serializable progression state for undo capability. |
| `WorkoutActivity` | Completed day record with snapshots and performances. |
| `CardioPrescription` | What a track asks for today ("7 min continuous @ level 9", "C25K wk 4 day 2"); derived from state. |
| `CardioLog` | A logged cardio session: duration, HR, RPE, distance, zone 4–5 and zone 5 minutes, effort button, intensity setting (level / incline % / speed), floors, steps, max HR, pain flag + area, source (manual / Hevy / WHOOP). New fields are nullable. |
| `HeartRateZones` | Max HR + five zone floors as % of max. |

### Enums

| Enum | Values |
|------|--------|
| `ProgramVariant` | FourDay, FiveDay, SixDay |
| `ProgramTier` | Primary (T1), Auxiliary (T2) |
| `DayNumber` | Day1–Day6 |
| `WorkoutStatus` | NotStarted, Active, Paused, Completed |
| `ExerciseCategory` | MainLift, Auxiliary, Accessory |
| `EquipmentType` | Barbell, Dumbbell, Cable, Machine, Bodyweight |
| `WeightUnit` | Kilograms, Pounds |
| `CardioModality` | Walk, Run, StairClimber, Bike, Row, Other |
| `CardioTrackStatus` | Active, Paused, Completed |
| `EffortRating` | Easy (=RPE 4), Solid (=7), Brutal (=9) |
| `PainFlag` | None, Niggle, Stop |
| `TrackSuggestion` | None, RepeatWeek, DropBackWeek, NextStage |

## Ubiquitous Language

| Term | Meaning |
|------|---------|
| **Training Max (TM)** | Reference weight for calculating working weights. Not actual 1RM — typically ~90% of 1RM. Adjusted each session based on AMRAP delta. |
| **Working Weight** | Actual weight lifted = TM × intensity%. Rounded to nearest 2.5kg/5lbs. |
| **AMRAP** | As Many Reps As Possible. Last set of Linear exercises. Performance vs rep-out target drives TM adjustment. |
| **Rep Out / Rep-Out Target** | Target reps for the AMRAP set. Delta = actual reps − target. |
| **AMRAP Delta** | `actualReps - repOutTarget`. Drives TM adjustment via AmrapDeltaTable. |
| **Block** | 7-week training phase (6 working + 1 deload). Standard program = 3 blocks (21 weeks). |
| **Mini-Cycle (MC)** | 3-week sub-phase within a block. Each block has MC1 (weeks 1-3) and MC2 (weeks 4-6). |
| **Deload** | Recovery week (weeks 7, 14, 21). Reduced intensity (58%) and sets (4 vs 5). No AMRAP. |
| **T1 / Primary** | Main lifts (Squat, Bench, Deadlift, OHP). Reps: 5→1 across blocks. Intensity: 70%→96%. |
| **T2 / Auxiliary** | Secondary lifts (Front Squat, Incline Bench). Reps: 7→2. Intensity: 70%→92%. |
| **Block Sequence** | Ordered list of block numbers (default `[1,2,3]`). Configurable — e.g., `[1,1,2,3]` = 28 weeks. |
| **Progression** | Automatic weight/set/rep adjustment after each session based on performance. |
| **Substitution** | Replacing an exercise. Permanent = new exercise + optional progression change. Temporary = skip progression for one session. |
| **Unilateral** | Exercise performed one side at a time. Max 3 sets per side for RPS exercises. |
| **Cardio track** | A cardio progression (walk builder, walk → run, stair finisher) that prescribes each session and steps up after enough qualifying sessions. Walking and running are separate tracks. |
| **Prescription** | Today's session for a track, derived from its state. |
| **Qualifying session** | Completed as prescribed, no pain flag, and not Brutal (easy stages; avg HR at or under the zone ceiling when logged) or ≥ the zone 4–5 share of work time (hard stages; falls back to effort ≥ Solid without HR). |
| **Effort (Easy / Solid / Brutal)** | The one-tap rating for a cardio session, stored as RPE 4 / 7 / 9. An exact RPE is optional. |
| **Stage** | One part of a track with its own strategy; reaching its top suggests the next stage, which the person accepts. |
| **Finisher** | A short hard cardio block after lifting (stair finisher). Meant for upper-body days. |
| **Benchmark** | A periodic test on a track (default every 4 weeks), kept as PR history. Never changes progression. |

## Progression Strategies

### Linear (RTF) — `LinearProgressionStrategy`

For T1/T2 lifts. TM-based with percentage loading per the A2S2 program table.

- Last set is AMRAP; delta = actual reps − rep-out target
- TM adjustment via **AmrapDeltaTable**:
  - Delta ≥ +5 → TM +3%
  - Delta +3 to +4 → TM +2%
  - Delta +1 to +2 → TM +1%
  - Delta 0 → no change
  - Delta −1 → TM −2%
  - Delta ≤ −2 → TM −5%
- Working weight = TM × intensity% (rounded to 2.5kg/5lbs)
- 5 working sets (4 on deload), reps decrease across blocks

### Reps Per Set (RPS) — `RepsPerSetStrategy`

For accessories. All sets at same weight/reps targeting RepRange max.

- **SUCCESS** (all sets hit max reps) → add set (or increase weight + reset sets if at target)
- **MAINTAINED** (all sets ≥ min reps) → no change
- **FAILED** (any set < min reps) → remove set (or decrease weight if at min sets)
- Unilateral cap: max 3 sets per side
- Cable/Machine: weight changes require user confirmation (`PendingWeightConfirmation`)
- Starting weight can be deferred until after first session (`IsWeightPending`)

### Minimal Sets — `MinimalSetsStrategy`

For bodyweight/assisted exercises. Target total reps in fewest sets.

- **SUCCESS** (completed target reps in fewer sets than current) → reduce set count
- **MAINTAINED** (completed in exactly current sets) → no change
- **FAILED** (couldn't complete target reps) → add set
- Weight is user-controlled (not auto-adjusted)
- Bounded by MinimumSets/MaximumSets

### Cardio strategies — `Aggregates/Cardio/CardioStrategies.cs`

Lifting strategies above are untouched. Cardio tracks use their own (pure, unit-tested) strategies; all decisions append an audit entry, and every change is undoable once.

- **WeekProgramStrategy** (generalised C25K): a list of weeks. Sessions needed at a week = the sessions the plan schedules for the track per week (cardio slots + lift finishers pointing at it, via `CardioWeekContext`), never fewer than `SessionsToAdvance` (walk → run: 2 a week, still 3 to advance); `SessionsToAdvance` alone without a plan slot. A planned session missed on the calendar since the week started (paused days excluded) counts against the week. After the needed sessions: all qualified → next week (last week → stage complete); otherwise suggest **repeat week**. Two niggles in a week or any stop → suggest **drop back a week**. The person accepts or declines (decline of repeat = advance anyway).
- **DoubleProgressionStrategy**: volume (minutes or interval rounds) from min to max by a step (or a %, rounded up); at the top, intensity (level / incline / speed) +1 step and volume resets. One step per N qualifying sessions (default 2). Non-qualifying completed sessions hold; two missed prescriptions in a row step back once. Top of volume and intensity → stage complete.
- **StageStrategy** (track level): the ordered stages, each with its own strategy and exit condition (`StageExit.AtTop` = done at the top of its range, `Never` = hold). Reaching an `AtTop` exit with a stage after it raises a **NextStage** suggestion; `MoveToNext` runs only when the person accepts. `CardioTrack` delegates prescription, evaluation and edits (`Fit`) to it. While a suggestion is pending, or the track is paused, sessions are recorded but not evaluated.
- **Finishers**: `SessionFinisher` on a lift slot points at exactly one cardio protocol or cardio track. `FinishGymSession` records a track finisher as a cardio completion with `CardioTrackId` and evaluates the track once (completion key); level, floors, effort and pain go on the `CardioLog`. The Hevy pull completes a lift's finisher slot (or a same-day track slot) from the built-in cardio exercise in the workout (`CompleteSession` with `PostToHevy: false`).
- **Session posts** (`SessionHevyPoster`): cardio and mobility completions recorded in the app, and track sessions logged on the Tracks page, become a `HevyWorkoutPost` (client session id = completion id, or the phone's UUID) with the gym-mode retry/adopt; payload `Cardio` (Walking 33EDD7DB / Treadmill 243710DE / Running AC1BB830 / Stair Machine (Floors) 4377A52B) or `Routine` (Stretching 527DA061 / Yoga 8C9D2928 — Yoga when the routine's kind is `YogaFlow` or `FollowAlong`; duration = the completion's `DurationMinutes`, else the routine's target).
- **User preference** `PushRoutinesToHevy` (default true, `GET|PUT /api/v1/users/me/preferences`) gates `HevyRoutinePusher`; finished sessions are posted regardless.
- Sessions are idempotent on a key: `completion:{id}` (plan session completion), `hevy:{workoutId}:{exerciseIndex}` (Hevy pull), `client:{key}` (log sheet), `manual:{guid}`.

## Coach signals (suggestions only)

None of these change A2S2, RPS, minimal-sets or cardio-strategy maths.

- **Readiness** (`Aggregates/Readiness/ReadinessEntry.cs`, table `ReadinessEntries`, one per user + local date): Green / Yellow / Red. `ReadinessAdvice.For`: red skips the lift slot's finisher by default (`KeepFinisher` is the one-tap undo) and suggests skipping hard cardio; yellow is a note. API `GET /api/v1/readiness?date=` (**token-readable**), `PUT /readiness/{date}`, `PUT /readiness/{date}/finisher`. Gym mode hides the finisher while `finisherSkipped`.
- **Start gates** (`Aggregates/Cardio/StartGate.cs`): `GateCondition` (`TrendWeightAtOrBelow`, `TrackAtOrAbove(template, minutes, intensity, effort, weeks)`, `NoPainInAreas(areas, weeks)`), one spec class each over `GateInputs`. A gated track has status `Gated` (sessions recorded, not evaluated) until the person accepts Start (`StartFromGate`). `StartGate.RunningGate()` is Walk → run's default (trend ≤ 90 kg; walk builder ≥ 45 min at ≥ 8 %, Easy, 2 weeks; no knee/shin/Achilles pain 4 weeks), applied by `withGate` on create or `PUT /cardio-tracks/{id}/gate {useDefault:true}`. `CardioGateEvaluator` (Application) gathers the trend EWMA, tracks and pain flags (track sessions + plan completions) for the list/today queries; `POST /cardio-tracks/{id}/start`.
- **Phase changes**: `PlanPhase` has `Label`, `Changes` (≤ 10 lines) and `Swaps` (`PhaseSwap(From, To)`), stored in the Phases jsonb. `PlanPhase.HeadsUpOn` = the 7 days before a phase through its first day. `GET /plans/phase-heads-up` resolves each swap to the linked workout's exercise; Today's Swap runs the existing permanent `SubstituteExercise` after the person confirms. `PUT /plans/{id}/phases/{from}/changes` edits them. Push kind `PhaseChange` goes out once per phase (08:00–21:00 inside the window), logged in `PushNudgeLogs` against the phase start date.

## A2S2 Program Table

21 weeks = 3 blocks × 7 weeks. Defined in `A2SHypertrophyProgram`.

- T1 reps: 5→1 (floor=1). T2 reps: 7→2 (floor=2).
- Intensity increases as reps decrease.
- Working sets: 5 (deload: 4 sets, 5 reps, 58% intensity, no AMRAP).
- Rep-out targets: MC1 = reps×2, MC2 = reps×2−1 (except B3 MC2 = reps×2).

## Domain Events

| Event | Raised By | Payload |
|-------|-----------|---------|
| `WorkoutCreated` | `Workout.Create()` | WorkoutId, Name, Variant, ExerciseCount |
| `WorkoutStarted` | `Workout.Start()` | WorkoutId |
| `DayCompleted` | `Workout.CompleteDay()` | WorkoutId, Day, WeekNumber, ExerciseCount |
| `WeekProgressed` | `Workout.ProgressToNextWeek()` | WorkoutId, NewWeek, NewBlock, IsDeload |
| `WorkoutCompleted` | `Workout.ProgressToNextWeek()` | WorkoutId (when all weeks done) |
| `TrainingMaxAdjusted` | `LinearProgressionStrategy.ApplyPerformanceResult()` | ProgressionId, NewTM, Adjustment, AmrapDelta |
| `CompletionUndone` | `Workout.UndoLastCompletion()` | WorkoutId, RestoredWeek, RestoredDay |
| `ProgressionSkipped` | `Exercise.ApplyProgression()` (temp sub) | ExerciseId, Reason |
| `ProgramRestarted` | `Workout.UpdateBlockSequence()` | WorkoutId, NewBlockSequence |

Cardio API: `/api/v1/cardio-tracks` (`GET`, `POST`, `GET /templates`, `GET /today`, `GET|PUT /heart-rate-zones`, `GET|PUT /{id}`, `POST /{id}/sessions|suggestion|undo|pause|resume|benchmarks`). Schedule guardrails (`PlanScheduleRules`, warnings only): 4×4 or any hard track the day before a Lower day, a hard track on a Lower day (finisher), weekly zone 4–5 minutes above `HighIntensityTarget.Max`. Hevy: built-in templates `Stair Machine (Floors)` 4377A52B, `Walking` 33EDD7DB, `Treadmill` 243710DE, `Running` AC1BB830 (`HevyTemplateCatalogue`); push appends a same-day stair track to the lift routine; pull logs those exercises against the matching track (`HevyCardioTrackLogger`).

Events are dispatched post-save via `MediatRDomainEventDispatcher`. Currently no consumers — extensibility point for future cross-aggregate concerns.

## CQRS Commands & Queries

### Commands

| Command | Handler | Purpose |
|---------|---------|---------|
| `CreateWorkout` | Creates workout with exercises from template | |
| `DeleteWorkout` | Hard-deletes the workout and its children (no soft delete) | |
| `SetActiveWorkout` | Sets workout as active (pauses others) | |
| `CompleteDay` | Records exercise performances, applies progression | |
| `ProgressWeek` | Advances to next week/block | |
| `UndoCompletion` | Restores pre-completion state from snapshots | |
| `SubstituteExercise` | Replaces exercise (permanent or temporary) | |
| `UpdateExercises` | Batch update exercise properties (TM, weight, etc.) | |
| `RemoveExercise` | Removes exercise from workout | |
| `UpdateBlockSequence` | Changes block ordering (can restart completed program) | |
| `UpdateWorkingWeight` | Direct weight override | |
| `ConfirmStartingWeight` | Confirms deferred starting weight for RPS | |
| `ConfirmWorkingWeight` | Confirms Cable/Machine weight after progression | |
| `RetrofixLinearTm` | Recalculates TM history from AMRAP data | |
| `SyncRoutineToHevy` | Pushes routine to Hevy via ACL; the key is resolved server-side via `IHevyKeyResolver` | |
| `CreateUser` | Auto-provisions user from Clerk auth | |
| `CreateCardioTrack` | Starts a track from a template (editable copy) | |
| `UpdateCardioTrack` | Edits name, notes, stages and benchmark; keeps position inside the new ranges | |
| `LogCardioTrackSession` | Logs and evaluates a session (idempotent on client key); ticks the Walks habit for walks | |
| `DecideCardioTrackSuggestion` | Accepts or declines repeat / drop back / next stage | |
| `UndoCardioTrack` | Restores the exact state before the last evaluation, decision or edit | |
| `SetCardioTrackPaused` | Pause / resume | |
| `RecordCardioBenchmark` | Adds a benchmark result (PR history) | |
| `SetHeartRateZones` | Max HR and zone boundaries on the user | |
| `CompleteSession` (cardio slot with a track) | Also evaluates the track once per completion | |
| `ApplyPlanTemplate` | Applies any `IPlanTemplate` (`SuggestedPlans/`, found by `PlanTemplates`) through `PlanTemplateApplier`: start date snaps to the Monday on/after it (Europe/London, never past), optional training maxes left empty start **pending**, routines/tracks reused by name/template key, everything is the user's own editable data. Idempotent while that template is active; another active plan → 409 until `confirmReplace` (old plan made inactive, kept with history; old workout paused) | `POST /api/v1/plans/templates/{key}/apply` |
| `SetTrainingMax` | "Set your training max" for an A2S2 exercise: the TM, or a recent set → Epley e1RM × 90 % rounded to 2.5 kg/5 lb (`TrainingMaxEstimator`). Clears a pending TM; same manual-TM path as UpdateExercises (no TM maths change) | `POST /api/v1/workouts/{id}/exercises/{exerciseId}/training-max` |
| `StartSuggestedPlan` | One-tap onboarding: builds "Cut to 85 — Nov 2026" from `SuggestedPlans/CutTo85Nov2026` (workout + cardio protocols + Mobility A/B + habits + linked plan) in one unit of work; idempotent while that plan is active | `POST /api/v1/plans/suggested/cut-to-85` |
| Mobility: `CreateMobilityRoutine`, `UpdateMobilityRoutine`, `ReplaceDrills`, `DeleteMobilityRoutine` | Mobility routines and their ordered drills. Update renames and changes target minutes; delete returns 409 while any of the user's plans schedules the routine (past completions keep the id as history) | `POST /api/v1/mobility-routines`, `PUT /{id}`, `PUT /{id}/drills`, `DELETE /{id}` |
| Mobility progression: `RecordDrillFeedback`, `RespondToDrillSuggestion`, `UndoDrillProgression`, `ApplyHoldIncrease` | Routine kind (`WarmUp`/`Corrective`/`Stretch`/`YogaFlow`/`FollowAlong`, follow-along = link + minutes, no drills). Drills may carry `Levels` (name, description, sets, dose, optional `DoseRange`, cues) with `CurrentLevel`/`CleanStreak`/`SuggestedLevel`. `DrillProgressionRule`: 2 clean sessions → +step in range, at top → suggest next level (user accepts/declines); strain/stop → step back once. Feedback is idempotent on the session key; every change is in `ProgressionLog` with a reason; undo reverts the last change (all drills it touched). `ApplyHoldIncrease` is the phase bump (+N s to timed holds, ranges shift) | `POST /{id}/feedback`, `/{id}/suggestion`, `/{id}/progression/undo`, `/{id}/hold-increase` |
| `UpdateSchedule` | Replaces the plan's weekly template; returns `PlanScheduleRules` warnings (4×4 the day before Lower A/B) alongside the plan. A lift slot may carry `WarmUpRoutineId` and `FinisherCardioProtocolId` | `PUT /api/v1/plans/{id}/schedule` |
| `FinishGymSession` | Gym mode finish: stages the plan's lift completion (and the finisher's cardio completion) and a `HevyWorkoutPost`, then runs `CompleteDay` unchanged, whose save commits it all; then tries the Hevy post once. Idempotent on `ClientSessionId` (a replay returns the stored result, `replayed: true`) | `POST /api/v1/gym-sessions` |
| `RetryHevyPost` | "Not in Hevy yet" → Retry: makes a gym session's post due now and tries it | `POST /api/v1/gym-sessions/{clientSessionId}/hevy-retry` |
| Habits: `CreateHabit`, `UpdateHabit`, `ArchiveHabit`, `UpsertHabitLog`, `IncrementHabit`, `ImportHabits` | Habit definitions and per-local-date logs (last write wins per habit/day) | `/api/v1/habits` |
| `RecordBodyWeight` | Morning weigh-in; mirrored to Hevy | `/api/v1/body-weight` |
| Cardio: `CreateCardioProtocol`, `AdvanceCouchToFiveK`, `SetRepeatWeek` | Cardio protocols and C25K week moves | `/api/v1/cardio-protocols` |
| Plans: `CreateTrainingPlan`, `AddPhase`/`RemovePhase`, `AddCheckpoint`/`RemoveCheckpoint`, `LinkWorkout`, `CompleteSession`/`UndoSessionCompletion`, `UseAllowance`/`RemoveAllowanceUse`, `SetHighIntensityTarget` | The training plan around the A2S2 workout | `/api/v1/plans` |
| Hevy: `PushSessionRoutine`, `PushWeekRoutines`, `PullHevyEvents`, `AssignHevyWorkout`, `IgnoreHevyWorkout` | Server-side push of sessions as routines; pull, match and complete workouts; unmatched inbox | `/api/v1/plans/{id}/sessions/push*`, `/api/v1/hevy/*` |
| Push: `SubscribePush`, `UnsubscribePush` | Web Push subscriptions for the nudge service | `/api/v1/push` |
| Meals: `CreateMeal`, `UpdateMeal`, `DeleteMeal`, `SetWeekMeals`, `GenerateShoppingList`, `ToggleShoppingItem` | Meals and the weekly shopping list | `/api/v1/meals`, `/api/v1/shopping-list` |
| API tokens: `CreateApiToken`, `RevokeApiToken` | Personal tokens for the desktop habit widget | `/api/v1/users/me/api-tokens` |

### Queries

| Query | Purpose |
|-------|---------|
| `GetAllWorkouts` | List user's workouts (summary) |
| `GetWorkout` | Full workout with exercises + progressions |
| `GetWeekPlan` | Planned sets for current week |
| `GetExerciseLibrary` | Paginated exercise definitions (search, filter) |
| `GetExerciseHistory` | Historical performance for an exercise |
| `GetWorkoutHistory` | Completed day history for a workout |
| `GetCurrentUser` / `GetUser` | User profile |
| Plans: `GetToday`, `GetPlanCalendar`, `GetWeeklyGoals`, `GetDayBudget` | Today screen, month calendar, weekly rings, hard sets and time estimate |
| `GetUpcomingDays` (`GET /plans/upcoming?from=&days=`, 1–31, default 14) | The next days' sessions, each lift with its planned sets from the current A2S2 week and its finisher; Today's "Up next" |

Personal API tokens (`[AllowApiToken]`) may read `GET /plans/today?date=` and `GET /plans/upcoming`; no other plans or workouts route and no write. The list is pinned by `ApiTokenAllowListTests`.
| Habits, body weight, Hevy inbox, meals, push and token queries | Read sides of the commands above |
| `GetToday` | A plan day's sessions, phase and week goals. Lift sessions carry the slot's warm-up and finisher and, when finished in gym mode, `gymSessionId` + `hevyPostStatus` (Pending/Posted/Failed/Skipped) — `GET /api/v1/plans/today` |
| `ListCardioTracks` / `GetCardioTrack` | Tracks with prescriptions; one track with sessions, audit, benchmarks and chart points |
| `ListCardioTrackTemplates` | The shipped templates |
| `GetTodayCardio` | Active tracks' prescriptions, this week's zone 4–5 minutes and guardrail warnings |
| `GetHeartRateZones` | Max HR and zones |
| `GetPlanTemplates` | Every template previewed from a start date: phases with dates, schedule slots, sessions with 2a/2b slots and prescriptions, warm-ups, home routines, cardio tracks, `toFillIn` — `GET /api/v1/plans/templates[?startDate=]`, `GET /api/v1/plans/templates/{key}` |
| `GetSuggestedPlans` | Previews every plan template (Cut to 85, Plan v3) for today (start Monday, week, lifting-day time estimates, inputs needed, already started) — `GET /api/v1/plans/suggested` |

## Gym mode

Brief § 3a. At the gym the person opens `/gym/:day` (Today's Start): one checklist of warm-up drills
(the slot's `WarmUpRoutineId`, else the routine named "Mobility A"), each lift's planned sets
pre-filled (tap to confirm, pencil to edit, the AMRAP set asks for reps), the attached finisher
(minutes, level, floors, Easy/Solid/Brutal → RPE 4/7/9), then Finish or Finish early.

- **Phone first.** Every tap is saved to localStorage (`a2s-gym-session:{workout}:{week}:{day}`); the
  finish goes through the offline queue as a `gym.finish` op carrying the client session id (a v4 UUID
  made when the session starts), so replays are idempotent. Screen Wake Lock is held; the rest timer
  starts after each confirmed set from `PlannedExerciseDto.RestSeconds` (`RestDefaults`: main lifts 180 s,
  others 90 s) and beeps at zero. The old `/workout/session/:day` page still works.
- **Server.** `FinishGymSession` completes the A2S2 day through the unchanged `CompleteDay` path (exercises
  left out keep their progression), records the plan completions and a `HevyWorkoutPost` (unique per user +
  client session id). `HevyWorkoutPoster` posts `POST /v1/workouts` with real start/end times: ticked drills
  as `warmup` sets, lifts as `normal` with the AMRAP set `failure`, the finisher as Stair Machine (Floors)
  `4377A52B` (`custom_metric` = floors) when it is a stair finisher or floors were logged, else the cardio
  kind's template. On success the Hevy id goes on the `SessionCompletion` (so the 15-minute pull dedupes)
  and a `HevyLink` kind `Workout`, key `gym:{clientSessionId}`.
- **Never two Hevy workouts.** A failed post is retried by `HevyPostRetryHostedService` (every 3 min;
  per-post backoff 2, 4, 8 … min, max 6 h, gives up after 8 attempts or a permanent error). Once an attempt
  may have reached Hevy, the poster first lists recent Hevy workouts and adopts one with the same title and
  a start within a minute. The pull does the same for a pending post before matching, so a pull that sees
  the workout before the post recorded its id never completes the session twice.
- The browser never writes workouts to Hevy: the v1 Send to Hevy on the completion summary is gone and the
  `/hevy/workouts` proxy refuses POST (GET stays for Pull from Hevy).
- Settings → Gym sessions sets each lift slot's warm-up and finisher (protocol, track, or a fixed cool-down).
- **Supersets.** `Exercise.SupersetGroup` (per day, max two exercises; set by plan specs, editable via
  UpdateExercises) feeds `PlannedExerciseDto.supersetGroup`, gym mode (A1 → B1 → A2 …, no rest between A
  and B, `RestDefaults.SupersetSeconds` 60 s after B) and `RoutineBuilder` superset ids. Older specs'
  `SupersetSpec` lists are turned into groups when a plan is applied.
- **Unilateral work is left side first**: gym mode lists Left then Right; Hevy routine/workout notes say "Left side first".
- **Extra set** (`Exercise.ExtraSetSide` None/Left/Right, unilateral reps-per-set only): sent as
  `ExercisePerformanceRequest.ExtraSet`, outside `CompletedSets`, so RPS evaluation never sees it (tested
  identical with/without); Hevy gets it as one more set with the note "Extra set: left". Plan v3 doesn't use it.
- **Fixed cool-down finisher** (`SessionFinisher.Fixed` = `FixedActivity` Walk/Treadmill, 1–60 min, notes):
  no LISS rule, no track. Gym mode ticks it; the finish logs a plain cardio completion; Hevy gets Treadmill
  `243710DE` (or Walking) with the duration.
- **Pending training max** (`LinearProgressionStrategy.IsTrainingMaxPending`): planned sets keep A2S2 sets,
  reps and AMRAP but weigh 0 (`metadata.isTrainingMaxPending`, `intensityPercentage`, `tier`); completing
  the exercise is refused ("Set your training max for … before finishing it.") until a TM is set.
- **Two devices.** `FinishGymSession` refuses (`ErrorCode.Conflict`, 409, "This day was already finished on
  another device") when the workout's current week already has the day recorded under a different client
  session id; nothing is staged and progression is untouched. The same client session id still replays.
  Drafts are versioned (`GymSessionState.version`, `migrateGymSession`) and drafts older than 7 days or for
  earlier weeks are removed (`expireGymDrafts`, on app start and when gym mode loads).

## Offline writes and idempotency

Queued writes (habit increment/set, body weight, plan completion, gym finish, cardio track session) carry an
`Idempotency-Key` header: the offline op id, sent on the first attempt and every replay. `[Idempotent]`
(`A2S.Api/Infrastructure/IdempotentAttribute.cs`, an MVC filter, not a MediatR behaviour, because the key and
the replayed answer are HTTP concerns) stores the first 2xx status + body per user + key in
`IdempotencyRecords` (`IIdempotencyStore`, 7 days) and replays it without running the action; the same key
with a different route/arguments answers 422. Failures are not stored. Requests without the header run as
before. Put `[Idempotent]` on any new endpoint the offline queue calls. See docs/development.md
"Offline and saving".

## Testing

| Project | Framework | Type |
|---------|-----------|------|
| `A2S.Domain.Tests` | xUnit + FluentAssertions | Unit tests |
| `A2S.Application.Tests` | xUnit + NSubstitute + FluentAssertions | Unit tests (mocked repos) |
| `A2S.Infrastructure.Tests` | xUnit + Testcontainers + FluentAssertions | Integration tests (real PostgreSQL) |
| `A2S.Api.Tests` | xUnit + TestWebApplicationFactory + Testcontainers | Integration tests (real HTTP + DB) |

### Conventions

- **No AAA comments** — code structure makes Arrange/Act/Assert obvious
- **Deterministic values only** — no `Guid.NewGuid()` or `DateTime.UtcNow` in assertions; use builder-provided values
- **Builders for domain objects** — use `WorkoutBuilder` and `ExerciseBuilder` from `A2S.Tests.Shared/Builders/`
- **Test organization**: API tests in `Unit/` and `Integration/` folders; Infrastructure tests by feature (`Persistence/`, `Repositories/`, `SeedData/`)
- **FluentAssertions** for all assertions — no raw `Assert.*`
- **NSubstitute** for mocking — no other mocking framework
- **Real DB** for infrastructure/API tests via Testcontainers

## Build & Run

```bash
# Full stack (PostgreSQL + API + frontend)
npm start

# Individual services
npm run start:db          # PostgreSQL via docker-compose
npm run start:api         # dotnet run (ports 5123/7179)
npm run start:web         # Vite dev server (port 5173)

# Build
dotnet build              # Backend
cd src/A2S.Web && npm run build  # Frontend

# Test
dotnet test               # All backend tests
cd src/A2S.Web && npm test      # Frontend tests

# Storybook
cd src/A2S.Web && npm run storybook  # Port 6006
```

## Docker & Deployment

### Docker Compose

| Service | Image | Port | Notes |
|---------|-------|------|-------|
| `api` | Multi-stage Dockerfile (.NET 10) | 8080 | Depends on `db` healthy |
| `db` | `postgres:16-alpine` | 5432 | Health-checked, `pgdata` volume |

```bash
docker-compose up -d      # Start PostgreSQL + API
docker-compose down        # Stop all
```

### Dockerfile

Two-stage build:
1. **Build**: `mcr.microsoft.com/dotnet/sdk:10.0` — restore, publish
2. **Runtime**: `mcr.microsoft.com/dotnet/aspnet:10.0` — port 8080, health check at `/health`

### Environment Variables

| Variable | Default (dev) | Purpose |
|----------|--------------|---------|
| `Database__ConnectionString` | `Host=localhost;...Database=a2s_dev;...` | PostgreSQL connection |
| `Clerk__Domain` | `https://cosmic-treefrog-30.clerk.accounts.dev` | Clerk auth domain |
| `Cors__Origins__0` | `http://localhost:5173` | Allowed CORS origins |
| `VITE_API_BASE_URL` | `https://localhost:5001/api/v1` | Frontend API base URL |
| `VITE_CLERK_PUBLISHABLE_KEY` | (from Clerk dashboard) | Frontend Clerk auth key |

### Ports

| Service | Port | Context |
|---------|------|---------|
| API (Docker) | 8080 | Container |
| API (dev HTTP) | 5123 | `dotnet run` |
| API (dev HTTPS) | 7179 | `dotnet run` |
| Frontend (Vite) | 5173 | `npm run dev` |
| PostgreSQL | 5432 | Both |
| Storybook | 6006 | `npm run storybook` |

### Startup Sequence

1. `npm run start:db` — PostgreSQL via docker-compose
2. `npm run start:api` — .NET API. It does **not** migrate on startup: it refuses to boot with pending migrations. Apply them with `dotnet ef database update` locally (the Docker image runs an EF migration bundle in `docker-entrypoint.sh`). On boot it upserts the exercise library by name and encrypts any legacy plaintext Hevy keys, erasing the plaintext.
3. `npm run start:web` — Vite dev server

Or `npm start` for all three concurrently.
