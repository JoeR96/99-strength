# v2 Phase 3 — Domain expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the aggregates the Whole Plan needs beside the untouched A2S2 `Workout`: a `TrainingPlan` with schedule, phases, checkpoints and session completions; `CardioProtocol` and `MobilityRoutine` definitions; `HabitDefinition`/`HabitLog` with streaks; `BodyWeightEntry` with trend and pace; and the weekly volume budget plus session time estimate.

**Architecture:** Three independent PRs developed in parallel from the `LocalClock` foundation commit, each with its own Domain aggregate(s), Application commands/queries, EF configuration, API controller and tests. **No PR generates an EF migration**; the integrator generates them in order (plan → habits → volume) so the model snapshot never conflicts. Every date a person sees is a `DateOnly` from `ILocalClock` (Europe/London); every aggregate is user-scoped by `UserId` and queried through its repository with the current user.

**Tech Stack:** .NET 10, EF Core 9 packages (Npgsql, owned `ToJson()` for value collections), MediatR, FluentValidation, xUnit + FluentAssertions + NSubstitute, Testcontainers for Infrastructure/API.

**Spec:** `docs/v2/brief.md` § 2 (Domain expansion) and § 5 (Suggested plan: the data every type must be able to represent); `docs/v2/plan.md` → Phase 3.

## Global Constraints

- The `Workout` aggregate and A2S2 progression maths do not change. New code reads `Workout` (exercises, `CompletedActivities`, `GetPlannedSetsForDay`) but never writes it.
- Domain has no package references. Factory methods, `CheckRule`, `Result<T>` in Application, thin handlers.
- Every new aggregate carries `UserId UserId` and every query/command checks ownership via `ICurrentUserService.GetUserId()` (see `CompleteDayCommandHandler`). Unauthenticated → `Result.Failure(..., ErrorCode.Unauthenticated)`; another user's record → `ErrorCode.Unauthorized` (API 403) or `NotFound`.
- Strongly typed ids are `readonly record struct XId(Guid Value)` in a **new file per PR** under `src/A2S.Domain/Common/` (do not edit `StronglyTypedIds.cs`): `PlanIds.cs` (PR 2), `HabitIds.cs` (PR 3). EF converters like `UserConfiguration`.
- Dates: `DateOnly` everywhere a person sees a day; UTC `DateTime`/`DateTimeOffset` only for audit instants. Week = Monday–Sunday via `LocalDates.StartOfWeek`. Never call `DateTime.UtcNow`/`DateOnly.FromDateTime(DateTime.Now)` in Domain or handlers; take `ILocalClock`.
- **No `dotnet ef migrations add`** in these PRs. Add `DbSet`s to `A2SDbContext` and `IEntityTypeConfiguration`s; the integrator scaffolds the migration.
- Existing suites stay green: Domain 819, Application 188, Infrastructure 100, API 176 (+1 skipped).
- Controllers follow `DaysController`: `[ApiController] [Route("api/v1/...")] [Authorize]`, `result.ToProblemResult()` on failure.
- Infrastructure tests that need the real schema use `MigratedDatabase.CreateAsync(fixture, prefix)` from `tests/A2S.Infrastructure.Tests/MigratedDatabase.cs` (it applies all migrations, so until the integrator adds the migration these tests are written against `EnsureCreated` on a fresh database instead: use `A2SDbContext` with a unique database name and `Database.EnsureCreatedAsync()`).

## Review Focus

1. **Pause phase starting mid-week with two lifts already done** (brief § 2): those two count as Done, remaining lift dates in the week are Paused not Missed, and `GetWeeklyGoals` shows planned = sessions not covered by the pause. Pinned in PR 2 `PlanCalendarServiceTests`.
2. **Completion at 23:30 UTC on 24 Oct 2026** (BST, local 25 Oct): the session lands on the 25th. Pinned in PR 2 `PlanCalendarServiceTests` using `LocalClock` with a `FakeTimeProvider`.
3. **Two misses in a row vs. one miss across a month boundary** (brief § 2 streaks): 30 Nov hit, 1 Dec miss, 2 Dec hit → streak continues; 1 Dec and 2 Dec both missed → streak resets. A day with every habit at ≥ minimum counts. Pinned in PR 3 `HabitStreakTests`.
4. **Counter floor and over-target** (brief § 2): decrement at 0 stays 0; 7/6 water is reported as 7/6 and counts. Pinned in PR 3 `HabitLogTests`.
5. **Pace against checkpoints on the checkpoint dates** (brief § 5): 95 kg on 9 Nov 2026 start, 92 kg on 7 Dec, 91 kg on 21 Dec, hold to 3 Jan, 88 kg on 1 Feb, 85 kg on 8 Mar 2027; a trend 0.5 kg above the line is `Behind`, within ±0.3 kg is `OnPace`, below is `Ahead`. Pinned in PR 3 `TrendServiceTests`.

---

## PR 2 — Time and plan (`feat/v2-domain-plan`)

### Task 2.1: Ids and `CardioProtocol`

