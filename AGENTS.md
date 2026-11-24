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
│       ├── MinimalSetsStrategy
│       └── CardioStageStrategy — cardio as an exercise (stair climber / incline walk); no sets
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
| `ExercisePerformance` | Completed sets + metadata for a single exercise; a cardio exercise carries a `CardioResult` instead of sets. |
| `CardioResult` | How a cardio exercise went: finished or stopped early, effort (Easy/Solid/Brutal), pain flag and area, total seconds, work minutes, level/incline, speed, floors. `IsMiss` = pain or stopped early. |
| `PlannedCardioBlock` | Today's cardio exercise session, derived from its lane state: lane, stage, warm-up / work (continuous or interval rounds) / cool-down, level or incline, speed, `IsSettingsPending`, total minutes and `Text` ("3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)"). Replaces planned sets for a cardio exercise. |
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
| `ExerciseCategory` | MainLift, Auxiliary, Accessory, Cardio (= 4) |
| `AuditEntryType` | … plus `CardioSettingsChanged` (a level/incline/speed edit on a cardio exercise: block text before → after) |
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

### Cardio stage — `CardioStageStrategy` (`Aggregates/Workout/`)

The fourth exercise strategy: cardio as an exercise on a lift day (Plan v3), last on its day, category `Cardio`, equipment `Machine`, no sets (`CalculatePlannedSets` is empty; the session is a `PlannedCardioBlock`). Library names `Stair Machine (Floors)` (Hevy `4377A52B`, **Hard** lane) and `Treadmill` (Hevy `243710DE`, **Easy** lane). Lifting and cardio-track strategies are untouched.

- **Hard lane (stair climber)**: warm-up 3 min at level 3, cool-down 2 min at level 3. Stage 1 continuous 6 → 12 min at level 6, +1 min. Stage 2 intervals 4 × 2/2 → 4 × 3/3 → 4 × 4/3 (hard/easy min, easy at level 3; recoveries only between rounds, so sessions run 11 → 17, 19, 26, 30 min); the hard level is asked on entry, suggestion = continuous level + 2. Stage 3 4 × 4/3 with hard level +1 per step (cap 20).
- **Easy lane (incline walk)**: no warm-up or cool-down. Stage 1 10 → 30 min, +2 min, at the starting incline (asked, 5 % suggested). Stage 2 30 min, incline +1 % to 12 %. Stage 3 30 min, speed +0.3 km/h and incline back to 8 %, then +1 % to 12 % again, repeating. Speed is asked on the first session (no cap).
- **Hard rule**: Easy → step up now; Solid → bank one, 2 banked → step up; Brutal → hold (banked Solids kept); pain or stopped early → hold, 2 in a row → step back one. **Easy rule**: Easy → step up; Solid → hold; Brutal → step back one; pain/stopped early → hold, 2 in a row → step back one.
- Stage changes are automatic (no accept prompt). "Step back one" returns to the exact previous prescription, across a stage boundary if needed.
- **Starting values are blank** until gym mode asks (`IsSettingsPending`); while blank, sessions change nothing. They can be edited later; every edit is a `CardioSettingsChanged` audit entry ("progression history").
- **Lane sharing**: a lane's exercises on different days share one progression. Each day keeps its own `Exercise`; `Workout` copies the lane's state to the other exercises with the same `CardioLane` after a completion, a settings edit and an undo, so the lane climbs once per session across both days.
- **Red readiness** skips the Hard lane in gym mode by default ("Do it anyway" keeps it, also offline); the Easy lane always shows. **Hevy pull** completes a cardio exercise only when the Hevy set has an RPE (→ effort); otherwise it is left out and its progression is unchanged.
- **CardioExerciseBackfill** (`Infrastructure/SeedData`, startup after `MuscleGroupBackfill`, idempotent: a workout that already has a cardio exercise is left alone): matches a running Plan v3 ("Falador Fat Furnace" and its former names) by name, inserts the four cardio exercises, seeds the stair lane from the old stair track's last session (its work minutes, one step up if it would step up, clamped 6–12 min, level 6), retro-logs this week's stair / fixed cool-down finisher sessions into history (no progression applied), marks that day's Hevy post `UpdatePending`, clears those finishers and pauses the old stair track (history kept). One save per plan; never stops startup.

### Cardio strategies — `Aggregates/Cardio/CardioStrategies.cs`

Lifting strategies above are untouched. Cardio tracks use their own (pure, unit-tested) strategies; all decisions append an audit entry, and every change is undoable once.

