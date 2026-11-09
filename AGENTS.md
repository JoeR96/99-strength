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
| `A2S.Integration.Hevy` | Hevy API anti-corruption layer (Polly resilience) |
| `A2S.Web` | React frontend (separate — see `src/A2S.Web/AGENTS.md`) |

### Rules

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

**User** (aggregate root) — `Aggregates/User/User.cs`

**ExerciseDefinition** — reference data entity (exercise library, read-only)

### Strongly-Typed IDs

| ID | Type | Backing |
|----|------|---------|
| `WorkoutId` | `readonly record struct` | `Guid` |
| `ExerciseId` | `readonly record struct` | `Guid` |
| `ExerciseProgressionId` | `readonly record struct` | `Guid` |
| `ExerciseDefinitionId` | `readonly record struct` | `Guid` |
| `UserId` | `readonly record struct` | `string` (Clerk ID) |

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

Events are dispatched post-save via `MediatRDomainEventDispatcher`. Currently no consumers — extensibility point for future cross-aggregate concerns.

## CQRS Commands & Queries

### Commands

| Command | Handler | Purpose |
|---------|---------|---------|
| `CreateWorkout` | Creates workout with exercises from template | |
| `DeleteWorkout` | Soft deletes workout | |
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
| `SyncRoutineToHevy` | Pushes routine to Hevy via ACL | |
| `CreateUser` | Auto-provisions user from Clerk auth | |

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
2. `npm run start:api` — .NET API (auto-migrates DB on startup, seeds exercise library)
3. `npm run start:web` — Vite dev server

Or `npm start` for all three concurrently.