**Files:**
- Create: `src/A2S.Domain/Common/PlanIds.cs` — `TrainingPlanId`, `CardioProtocolId`, `MobilityRoutineId`, `SessionCompletionId` (all `Guid`).
- Create: `src/A2S.Domain/Aggregates/Cardio/CardioProtocol.cs`, `CardioProtocolKind.cs` (`NorwegianFourByFour | CouchToFiveK | Liss`), `CardioInterval.cs` (VO: `string Label, int Seconds, CardioEffort Effort` with `CardioEffort WarmUp|Work|Recovery|CoolDown|Easy`).
- Create: `src/A2S.Domain/Repositories/ICardioProtocolRepository.cs` (`IRepository<CardioProtocol, CardioProtocolId>` + `Task<IReadOnlyList<CardioProtocol>> ListForUserAsync(UserId, ct)`).
- Test: `tests/A2S.Domain.Tests/Aggregates/Cardio/CardioProtocolTests.cs`

**Interfaces:**
- Produces: `CardioProtocol : AggregateRoot<CardioProtocolId>` with `UserId UserId`, `string Name`, `CardioProtocolKind Kind`, `IReadOnlyList<CardioInterval> Intervals`, `int TotalMinutes` (sum of intervals rounded up), `string Notes`, `int? WeekIndex`, `bool RepeatWeek`.
- Factories: `static CardioProtocol NorwegianFourByFour(UserId)` → warm-up 600 s, then 4 × (work 240 s + recovery 180 s), cool-down 300 s; `TotalMinutes == 38`. `static CardioProtocol CouchToFiveK(UserId, int weekIndex /*1–9*/, bool repeatWeek = false)` → the standard C25K week (table in the test, e.g. W1: 5 min warm-up walk then 8 × (60 s run + 90 s walk), 5 min cool-down; W9: 5 min walk, 30 min run, 5 min walk). `static CardioProtocol Liss(UserId, int minutes /*30–40*/, string? note)` → one `Easy` interval; `CheckRule` on range.
- `CardioProtocol WithRepeatWeek()` (C25K only), `CardioProtocol NextWeek()` (C25K: weekIndex+1 or same if `RepeatWeek`, caps at 9).

- [ ] Tests: 4×4 is 38 min with 10 intervals; LISS 29 and 41 minutes throw; C25K week 1 and week 9 interval shapes; `NextWeek` honours repeat and the 9 cap.
- [ ] Implement, run `dotnet test tests/A2S.Domain.Tests --filter CardioProtocol` → PASS. Commit.

### Task 2.2: `MobilityRoutine`

**Files:**
- Create: `src/A2S.Domain/Aggregates/Mobility/MobilityRoutine.cs`, `MobilityDrill.cs` (VO), `DrillDose.cs` (VO: `DoseUnit Unit Reps|Seconds|Breaths`, `int Amount`), `SideBias.cs` (`None|Left|Right`).
- Create: `src/A2S.Domain/Repositories/IMobilityRoutineRepository.cs` (+ `ListForUserAsync`).
- Test: `tests/A2S.Domain.Tests/Aggregates/Mobility/MobilityRoutineTests.cs`

**Interfaces:**
- `MobilityDrill(string Name, int Sets, DrillDose Dose, SideBias ExtraSetSide, string? Cues, string? StandingAlternative, bool PerSide)`.
- `MobilityRoutine : AggregateRoot<MobilityRoutineId>`: `UserId`, `string Name`, `int TargetMinutes`, `IReadOnlyList<MobilityDrill> Drills`; `static Create(UserId, name, targetMinutes, IEnumerable<MobilityDrill>)` (≥ 1 drill, unique names); `AddDrill`, `RemoveDrill(name)`, `MoveDrill(name, newIndex)`, `ReplaceDrills(...)`.

- [ ] Tests: create with the five Mobility A drills from brief § 5 (names, sets, doses, standing alternatives); duplicate drill name throws; move/remove keep order.
- [ ] Implement → PASS. Commit.

### Task 2.3: `TrainingPlan` aggregate

**Files:**
- Create: `src/A2S.Domain/Aggregates/Plan/TrainingPlan.cs`, `ScheduledSession.cs`, `SessionKind.cs`, `PlanPhase.cs`, `PhaseType.cs`, `Checkpoint.cs`, `SessionCompletion.cs`, `CardioLog.cs`, `CompletionSource.cs`, `WeeklyAllowances.cs`.
- Create: `src/A2S.Domain/Repositories/ITrainingPlanRepository.cs` (`GetActiveForUserAsync(UserId, ct)`, `ListForUserAsync`).
- Create: `src/A2S.Domain/Events/PlanEvents.cs` (`TrainingPlanStarted`, `SessionCompleted`, `PhaseAdded`).
- Test: `tests/A2S.Domain.Tests/Aggregates/Plan/TrainingPlanTests.cs`