- **WeekProgramStrategy** (generalised C25K): a list of weeks. Sessions needed at a week = the sessions the plan schedules for the track per week (cardio slots + lift finishers pointing at it, via `CardioWeekContext`), never fewer than `SessionsToAdvance` (walk → run: 2 a week, still 3 to advance); `SessionsToAdvance` alone without a plan slot. A planned session missed on the calendar since the week started (paused days excluded) counts against the week. After the needed sessions: all qualified → next week (last week → stage complete); otherwise suggest **repeat week**. Two niggles in a week or any stop → suggest **drop back a week**. The person accepts or declines (decline of repeat = advance anyway).
- **DoubleProgressionStrategy**: volume (minutes or interval rounds) from min to max by a step (or a %, rounded up); at the top, intensity (level / incline / speed) +1 step and volume resets to `VolumeResetTo` (null = the range minimum, the original rule; must sit inside the range). One step per N qualifying sessions (default 2). Non-qualifying completed sessions hold; two missed prescriptions in a row step back once. Top of volume and intensity → stage complete.
- **StageStrategy** (track level): the ordered stages, each with its own strategy and exit condition (`StageExit.AtTop` = done at the top of its range, `Never` = hold). Reaching an `AtTop` exit with a stage after it raises a **NextStage** suggestion; `MoveToNext` runs only when the person accepts. `CardioTrack` delegates prescription, evaluation and edits (`Fit`) to it. While a suggestion is pending, or the track is paused, sessions are recorded but not evaluated.
- **Warm-up / cool-down segments**: a stage may carry `WarmUp` / `CoolDown` (`TrackSegment`: 1–30 min, easy level, zone note; null = none). They never progress and are never judged. `StageStrategy.Prescribe` wraps the work prescription: `Minutes` becomes the whole session, `WorkMinutes` stays the work block (qualification, including "zone 4–5 ≥ 50 %", uses it), `Text` is "3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)" (`Title` is still the work alone). Logs carry `WorkMinutes` beside the total `DurationSeconds`. Stair finisher defaults: warm-up 3 min L3 Z2, continuous 5 → 15 min @ L6 (+1 min per 2 qualifying, at 15 level +1 and back to 10), intervals 6 → 10 rounds (back to 8 after a hard-level step), cool-down 3 min L3 Z1–2. Migration `BackfillStairFinisherSegments` moved existing stair finishers onto these, keeping position.
- **Finishers**: `SessionFinisher` on a lift slot points at exactly one cardio protocol or cardio track. `FinishGymSession` records a track finisher as a cardio completion with `CardioTrackId` and evaluates the track once (completion key); level, floors, effort and pain go on the `CardioLog`. The Hevy pull completes a lift's finisher slot (or a same-day track slot) from the built-in cardio exercise in the workout (`CompleteSession` with `PostToHevy: false`).
- **Live finisher wizard** (gym mode, stair/track finisher only; walks, runs and standalone cardio don't get it): "Start finisher" on the card opens a full-screen guide. Frontend `features/gym/finisher/`: `finisherPlan.ts` (pure: prescription → phases `{kind: warmup|work|hard|easy|cooldown, seconds, level, zone, label}`, 1:1 intervals expanded into hard/easy rounds), `finisherEngine.ts` (pure wall-clock engine: instants + time done per phase; current phase always derived from `Date.now()`, so backgrounding/suspension/reload are correct; localStorage key `a2s.finisher.{clientSessionId}`, cleared on finish/abandon), `useFinisherEngine.ts` (ticks, re-derives on visibilitychange/focus/pageshow), `FinisherWizard.tsx`. Finish prefills the log (total, work minutes, level; effort and floors left). Phase-change push: the phone posts the whole schedule (absolute UTC times + text) to `POST /api/v1/finisher-sessions/{clientSessionId}/schedule` on Start and every pause/resume/skip/back/±30 s (replace; empty while paused) and `DELETE`s it on finish/abandon. Server: `FinisherSchedule` aggregate (`Aggregates/Push`, table `FinisherSchedules`, nudges as jsonb, unique per user + session; `TakeDue` marks due nudges sent before the save, sends only the latest fresh one, drops nudges > 1 min late; exhausted schedules are deleted), `FinisherNudgeService` + `FinisherNudgeHostedService` (every 5 s, idle in Testing / without VAPID keys) over the existing `IPushSender`; title "Stair finisher", tag `finisher-{id}`, deep link `/gym/{day}?finisher=1` (reopens the wizard). No subscription: nothing is sent. Migration `AddFinisherSchedules` (Down tested).
- **Session posts** (`SessionHevyPoster`): cardio and mobility completions recorded in the app, and track sessions logged on the Tracks page, become a `HevyWorkoutPost` (client session id = completion id, or the phone's UUID) with the gym-mode retry/adopt; payload `Cardio` (Walking 33EDD7DB / Treadmill 243710DE / Running AC1BB830 / Stair Machine (Floors) 4377A52B) or `Routine` (Stretching 527DA061 / Yoga 8C9D2928 — Yoga when the routine's kind is `YogaFlow` or `FollowAlong`; duration = the completion's `DurationMinutes`, else the routine's target).
- **User preference** `PushRoutinesToHevy` (default true, `GET|PUT /api/v1/users/me/preferences`) gates `HevyRoutinePusher`; finished sessions are posted regardless.
- Sessions are idempotent on a key: `completion:{id}` (plan session completion), `hevy:{workoutId}:{exerciseIndex}` (Hevy pull), `client:{key}` (log sheet), `manual:{guid}`.

## Coach signals (suggestions only)

None of these change A2S2, RPS, minimal-sets or cardio-strategy maths.

- **Readiness** (`Aggregates/Readiness/ReadinessEntry.cs`, table `ReadinessEntries`, one per user + local date): Green / Yellow / Red. `ReadinessAdvice.For`: red skips the lift slot's finisher by default (`KeepFinisher` is the one-tap undo) and suggests skipping hard cardio; yellow is a note. API `GET /api/v1/readiness?date=` (**token-readable**), `PUT /readiness/{date}`, `PUT /readiness/{date}/finisher`. Gym mode hides the finisher while `finisherSkipped`.
- **Start gates** (`Aggregates/Cardio/StartGate.cs`): `GateCondition` (`TrendWeightAtOrBelow`, `TrackAtOrAbove(template, minutes, intensity, effort, weeks)`, `NoPainInAreas(areas, weeks)`), one spec class each over `GateInputs`. A gated track has status `Gated` (sessions recorded, not evaluated) until the person accepts Start (`StartFromGate`). `StartGate.RunningGate()` is Walk → run's default (trend ≤ 90 kg; walk builder ≥ 45 min at ≥ 8 %, Easy, 2 weeks; no knee/shin/Achilles pain 4 weeks), applied by `withGate` on create or `PUT /cardio-tracks/{id}/gate {useDefault:true}`. `CardioGateEvaluator` (Application) gathers the trend EWMA, tracks and pain flags (track sessions + plan completions) for the list/today queries; `POST /cardio-tracks/{id}/start`.
- **Phase changes**: `PlanPhase` has `Label`, `Changes` (≤ 10 lines) and `Swaps` (`PhaseSwap(From, To)`), stored in the Phases jsonb. `PlanPhase.HeadsUpOn` = the 7 days before a phase through its first day. `GET /plans/phase-heads-up` resolves each swap to the linked workout's exercise; The Workout page's Swap runs the existing permanent `SubstituteExercise` after the person confirms. `PUT /plans/{id}/phases/{from}/changes` edits them. Push kind `PhaseChange` goes out once per phase (08:00–21:00 inside the window), logged in `PushNudgeLogs` against the phase start date.

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
| `SetCardioSettings` | Sets a cardio exercise's level/incline and/or speed (asked before its first session, editable later); audited as `CardioSettingsChanged` and copied to the lane's other exercises | `PUT /api/v1/workouts/{id}/exercises/{exerciseId}/cardio-settings` `{ intensity?, speed? }` |
| `ConfirmStartingWeight` | Confirms deferred starting weight for RPS | |
| `ConfirmWorkingWeight` | Confirms Cable/Machine weight after progression | |
| `RetrofixLinearTm` | Recalculates TM history from AMRAP data | |
| `SyncRoutineToHevy` | Pushes routine to Hevy via ACL; the key is resolved server-side via `IHevyKeyResolver` | |
| `CreateUser` | Auto-provisions user from Clerk auth | |
| `ReplaceFinisherSchedule` / `DeleteFinisherSchedule` | Live finisher wizard's phase-change push schedule (replace / forget) | |
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
| `StartSuggestedPlan` | One-tap onboarding: builds "Lumbridge Lard Shed" (template key `cut-to-85`, first shipped as "Cut to 85 — Nov 2026") from `SuggestedPlans/CutTo85Nov2026` (workout + cardio protocols + Mobility A/B + habits + linked plan) in one unit of work; idempotent while that plan is active, also under its old name (`IPlanTemplate.PreviousNames`, `PlanTemplates.IsSamePlan`) | `POST /api/v1/plans/suggested/cut-to-85` |
| Mobility: `CreateMobilityRoutine`, `UpdateMobilityRoutine`, `ReplaceDrills`, `DeleteMobilityRoutine` | Mobility routines and their ordered drills. Update renames and changes target minutes; delete returns 409 while any of the user's plans schedules the routine (past completions keep the id as history) | `POST /api/v1/mobility-routines`, `PUT /{id}`, `PUT /{id}/drills`, `DELETE /{id}` |
| Mobility progression: `RecordDrillFeedback`, `RespondToDrillSuggestion`, `UndoDrillProgression`, `ApplyHoldIncrease` | Routine kind (`WarmUp`/`Corrective`/`Stretch`/`YogaFlow`/`FollowAlong`, follow-along = link + minutes, no drills). Drills may carry `Levels` (name, description, sets, dose, optional `DoseRange`, cues) with `CurrentLevel`/`CleanStreak`/`SuggestedLevel`. `DrillProgressionRule`: 2 clean sessions → +step in range, at top → suggest next level (user accepts/declines); strain/stop → step back once. Feedback is idempotent on the session key; every change is in `ProgressionLog` with a reason; undo reverts the last change (all drills it touched). `ApplyHoldIncrease` is the phase bump (+N s to timed holds, ranges shift) | `POST /{id}/feedback`, `/{id}/suggestion`, `/{id}/progression/undo`, `/{id}/hold-increase` |
| `RenamePlan` | Renames a plan (`TrainingPlan.Rename`: required, trimmed, max 200). Owner only; another user's plan is 404. UI: pencil next to the plan name on Progress | `PUT /api/v1/plans/{id}/name` `{ name }` |
| `UpdateSchedule` | Replaces the plan's weekly template; returns `PlanScheduleRules` warnings (4×4 the day before Lower A/B) alongside the plan. A lift slot may carry `WarmUpRoutineId` and `FinisherCardioProtocolId` | `PUT /api/v1/plans/{id}/schedule` |
| `FinishGymSession` | Gym mode finish: stages the plan's lift completion (and the finisher's cardio completion) and a `HevyWorkoutPost`, then runs `CompleteDay` unchanged, whose save commits it all; then tries the Hevy post once. Idempotent on `ClientSessionId` (a replay returns the stored result, `replayed: true`) | `POST /api/v1/gym-sessions` |
| `RetryHevyPost` | "Not in Hevy yet" → Retry: makes a gym session's post due now and tries it | `POST /api/v1/gym-sessions/{clientSessionId}/hevy-retry` |
| Habits: `CreateHabit`, `UpdateHabit`, `ArchiveHabit`, `UpsertHabitLog`, `IncrementHabit`, `ImportHabits` | Counters have a `Step` (default 1; a ±1 tap moves ±Step, floor 0; icon key `meat` for protein). Habit definitions and per-local-date logs (last write wins per habit/day) | `/api/v1/habits` |
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
| `GetWeekPlan` | Planned sets for a week/day, plus the plan slot's label, warm-up, paired routine and the finisher as a `PlannedCardioDto` (`cardio`); a cardio exercise has no sets and carries `cardioBlock` (`PlannedCardioBlockDto`) |
| `GetCycle` (`GET /workouts/current/cycle`, `/workouts/{id}/cycle`) | The training cycle from the current session: current week/day, rest of this week, all of next week, each with its `WeekPlanDto` (Current workout page) |
| `GetStartingNumbersSuggestions` (`GET /workouts/{id}/suggestions/starting-numbers`, Clerk only) | For each pending exercise (Linear `IsTrainingMaxPending`, RPS `IsWeightPending`) a suggestion or null with evidence (set date/weight/reps, e1RM, 90-day window, stale flag, one-line reason). History = `HevyStrengthHistory`: up to 20 pages of `GET /v1/workouts` (stops a year back) merged with `HevyWorkoutSnapshot` rows, cached per user 5 min (`IMemoryCache`, successes only). Match by Hevy template id, then exact title, then the `custom:` catalogue name. `hevyStatus` `ok`/`noKey`/`unavailable` — the UI falls back to manual input; never a 5xx for a Hevy failure |
| `GetExerciseLibrary` | Paginated exercise definitions (search, filter) |
| `GetExerciseHistory` | Historical performance for an exercise |
| `GetWorkoutHistory` | Completed day history for a workout; a cardio performance carries `cardioSummary` ("7 min @ level 6 · Easy") instead of sets (also on `GetExerciseHistory` sessions); a cardio exercise's `settingsChanges` lists its settings edits |
| `GetCurrentUser` / `GetUser` | User profile |
| Plans: `GetToday`, `GetPlanCalendar`, `GetWeeklyGoals`, `GetDayBudget` | Today screen, month calendar, weekly rings, hard sets and time estimate |
| `GetUpcomingDays` (`GET /plans/upcoming?from=&days=`, 1–31, default 14) | The next days' sessions, each lift with its planned sets from the current A2S2 week, its finisher and (additive) `cardio` planned item; Terran CC |

Personal API tokens (`[AllowApiToken]`) may read `GET /plans/today?date=`, `GET /plans/upcoming` and `GET /sync/version` + `GET /sync/events`; no other plans or workouts route and no write. The list is pinned by `ApiTokenAllowListTests`.
| Habits, body weight, Hevy inbox, meals, push and token queries | Read sides of the commands above |
| `GetToday` | A plan day's sessions, phase and week goals. Lift sessions carry the slot's warm-up and finisher and, when finished in gym mode, `gymSessionId` + `hevyPostStatus` (Pending/Posted/Failed/Skipped) — `GET /api/v1/plans/today` |
| `ListCardioTracks` / `GetCardioTrack` | Tracks with prescriptions; one track with sessions, audit, benchmarks and chart points |
| `ListCardioTrackTemplates` | The shipped templates |
| `GetTodayCardio` | Active tracks' prescriptions, this week's zone 4–5 minutes and guardrail warnings |
| `GetHeartRateZones` | Max HR and zones |
| `GetPlanTemplates` | Every template previewed from a start date: phases with dates, schedule slots, sessions with 2a/2b slots and prescriptions, warm-ups, home routines, cardio tracks, `toFillIn` — `GET /api/v1/plans/templates[?startDate=]`, `GET /api/v1/plans/templates/{key}` |
| `GetSuggestedPlans` | Previews every plan template (Lumbridge Lard Shed, Falador Fat Furnace) for today (start Monday, week, lifting-day time estimates, inputs needed, already started) — `GET /api/v1/plans/suggested` |