**Interfaces:**
- `enum SessionKind { Rest, Lift, Cardio, Mobility }`; `ScheduledSession(DayOfWeek Day, SessionKind Kind, DayNumber? LiftDay, CardioProtocolId? CardioProtocolId, MobilityRoutineId? MobilityRoutineId, string Label)` — a weekday may hold more than one session (Wed = Cardio + Mobility B), so the schedule is `IReadOnlyList<ScheduledSession>` ordered by `Day` then insertion.
- `enum PhaseType { Cut, Maintenance, Pause, Deload }`; `PlanPhase(PhaseType Type, DateOnly From, DateOnly To /*inclusive*/, string? Note)`; phases may not overlap.
- `Checkpoint(DateOnly Date, decimal TargetKg, string? Note)` ordered by date, unique dates.
- `WeeklyAllowances(int FlexibleMeals = 1, int AlcoholOccasions = 1)`.
- `enum CompletionSource { Manual, Hevy }`; `SessionCompletion : Entity<SessionCompletionId>` with `DateOnly Date`, `SessionKind Kind`, `DayNumber? LiftDay`, `CardioProtocolId?`, `MobilityRoutineId?`, `CompletionSource Source`, `string? HevyWorkoutId`, `DateTimeOffset RecordedAtUtc`, `CardioLog? Cardio` (`int DurationSeconds, int? AvgHeartRate, int? Rpe, decimal? DistanceMeters, string? Notes`), `IReadOnlyList<bool>? DrillTicks`, plus allowance kinds recorded as `AllowanceUse(DateOnly, AllowanceKind FlexibleMeal|Alcohol)` in a separate owned list.
- `TrainingPlan : AggregateRoot<TrainingPlanId>`: `UserId`, `string Name`, `DateOnly StartDate`, `bool IsActive`, `WorkoutId? LinkedWorkoutId`, `IReadOnlyList<ScheduledSession> Schedule`, `IReadOnlyList<PlanPhase> Phases`, `IReadOnlyList<Checkpoint> Checkpoints`, `IReadOnlyList<SessionCompletion> Completions`, `IReadOnlyList<AllowanceUse> AllowanceUses`, `WeeklyAllowances Allowances`, `decimal StartWeightKg`.
  - `static TrainingPlan Create(UserId, string name, DateOnly requestedStart, IEnumerable<ScheduledSession> schedule, decimal startWeightKg, WeeklyAllowances? allowances = null)` → `StartDate = LocalDates.ComingMonday(requestedStart)`; at least one non-rest session.
  - `ReplaceSchedule(IEnumerable<ScheduledSession>)`, `AddPhase(PlanPhase)` (no overlap; `From <= To`), `RemovePhase(From)`, `AddCheckpoint(Checkpoint)`, `RemoveCheckpoint(date)`, `LinkWorkout(WorkoutId)`, `Activate()`/`Deactivate()`.
  - `IReadOnlyList<ScheduledSession> SessionsOn(DateOnly date)` (empty before `StartDate`), `PlanPhase? PhaseOn(DateOnly)`, `bool IsPaused(DateOnly)`.
  - `SessionCompletion CompleteSession(DateOnly date, SessionKind kind, CompletionSource source, DayNumber? liftDay = null, CardioProtocolId? cardio = null, MobilityRoutineId? mobility = null, string? hevyWorkoutId = null, CardioLog? log = null, IReadOnlyList<bool>? drillTicks = null, DateTimeOffset? recordedAt = null)` — idempotent on `hevyWorkoutId` (returns the existing completion) and on (date, kind, liftDay/cardio/mobility) for manual completions (returns existing). Raises `SessionCompleted`.
  - `bool UndoCompletion(SessionCompletionId)`; `AllowanceUse UseAllowance(DateOnly, AllowanceKind)`; `bool RemoveAllowanceUse(DateOnly, AllowanceKind)`.

- [ ] Tests: start snaps to coming Monday (Wed 11 Nov 2026 → Mon 16 Nov); Monday stays; `SessionsOn` before start is empty; overlapping phases throw; duplicate Hevy id returns the same completion and the count stays 1; manual duplicate same day/kind returns existing; `IsPaused` inside/outside a Pause phase; `UseAllowance` twice same kind same week is allowed (goals report the overflow).
- [ ] Implement → PASS. Commit.

### Task 2.4: `PlanCalendarService` and `WeeklyGoals`

**Files:**
- Create: `src/A2S.Domain/Services/PlanCalendarService.cs`, `src/A2S.Domain/Services/PlanDayStatus.cs`, `WeeklyGoals.cs`.
- Test: `tests/A2S.Domain.Tests/Services/PlanCalendarServiceTests.cs`

**Interfaces:**
- `enum SessionStatus { Planned, Done, Missed, Paused, Rest }`; `record PlanDaySession(ScheduledSession Session, SessionStatus Status, SessionCompletion? Completion)`; `record PlanDay(DateOnly Date, PhaseType? Phase, IReadOnlyList<PlanDaySession> Sessions)`.
- `record WeeklyGoals(DateOnly WeekStart, int LiftsPlanned, int LiftsDone, int CardioPlanned, int CardioDone, int MobilityPlanned, int MobilityDone, int FlexibleMealsUsed, int FlexibleMealsAllowed, int AlcoholOccasions, int AlcoholAllowed)`.
- `PlanCalendarService(ILocalClock clock)`:
  - `PlanDay DayFor(TrainingPlan plan, DateOnly date, IReadOnlyCollection<WorkoutActivity> liftingHistory)` — a lift session is Done when a `SessionCompletion` of kind Lift exists for the date **or** a `WorkoutActivity` with `CompletedAt` falling on that local date (`clock.ToLocalDate`) and matching `DayNumber` exists; status rules: inside a Pause phase → `Paused` unless completed (then `Done`); date < `clock.Today()` and not done → `Missed`; date ≥ today → `Planned`; `Rest` kind → `Rest`.
  - `IReadOnlyList<PlanDay> Month(TrainingPlan, int year, int month, history)`; `IReadOnlyList<PlanDay> Week(TrainingPlan, DateOnly anyDay, history)`.
  - `WeeklyGoals ForWeek(TrainingPlan, DateOnly anyDay, history)` — planned counts exclude Paused days; done counts include completions from either source; allowances count `AllowanceUses` in the week.