## Gym mode

Brief § 3a. At the gym the person opens `/gym/:day` (Start workout on the Current workout page): one checklist of warm-up drills
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
  A posted session whose payload changed later (`HevyWorkoutPost.RequestUpdate`, status `UpdatePending`) is sent by
  the same retry as `PUT /v1/workouts/{id}` to its stored Hevy id, never posted or adopted; giving up leaves it Posted.
  `CardioExerciseBackfill` (startup, after `MuscleGroupBackfill`) moved a running Falador Fat Furnace onto cardio
  exercises this way: seeded the stair climber from the old stair track, retro-logged this week's stair finisher /
  fixed cool-down into history, queued the Hevy update, cleared those finishers and paused the stair track.
- The browser never writes workouts to Hevy: the v1 Send to Hevy on the completion summary is gone and the
  `/hevy/workouts` proxy refuses POST (GET stays for Pull from Hevy).
- **Cardio exercises (Plan v3).** Plan v3's stair climber and incline walk are exercises, not finishers: after the lifts each
  appears as a cardio card (block text, "set level / speed first" when settings are blank, Easy / Solid / Brutal, stopped
  early, pain), sent as `cardio` on the exercise performance; only items with an effort chosen are sent, so an untouched
  one keeps its progression. They replace the stair/fixed finisher on Plan v3 (earlier plans keep finishers and the live
  finisher wizard). The read-only preview (`GymPreview`), the cycle list and the Workout page's full plan list them with
  their block text; history rows and the progression modal show `cardioSummary` / the session list, and the modal lists the
  exercise's settings edits (`ExerciseHistoryDto.SettingsChanges`, from `CardioSettingsChanged` audit entries: date, old → new text). Frontend: `features/gym/gymCardio.ts` (`cardioBlockLine`),
  `GymCardioCard`, `CardioSettingsDialog`.