- [ ] Tests (clock = `FakeTimeProvider`): review focus 1 (Pause Wed–Sun with Mon+Tue lifts done → Mon/Tue Done, Thu/Fri Paused, `LiftsPlanned == 2`, `LiftsDone == 2`); review focus 2 (activity `CompletedAt = 2026-10-24T23:30Z` is Done on 2026-10-25); yesterday's un-done lift is Missed, tomorrow's is Planned; Sunday is Rest; Wednesday with two sessions returns both.
- [ ] Implement → PASS. Commit.

### Task 2.5: Application — commands, queries, DTOs

**Files:**
- Create under `src/A2S.Application/Commands/Plans/`: `CreateTrainingPlanCommand` (+Handler, +Validator), `UpdateScheduleCommand`, `AddPhaseCommand`, `RemovePhaseCommand`, `AddCheckpointCommand`, `RemoveCheckpointCommand`, `LinkWorkoutCommand`, `CompleteSessionCommand` (date, kind, liftDay?, cardioProtocolId?, mobilityRoutineId?, cardio log?, drillTicks?), `UndoSessionCompletionCommand`, `UseAllowanceCommand`, `RemoveAllowanceUseCommand`.
- Create under `src/A2S.Application/Commands/Cardio/`: `CreateCardioProtocolCommand` (kind + params), `AdvanceCouchToFiveKCommand`, `SetRepeatWeekCommand`.
- Create under `src/A2S.Application/Commands/Mobility/`: `CreateMobilityRoutineCommand`, `ReplaceDrillsCommand`.
- Create under `src/A2S.Application/Queries/Plans/`: `GetActivePlanQuery` → `TrainingPlanDto`; `GetTodayQuery(DateOnly? date)` → `TodayDto(DateOnly Date, PhaseType? Phase, IReadOnlyList<TodaySessionDto> Sessions, WeeklyGoalsDto Week)`; `GetPlanCalendarQuery(int year, int month)` → `IReadOnlyList<PlanDayDto>`; `GetWeeklyGoalsQuery(DateOnly? anyDay)`; `ListCardioProtocolsQuery`; `ListMobilityRoutinesQuery`.
- Create: `src/A2S.Application/DTOs/PlanDtos.cs` (all plan DTOs; `DateOnly` serialises as `yyyy-MM-dd`).
- Test: `tests/A2S.Application.Tests/Commands/Plans/*Tests.cs`, `tests/A2S.Application.Tests/Queries/Plans/GetTodayQueryHandlerTests.cs` (NSubstitute repos; `FakeTimeProvider`).

**Interfaces:**
- `TodaySessionDto(SessionKind Kind, string Label, SessionStatus Status, DayNumber? LiftDay, Guid? CardioProtocolId, Guid? MobilityRoutineId, Guid? CompletionId)`.
- Handlers resolve the lifting history via `IWorkoutRepository.GetByIdAsync(plan.LinkedWorkoutId)` when linked.

- [ ] Tests: create plan unauthenticated → `Unauthenticated`; create snaps start; complete session twice with the same Hevy id → one completion; `GetToday` on a paused day returns `Paused` sessions; `GetToday` for another user's plan → `NotFound`.
- [ ] Implement (handlers thin; validators for ranges: checkpoint kg 30–300, LISS 30–40, C25K week 1–9) → PASS. Commit.

### Task 2.6: Infrastructure + API