- Settings → Gym sessions sets each lift slot's warm-up and finisher (protocol, track, or a fixed cool-down).
- **Supersets.** `Exercise.SupersetGroup` (per day, max two exercises; set by plan specs, editable via
  UpdateExercises) feeds `PlannedExerciseDto.supersetGroup`, gym mode (A1 → B1 → A2 …, no rest between A
  and B, `RestDefaults.SupersetSeconds` 60 s after B) and `RoutineBuilder` superset ids. Older specs'
  `SupersetSpec` lists are turned into groups when a plan is applied.
- **Unilateral work is left side first**: gym mode lists Left then Right; Hevy routine/workout notes say "Left side first".
- **Extra set** (`Exercise.ExtraSetSide` None/Left/Right, unilateral reps-per-set only): sent as
  `ExercisePerformanceRequest.ExtraSet`, outside `CompletedSets`, so RPS evaluation never sees it (tested
  identical with/without); Hevy gets it as one more set with the note "Extra set: left". Plan v3 doesn't use it, nor supersets (its pause squat is reps per set, 4 × 5–8; its Walk → run gate has no walk-builder condition).
- **Fixed cool-down finisher** (`SessionFinisher.Fixed` = `FixedActivity` Walk/Treadmill, 1–60 min, notes):
  no LISS rule, no track. Gym mode ticks it; the finish logs a plain cardio completion; Hevy gets Treadmill
  `243710DE` (or Walking) with the duration.
- **Pending training max** (`LinearProgressionStrategy.IsTrainingMaxPending`): planned sets keep A2S2 sets,
  reps and AMRAP but weigh 0 (`metadata.isTrainingMaxPending`, `intensityPercentage`, `tier`); completing
  the exercise is refused ("Set your training max for … before finishing it.") until a TM is set.
- **Starting-number suggestions** (`Domain/Services/TrainingMaxSuggester`, pure): best Epley e1RM
  (`TrainingMaxEstimator.EstimateOneRepMax`; working sets of 1–12 reps, warm-ups ignored) dated within 90 days
  of today (Europe/London). TM = 90 % e1RM rounded **down** to 2.5 kg; RPS starting weight = e1RM ÷ (1 + mid-reps/30)
  rounded down to the equipment (2.5 kg; dumbbells 1 kg below 10 kg, even sizes from 10). Nothing in 90 days but
  older history: best set of the most recent session, −5 % per full 90 days since (cap −20 %), `stale`. No history:
  no suggestion. Suggestions are only prefilled — the typed value always wins, and nothing is saved until the
  person saves (`SetTrainingMax` / `ConfirmStartingWeight`, unchanged). A2S2/RPS/MinimalSets maths untouched.
- **Two devices.** `FinishGymSession` refuses (`ErrorCode.Conflict`, 409, "This day was already finished on
  another device") when the workout's current week already has the day recorded under a different client
  session id; nothing is staged and progression is untouched. The same client session id still replays.
  Drafts are versioned (`GymSessionState.version`, `migrateGymSession`) and drafts older than 7 days or for
  earlier weeks are removed (`expireGymDrafts`, on app start and when gym mode loads).

## Keeping clients in sync

Every user has a change stamp (`Aggregates/Sync/ChangeStamp`: `Version` + one version per area `habits`, `plan`, `workout`, `cardio`, `mobility`; an area holds the version of its last change). Commands declare what they write with `[ChangesSyncArea(SyncArea.X)]`; `ChangeVersionBehavior` (outermost MediatR behaviour) bumps the stamp after a successful result (atomic upsert in `UserChangeLogs`, migration `AddUserChangeLog`) and publishes to `ISyncNotifier` (in-process, one API instance). A failed bump is logged and never fails the write. Every command in the synced feature folders must carry the attribute (`ChangeVersionBehaviorTests.EveryCommandOfTheSyncedFeatures_IsMarked`).

- `GET /api/v1/sync/version` → `{ version, areas }`, `ETag: "<version>"`, `If-None-Match` → 304. Token-readable.
- `[SyncETag(areas)]` on `GET /habits`, `/habits/today`, `/plans/today`, `/plans/upcoming`: ETag = area versions + hash of query string and today's local date; 304 on match; bodies unchanged.
- `GET /api/v1/sync/events` (SSE, Clerk or token): `event: hello` on connect, `event: changed` `{"version":124,"areas":["habits"]}` per bump, `:keepalive` every `Sync:KeepAliveSeconds` (25), `id` = version, `Last-Event-ID` resumes (a `changed` with what was missed). Response buffering off, `X-Accel-Buffering: no`, `Cache-Control: no-cache`.
- Web: `sync/useSyncEvents` (mounted in `AppContent`) reads the stream with `fetch` + the Clerk token and invalidates `AREA_QUERY_KEYS`; silent backoff on failure.

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