**Files:**
- Create: `src/A2S.Infrastructure/Persistence/Configurations/TrainingPlanConfiguration.cs` (table `TrainingPlans`; `Schedule`, `Phases`, `Checkpoints`, `AllowanceUses` as owned `ToJson()`; `Completions` as a child table `SessionCompletions` with FK, `CardioLog` owned JSON, `DrillTicks` jsonb; unique index on (`TrainingPlanId`, `HevyWorkoutId`) where not null; index (`UserId`, `IsActive`)), `CardioProtocolConfiguration.cs` (`CardioProtocols`, intervals JSON), `MobilityRoutineConfiguration.cs` (`MobilityRoutines`, drills JSON).
- Modify: `src/A2S.Infrastructure/Persistence/A2SDbContext.cs` (add `TrainingPlans`, `CardioProtocols`, `MobilityRoutines` DbSets), `src/A2S.Infrastructure/DependencyInjection.cs` (repositories).
- Create: `src/A2S.Infrastructure/Repositories/TrainingPlanRepository.cs` (`GetByIdAsync` includes `Completions`), `CardioProtocolRepository.cs`, `MobilityRoutineRepository.cs`.
- Create: `src/A2S.Api/Controllers/PlansController.cs` (`/api/v1/plans`: `GET active`, `POST`, `PUT {id}/schedule`, `POST {id}/phases`, `DELETE {id}/phases/{from}`, `POST {id}/checkpoints`, `DELETE {id}/checkpoints/{date}`, `POST {id}/link-workout`, `POST {id}/completions`, `DELETE {id}/completions/{completionId}`, `POST {id}/allowances`, `DELETE {id}/allowances`, `GET today?date=`, `GET calendar?year=&month=`, `GET weekly-goals?date=`), `CardioProtocolsController.cs` (`/api/v1/cardio-protocols`), `MobilityRoutinesController.cs` (`/api/v1/mobility-routines`).
- Test: `tests/A2S.Infrastructure.Tests/Repositories/TrainingPlanRepositoryTests.cs` (round-trip a plan with schedule, phases, checkpoints and two completions through a fresh `EnsureCreated` database; completions reload), `tests/A2S.Api.Tests/Integration/PlansControllerTests.cs` (create → today → complete → today shows Done; other user's plan 404; unauthenticated 401).

- [ ] Implement, `dotnet build A2S.sln` clean, run all suites (API tests will fail with "relation does not exist" until the integrator adds the migration — in that case run the new API tests after the integrator's migration; Domain/Application/Infrastructure must be green now). Commit.

---

## PR 3 — Habits and body weight (`feat/v2-domain-habits`)

### Task 3.1: `HabitDefinition` and `HabitLog`

**Files:**
- Create: `src/A2S.Domain/Common/HabitIds.cs` — `HabitId`, `HabitLogId`, `BodyWeightEntryId`.
- Create: `src/A2S.Domain/Aggregates/Habits/HabitDefinition.cs`, `HabitType.cs` (`Counter | Numeric | Checkbox`), `HabitLog.cs`.
- Create: `src/A2S.Domain/Repositories/IHabitRepository.cs` (`ListForUserAsync(UserId, includeArchived, ct)`, `GetLogsAsync(UserId, DateOnly from, DateOnly to, ct)`, `GetLogAsync(HabitId, DateOnly, ct)`, `AddLogAsync`, `UpdateLog`).
- Test: `tests/A2S.Domain.Tests/Aggregates/Habits/HabitDefinitionTests.cs`, `HabitLogTests.cs`

**Interfaces:**
- `HabitDefinition : AggregateRoot<HabitId>`: `UserId`, `string Name`, `HabitType Type`, `decimal Target`, `decimal Minimum`, `string? Unit`, `int Order`, `bool Archived`, `string? Icon`, `string? ExternalKey` (e.g. `terran:walks`, used by import idempotency).
  - `static Create(UserId, name, type, target, minimum, unit, order, icon = null, externalKey = null)`: Counter/Numeric need `0 < minimum <= target`; Checkbox forces target = minimum = 1.
  - `Rename`, `SetTargets(target, minimum)`, `SetOrder`, `Archive()`, `Unarchive()`.
- `HabitLog : Entity<HabitLogId>`: `HabitId`, `UserId`, `DateOnly Date`, `decimal Value`, `DateTimeOffset UpdatedAtUtc`.
  - `static Create(HabitId, UserId, DateOnly date, decimal value, DateTimeOffset updatedAt)`; `Increment(step = 1, at)`, `Decrement(step = 1, at)` (floor 0), `SetValue(value, at)` (≥ 0), `Toggle(at)` (0 ↔ 1).
  - `bool Apply(decimal value, DateTimeOffset clientTimestamp)` — last-write-wins: applies only when `clientTimestamp >= UpdatedAtUtc`; returns whether applied.
- `static class HabitRules { static bool MetMinimum(HabitDefinition, HabitLog?) ; static bool MetTarget(...) }`.

- [ ] Tests: Checkbox normalises targets; `minimum > target` throws; decrement floors at 0; over-target 7/6 is kept; LWW ignores an older timestamp; Toggle.
- [ ] Implement → PASS. Commit.

### Task 3.2: Streaks

**Files:**
- Create: `src/A2S.Domain/Services/HabitStreakService.cs`, `HabitStreak.cs`.
- Test: `tests/A2S.Domain.Tests/Services/HabitStreakTests.cs`

**Interfaces:**
- `record HabitStreak(int Current, int Best, DateOnly? LastCountedDay, bool TodayCounted, bool GraceUsed)`.
- `HabitStreakService.Compute(IReadOnlyList<HabitDefinition> activeHabits, IReadOnlyList<HabitLog> logs, DateOnly today, DateOnly? firstDay = null)`: a day counts when **every** active (non-archived) habit has a log that meets at least its `Minimum` (brief: "a 30% day still counts"); walking back from today, one non-counting day is forgiven (`GraceUsed`), two consecutive non-counting days end the streak ("never miss twice"); today not yet counted does not break the streak (`TodayCounted == false`); `Best` is the longest run under the same rule across all history; `firstDay` defaults to the earliest log.

- [ ] Tests: review focus 3 (both variants across 30 Nov–2 Dec); minimum-but-not-target day counts; one habit missing on a day → day does not count; today pending keeps `Current`; two habits with one archived — archived is ignored; empty logs → zeros.
- [ ] Implement → PASS. Commit.

### Task 3.3: `BodyWeightEntry` and `TrendService`

**Files:**
- Create: `src/A2S.Domain/Aggregates/BodyWeight/BodyWeightEntry.cs`, `src/A2S.Domain/Repositories/IBodyWeightRepository.cs` (`GetForDateAsync(UserId, DateOnly)`, `ListAsync(UserId, from, to)`).
- Create: `src/A2S.Domain/Services/TrendService.cs`, `TrendPoint.cs`, `Pace.cs`.
- Test: `tests/A2S.Domain.Tests/Services/TrendServiceTests.cs`

**Interfaces:**
- `BodyWeightEntry : AggregateRoot<BodyWeightEntryId>`: `UserId`, `DateOnly Date`, `decimal Kg` (30–300), `DateTimeOffset RecordedAtUtc`; `static Create(...)`, `Update(kg, at)`. One entry per user per date (unique index).
- `record TrendPoint(DateOnly Date, decimal Kg, decimal? MovingAverage7, decimal Ewma)`; `enum Pace { OnPace, Behind, Ahead, NoTarget }`; `record PaceResult(Pace Pace, decimal TargetKgToday, decimal TrendKg, decimal DeltaKg)`.
- `TrendService.Series(IReadOnlyList<BodyWeightEntry> entries, decimal alpha = 0.1m)` → points ordered by date; `MovingAverage7` null until 7 entries exist in the trailing 7 calendar days window (use entries, not calendar gaps, within the last 7 days); EWMA seeded with the first weight.
- `TrendService.PaceAgainst(IReadOnlyList<(DateOnly Date, decimal Kg)> checkpoints, DateOnly startDate, decimal startKg, DateOnly today, decimal trendKg, decimal toleranceKg = 0.3m)` → straight line between the neighbouring checkpoints (start counts as the first point; after the last checkpoint the target holds); `DeltaKg = trendKg - target`; `Behind` when delta > tolerance, `Ahead` when delta < -tolerance.

- [ ] Tests: review focus 5 on each checkpoint date; a date between 7 Dec and 21 Dec interpolates linearly (14 Dec → 91.5); maintenance window 22 Dec–3 Jan holds 91; EWMA α = 0.1 hand-computed for three points; MA7 null before 7 points.
- [ ] Implement → PASS. Commit.

### Task 3.4: Application

**Files:**
- Create under `src/A2S.Application/Commands/Habits/`: `CreateHabitCommand`, `UpdateHabitCommand` (name, target, minimum, unit, order, icon), `ArchiveHabitCommand`, `UnarchiveHabitCommand`, `UpsertHabitLogCommand(Guid HabitId, DateOnly Date, decimal Value, DateTimeOffset ClientTimestamp)` (LWW), `IncrementHabitCommand(Guid HabitId, DateOnly Date, int Delta /*±1*/, DateTimeOffset ClientTimestamp)`, `ImportHabitsCommand(IReadOnlyList<ImportHabitDto>)` where `ImportHabitDto(string ExternalKey, string Name, HabitType Type, decimal Target, decimal Minimum, string? Unit, int Order, IReadOnlyList<ImportHabitLogDto> Logs)` and `ImportHabitLogDto(DateOnly Date, decimal Value)` — idempotent: habit matched by `ExternalKey`, logs upserted only when the value differs; returns `ImportResult(int HabitsCreated, int HabitsMatched, int LogsWritten, int LogsUnchanged)`.
- Create under `src/A2S.Application/Commands/BodyWeight/`: `RecordBodyWeightCommand(DateOnly Date, decimal Kg)` (upsert by date).
- Create under `src/A2S.Application/Queries/Habits/`: `GetHabitsTodayQuery(DateOnly? date)` → `HabitsTodayDto(DateOnly Date, IReadOnlyList<HabitTodayDto> Habits, HabitStreakDto Streak)` with `HabitTodayDto(Guid Id, string Name, HabitType Type, decimal Target, decimal Minimum, string? Unit, decimal Value, bool MetMinimum, bool MetTarget, int Order, string? Icon)`; `GetHabitHistoryQuery(DateOnly From, DateOnly To)` → per habit per day values; `GetHabitsQuery(bool includeArchived)`.
- Create under `src/A2S.Application/Queries/BodyWeight/`: `GetWeightTrendQuery(DateOnly From, DateOnly To)` → `WeightTrendDto(IReadOnlyList<TrendPointDto> Points, PaceDto? Pace)` — pace is computed only when an active `TrainingPlan` exists; **because PR 2 is developed in parallel, define `IPlanCheckpointSource { Task<PlanCheckpoints?> ForUserAsync(UserId, ct) }` in `src/A2S.Application/Interfaces/IPlanCheckpointSource.cs` with `record PlanCheckpoints(DateOnly StartDate, decimal StartKg, IReadOnlyList<(DateOnly Date, decimal Kg)> Checkpoints)`, and register a `NullPlanCheckpointSource` (returns null) in Infrastructure DI; the integrator swaps in a TrainingPlan-backed implementation.**
- Test: `tests/A2S.Application.Tests/Commands/Habits/*Tests.cs`, `.../Queries/Habits/GetHabitsTodayQueryHandlerTests.cs`.

- [ ] Tests: upsert with older client timestamp leaves the newer value; increment creates the log when absent; import twice → second run `LogsWritten == 0`; `GetHabitsToday` reports `MetMinimum`/`MetTarget` and the streak; record weight twice same date updates.
- [ ] Implement → PASS. Commit.

### Task 3.5: Infrastructure + API

**Files:**
- Create: `HabitDefinitionConfiguration.cs` (`Habits`; index (`UserId`, `Archived`, `Order`); unique (`UserId`, `ExternalKey`) where not null), `HabitLogConfiguration.cs` (`HabitLogs`; unique (`HabitId`, `Date`); index (`UserId`, `Date`)), `BodyWeightEntryConfiguration.cs` (`BodyWeightEntries`; unique (`UserId`, `Date`)).
- Modify: `A2SDbContext.cs` (DbSets `Habits`, `HabitLogs`, `BodyWeightEntries`), `DependencyInjection.cs` (repositories, `NullPlanCheckpointSource`).
- Create: `HabitRepository.cs`, `BodyWeightRepository.cs`.
- Create: `src/A2S.Api/Controllers/HabitsController.cs` (`/api/v1/habits`: `GET`, `POST`, `PUT {id}`, `POST {id}/archive`, `POST {id}/unarchive`, `GET today?date=`, `GET history?from=&to=`, `PUT {id}/logs/{date}` body `{ value, clientTimestamp }`, `POST {id}/logs/{date}/increment` body `{ delta, clientTimestamp }`, `POST import`), `BodyWeightController.cs` (`/api/v1/body-weight`: `PUT {date}` body `{ kg }`, `GET trend?from=&to=`).
- Test: `tests/A2S.Infrastructure.Tests/Repositories/HabitRepositoryTests.cs` (fresh `EnsureCreated` DB; unique (habit, date) enforced; range query), `tests/A2S.Api.Tests/Integration/HabitsControllerTests.cs` (create → increment ×2 → today shows 2/6 → decrement → 1/6; LWW; import idempotent; 401).

- [ ] Implement, build clean, Domain/Application/Infrastructure green. Commit.

---

## PR 4 — Volume budget and time estimate (`feat/v2-domain-volume`)

### Task 4.1: Muscle groups on `ExerciseDefinition`

**Files:**
- Modify: `src/A2S.Domain/Entities/ExerciseDefinition.cs` — add `IReadOnlyList<string> SecondaryMuscleGroups` (empty default) and `void SetMuscleGroups(string primary, IEnumerable<string> secondary)` (lower-case, trimmed, distinct, primary excluded from secondary).
- Create: `src/A2S.Domain/Services/MuscleGroups.cs` — `enum VolumeBucket { Chest, Back, Shoulders, Quads, Hamstrings, Glutes, Core, Arms, Calves, Other }` and `static VolumeBucket? BucketFor(string hevyMuscleGroup)` mapping Hevy's groups: chest→Chest; lats, upper_back, traps, lower_back→Back; shoulders→Shoulders; quadriceps→Quads; hamstrings→Hamstrings; glutes, abductors, adductors→Glutes; abdominals→Core; biceps, triceps, forearms→Arms; calves→Calves; neck, full_body, cardio, other→Other.
- Create: `src/A2S.Infrastructure/SeedData/MuscleGroupBackfill.cs` — `static Task<int> RunAsync(A2SDbContext, ILogger, ct)`: for every `ExerciseDefinition`, parse the primary group from the `Hevy: <group>` token in `Description` (regex `Hevy:\s*([a-z_]+)`), and secondary groups from `scripts/hevy-exercises.json` embedded as `A2S.Infrastructure.SeedData.hevy-exercises.json` (copy the file into `SeedData/` and add it as `EmbeddedResource`; match by exercise title, case-insensitive); only updates rows whose values differ; returns the count. Called from `Program.cs` after the seeder. (Column add is the integrator's migration.)
- Test: `tests/A2S.Domain.Tests/Services/MuscleGroupsTests.cs`, `tests/A2S.Infrastructure.Tests/SeedData/MuscleGroupBackfillTests.cs` (fresh `EnsureCreated` DB seeded via `ExerciseDefinitionSeeder`; after backfill `Squat (Barbell)` primary is `quadriceps`; second run returns 0).

- [ ] Tests → implement → PASS. Commit.

### Task 4.2: `VolumeBudgetService`

**Files:**
- Create: `src/A2S.Domain/Services/VolumeBudgetService.cs`, `VolumeCaps.cs`, `VolumeWarning.cs`.
- Test: `tests/A2S.Domain.Tests/Services/VolumeBudgetServiceTests.cs`

**Interfaces:**
- `record VolumeCaps(IReadOnlyDictionary<VolumeBucket,int> HardSetsPerWeek)` with `static VolumeCaps Default` = chest 10, back 14, shoulders 10, quads 10, hamstrings 10, glutes 12, core 12 (arms/calves/other uncapped).
- `record VolumeWarning(VolumeBucket Bucket, int Sets, int Cap, string Message)`.
- `VolumeBudgetService(IA2SProgramProvider program)`:
  - `IReadOnlyDictionary<VolumeBucket,int> HardSetsPerGroup(Workout workout, int weekNumber, Func<string, (string Primary, IReadOnlyList<string> Secondary)?> lookup)` — for every exercise in the workout, planned working sets for the week (`exercise.CalculatePlannedSets(week, block)`, excluding warm-ups; deload weeks use the deload set count) count **1.0 to the primary bucket and 0.5 to each secondary bucket**, rounded half-up per bucket at the end; `lookup` resolves an exercise's `ExternalTemplateId`/name to groups (Application supplies it from `IExerciseLibraryProvider`).
  - `IReadOnlyList<VolumeWarning> Warnings(IReadOnlyDictionary<VolumeBucket,int> sets, VolumeCaps caps)` — one warning per bucket over cap: `"Chest: 12 hard sets this week, cap 10"`.

- [ ] Tests: Upper A from brief § 5 (bench 5 sets + chest-supported row 3 + landmine press 3 + cable row 3 + face pulls 3) → chest 5+1.5(press secondary)=7 → 7, back 3+3+1.5=8 → 8, shoulders 3+3(face pulls primary shoulders)+2.5 → …; adding a 4th chest accessory with 3 sets pushes chest over 10 and yields exactly one warning naming Chest; deload week counts 4 sets for the main lift.
- [ ] Implement → PASS. Commit.

### Task 4.3: `SessionTimeEstimator`

**Files:**
- Create: `src/A2S.Domain/Services/SessionTimeEstimator.cs`, `SessionTimeEstimate.cs`.
- Test: `tests/A2S.Domain.Tests/Services/SessionTimeEstimatorTests.cs`

**Interfaces:**
- `record ExerciseTiming(string Name, int Sets, int RestSeconds, int? SupersetGroup)`; `record SessionTimeEstimate(int TotalMinutes, int WarmUpMinutes, IReadOnlyList<(string Name, int Minutes)> PerExercise, bool OverBudget, int BudgetMinutes)`.
- `SessionTimeEstimator.Estimate(IEnumerable<ExerciseTiming> exercises, int warmUpMinutes = 10, int workSeconds = 40, int budgetMinutes = 55)` — each set costs `workSeconds + restSeconds`; exercises sharing a `SupersetGroup` alternate, so rest is counted once per pair of sets (the pair costs `2 × workSeconds + max(rest)`); result rounded up to whole minutes; `OverBudget = TotalMinutes > budgetMinutes`.
- Default rests when unknown: main lift 180 s, accessories 90 s, superset 60 s (constants `RestDefaults`).

- [ ] Tests: Upper A hand calculation: bench 5 × (40+180) = 1100 s; row 3 × 130 = 390; landmine 3 × 130 = 390 (per side counts as one set each side → 6 sets = 780); superset cable row + face pulls 3 pairs × (80 + 60) = 420 → total ≈ 10 + (1100+390+780+420)/60 ≈ 55 → assert within ±10 % of 55 and `OverBudget` false at 55; adding one 3 × 90 s exercise flips `OverBudget`.
- [ ] Implement → PASS. Commit.

### Task 4.4: Application + API

**Files:**
- Create: `src/A2S.Application/Queries/GetDayBudget/GetDayBudgetQuery(Guid WorkoutId, DayNumber Day, int? WeekNumber)` → `DayBudgetDto(IReadOnlyDictionary<string,int> HardSetsThisWeek, IReadOnlyList<VolumeWarningDto> VolumeWarnings, SessionTimeEstimateDto Time)`; handler uses `IExerciseLibraryProvider` for groups and `RestDefaults` for rests.
- Modify: `UpdateExercisesCommandHandler` and `SubstituteExerciseCommandHandler` result DTOs gain `IReadOnlyList<VolumeWarningDto> Warnings` and `bool OverTimeBudget` (warn, never block).
- Modify: `src/A2S.Api/Controllers/DaysController.cs` — `GET {id:guid}/days/{day:int}/budget?week=`.
- Modify: `ExerciseLibraryDto`/`ExerciseTemplateDto` gain `string MuscleGroup` (already) and `IReadOnlyList<string> SecondaryMuscleGroups`.
- Test: `tests/A2S.Application.Tests/Queries/GetDayBudgetQueryHandlerTests.cs`; `tests/A2S.Api.Tests/Integration/DayBudgetControllerTests.cs` (create the 4-day template workout → budget for day 1 returns a time estimate and a sets map).

- [ ] Implement → PASS. Commit.

---

## Integration (done by the orchestrator, in order)

1. Merge PR 2 branch; `dotnet ef migrations add AddTrainingPlan`; review Up/Down; run `MigratedDatabase`-based tests and API tests. Push, open PR.
2. Rebase/merge PR 3 onto it; `dotnet ef migrations add AddHabitsAndBodyWeight`; swap `NullPlanCheckpointSource` for a `TrainingPlan`-backed one; run suites. Push, PR.
3. Rebase/merge PR 4; `dotnet ef migrations add AddSecondaryMuscleGroups`; run suites. Push, PR.
