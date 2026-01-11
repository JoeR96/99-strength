# A2S Workout Tracking Application - Strategic Implementation Plan

## Phase 2: Strategic Planning Document

**Version:** 1.0
**Date:** 2026-01-11
**Based On:** Phase 1 Research Documentation

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Domain Layer Design](#2-domain-layer-design)
3. [Progression Strategies Implementation](#3-progression-strategies-implementation)
4. [Application Layer Design](#4-application-layer-design)
5. [Infrastructure Layer Design](#5-infrastructure-layer-design)
6. [Phased Implementation Plan](#6-phased-implementation-plan)
7. [Data Model and Persistence Strategy](#7-data-model-and-persistence-strategy)
8. [Testing Strategy](#8-testing-strategy)
9. [Key Design Decisions](#9-key-design-decisions)
10. [Implementation Sequence](#10-implementation-sequence)
11. [Success Criteria](#11-success-criteria)
12. [Risk Mitigation](#12-risk-mitigation)

---

## 1. Architecture Overview

### 1.1 Clean Architecture / DDD Layer Structure

The application follows Clean Architecture principles with Domain-Driven Design tactical patterns. Dependencies flow inward, with the Domain layer at the center having no external dependencies.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              API / Web Layer                                  │
│                                                                               │
│  ┌─────────────────────────────────┐  ┌─────────────────────────────────┐   │
│  │    ASP.NET Core Web API         │  │       React (TypeScript)        │   │
│  │    - Controllers                │  │       - Components              │   │
│  │    - Middleware                 │  │       - State Management        │   │
│  │    - DI Configuration           │  │       - API Client              │   │
│  └─────────────────────────────────┘  └─────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Application Layer                                  │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │    Commands      │  │     Queries      │  │      Validators          │   │
│  │    (MediatR)     │  │    (MediatR)     │  │   (FluentValidation)     │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │    Handlers      │  │      DTOs        │  │    Pipeline Behaviors    │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Domain Layer                                     │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │   Aggregates     │  │  Value Objects   │  │    Domain Events         │   │
│  │   - Workout      │  │  - TrainingMax   │  │    - WorkoutCreated      │   │
│  │   - Exercise     │  │  - RepRange      │  │    - DayCompleted        │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │ Domain Services  │  │   Interfaces     │  │    Specifications        │   │
│  │ - ISetCalcSvc    │  │ - IWorkoutRepo   │  │    (Business Rules)      │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Infrastructure Layer                                │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │   EF Core 9      │  │   Repositories   │  │    Unit of Work          │   │
│  │   PostgreSQL     │  │   Implementations│  │    Implementation        │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
│                                                                               │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────────┐   │
│  │ Domain Service   │  │  Entity Configs  │  │   External Services      │   │
│  │ Implementations  │  │   (Fluent API)   │  │   (if needed)            │   │
│  └──────────────────┘  └──────────────────┘  └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **API** | ASP.NET Core 9 Web API | RESTful endpoints, DI container |
| **Web** | React + TypeScript | Desktop browser SPA |
| **Application** | MediatR 12.x | CQRS command/query handlers |
| **Validation** | FluentValidation | Request validation pipeline |
| **Domain** | Pure C# | Business logic, no dependencies |
| **Persistence** | EF Core 9 + PostgreSQL | ORM and database |
| **Mapping** | Manual in handlers | Domain to DTO transformation |

### 1.3 Solution Structure

```
src/
├── A2S.Domain/
│   ├── Aggregates/
│   │   └── Workout/
│   │       ├── Workout.cs                    # Aggregate Root
│   │       ├── Exercise.cs                   # Entity
│   │       └── ExerciseProgression/
│   │           ├── ExerciseProgression.cs    # Abstract base
│   │           ├── LinearProgressionStrategy.cs
│   │           └── RepsPerSetStrategy.cs
│   ├── ValueObjects/
│   │   ├── TrainingMax.cs
│   │   ├── RepRange.cs
│   │   ├── PlannedSet.cs
│   │   ├── CompletedSet.cs
│   │   ├── WorkoutActivity.cs
│   │   └── ExercisePerformance.cs
│   ├── Events/
│   │   ├── WorkoutCreated.cs
│   │   ├── DayCompleted.cs
│   │   ├── WeekProgressed.cs
│   │   └── TrainingMaxAdjusted.cs
│   ├── Services/
│   │   ├── ISetCalculationService.cs
│   │   └── IProgressionService.cs
│   ├── Repositories/
│   │   ├── IWorkoutRepository.cs
│   │   └── IUnitOfWork.cs
│   └── Common/
│       ├── Entity.cs
│       ├── AggregateRoot.cs
│       └── ValueObject.cs
│
├── A2S.Application/
│   ├── Commands/
│   │   ├── CreateWorkout/
│   │   │   ├── CreateWorkoutCommand.cs
│   │   │   ├── CreateWorkoutHandler.cs
│   │   │   └── CreateWorkoutValidator.cs
│   │   ├── CompleteDay/
│   │   ├── ProgressToNextWeek/
│   │   ├── AdjustTrainingMax/
│   │   └── UpdateStartingWeight/
│   ├── Queries/
│   │   ├── GetCurrentWeek/
│   │   ├── GetWorkoutHistory/
│   │   ├── GetExerciseProgress/
│   │   └── GetPlannedSets/
│   ├── DTOs/
│   ├── Common/
│   │   └── Behaviors/
│   │       ├── ValidationBehavior.cs
│   │       └── LoggingBehavior.cs
│   └── DependencyInjection.cs
│
├── A2S.Infrastructure/
│   ├── Persistence/
│   │   ├── WorkoutDbContext.cs
│   │   ├── Configurations/
│   │   │   ├── WorkoutConfiguration.cs
│   │   │   ├── ExerciseConfiguration.cs
│   │   │   └── ExerciseProgressionConfiguration.cs
│   │   ├── Repositories/
│   │   │   └── WorkoutRepository.cs
│   │   └── UnitOfWork.cs
│   ├── Services/
│   │   ├── LinearProgressionCalculator.cs
│   │   └── RepsPerSetCalculator.cs
│   └── DependencyInjection.cs
│
├── A2S.Api/
│   ├── Controllers/
│   │   ├── WorkoutsController.cs
│   │   └── ExercisesController.cs
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   └── Program.cs
│
└── A2S.Web/
    ├── src/
    │   ├── components/
    │   ├── pages/
    │   ├── hooks/
    │   ├── services/
    │   └── types/
    └── package.json

tests/
├── A2S.Domain.Tests/
├── A2S.Application.Tests/
├── A2S.Infrastructure.Tests/
└── A2S.Api.Tests/
```

### 1.4 Cross-Cutting Concerns

#### Validation
- **Request Level**: FluentValidation in MediatR pipeline
- **Domain Level**: Invariant enforcement in aggregate methods
- **Example Flow**: `CreateWorkoutCommand` → `ValidationBehavior<T>` → `CreateWorkoutHandler`

#### Logging
- Structured logging with Serilog
- MediatR `LoggingBehavior<T>` for command/query tracing
- Domain events logged for audit trail

#### Transactions
- Unit of Work pattern with EF Core
- Single `SaveChangesAsync()` per command
- Domain events dispatched after successful commit

#### Exception Handling
- Global exception middleware in API
- Domain exceptions mapped to HTTP status codes
- Validation failures return 400 with problem details

---

## 2. Domain Layer Design

### 2.1 Aggregate: Workout (Aggregate Root)

The `Workout` aggregate is the central entity that encapsulates all workout-related state and behavior for a training program.

**Reference:** `research/domain-model.md` Section "Aggregate Structure"

```csharp
public sealed class Workout : AggregateRoot
{
    private readonly List<Exercise> _exercises = new();

    public WorkoutId Id { get; private set; }
    public string Name { get; private set; }
    public ProgramVariant Variant { get; private set; }
    public int TotalWeeks { get; private set; }
    public int CurrentWeek { get; private set; }
    public int CurrentBlock { get; private set; }
    public WorkoutStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();

    // Factory method - enforces creation invariants
    public static Workout Create(
        string name,
        ProgramVariant variant,
        IEnumerable<ExerciseDefinition> exercises)
    {
        // Validation and creation logic
    }

    // Domain methods
    public void Start();
    public void CompleteDay(DayNumber day, IEnumerable<ExercisePerformance> performances);
    public void ProgressToNextWeek();
    public void Pause();
    public void Resume();
    public void AdjustTrainingMax(ExerciseId exerciseId, TrainingMax newTm);

    // Query methods
    public IEnumerable<PlannedSet> GetPlannedSetsForDay(DayNumber day);
    public bool IsDeloadWeek();
    public int GetCurrentBlockNumber();
}
```

### 2.2 Entity: Exercise

An `Exercise` within a workout program, containing its configuration and progression strategy.

```csharp
public sealed class Exercise : Entity
{
    public ExerciseId Id { get; private set; }
    public string Name { get; private set; }
    public ExerciseCategory Category { get; private set; }
    public EquipmentType Equipment { get; private set; }
    public DayNumber AssignedDay { get; private set; }
    public int OrderInDay { get; private set; }

    // Polymorphic progression strategy (owned)
    public ExerciseProgression Progression { get; private set; }

    // Domain methods
    public void ApplyProgression(ExercisePerformance performance);
    public IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber);
    public void UpdateStartingWeight(Weight weight);
}
```

### 2.3 Entity: ExerciseProgression (Polymorphic Base)

Abstract base class for progression strategies. Implemented as TPH (Table Per Hierarchy) in EF Core.

```csharp
public abstract class ExerciseProgression : Entity
{
    public ExerciseProgressionId Id { get; protected set; }
    public string ProgressionType { get; protected set; } // Discriminator

    // Abstract methods for polymorphic behavior
    public abstract IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber);
    public abstract void ApplyPerformanceResult(ExercisePerformance performance);
    public abstract ProgressionSummary GetSummary();
}
```

### 2.4 Concrete Strategy: LinearProgressionStrategy

**Reference:** `research/business-rules.md` Section 3.1 "RTF Progression"

```csharp
public sealed class LinearProgressionStrategy : ExerciseProgression
{
    public TrainingMax TrainingMax { get; private set; }
    public bool UseAmrap { get; private set; }
    public int BaseSetsPerExercise { get; private set; }

    // Calculated from week/block
    private decimal GetIntensityPercentage(int weekNumber, int blockNumber);
    private int GetTargetReps(int weekNumber, int blockNumber);

    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        var intensity = GetIntensityPercentage(weekNumber, blockNumber);
        var targetReps = GetTargetReps(weekNumber, blockNumber);
        var workingWeight = TrainingMax.CalculateWorkingWeight(intensity);

        var sets = new List<PlannedSet>();
        for (int i = 1; i <= BaseSetsPerExercise; i++)
        {
            bool isAmrap = UseAmrap && i == BaseSetsPerExercise;
            sets.Add(new PlannedSet(i, workingWeight, targetReps, isAmrap));
        }
        return sets;
    }

    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        if (!UseAmrap) return;

        var amrapSet = performance.CompletedSets.Last();
        var targetReps = performance.PlannedSets.Last().TargetReps;
        var delta = amrapSet.ActualReps - targetReps;

        var adjustment = CalculateAdjustmentFromDelta(delta);
        TrainingMax = TrainingMax.ApplyAdjustment(adjustment);

        AddDomainEvent(new TrainingMaxAdjusted(Id, TrainingMax, adjustment, delta));
    }

    // Delta table from business-rules.md
    private TrainingMaxAdjustment CalculateAdjustmentFromDelta(int delta)
    {
        return delta switch
        {
            >= 5 => TrainingMaxAdjustment.Percentage(0.03m),
            4 => TrainingMaxAdjustment.Percentage(0.02m),
            3 => TrainingMaxAdjustment.Percentage(0.015m),
            2 => TrainingMaxAdjustment.Percentage(0.01m),
            1 => TrainingMaxAdjustment.Percentage(0.005m),
            0 => TrainingMaxAdjustment.None,
            -1 => TrainingMaxAdjustment.Percentage(-0.02m),
            _ => TrainingMaxAdjustment.Percentage(-0.05m) // -2 or worse
        };
    }
}
```

### 2.5 Concrete Strategy: RepsPerSetStrategy

**Reference:** `research/business-rules.md` Section 3.3 "Reps Per Set Progression"

```csharp
public sealed class RepsPerSetStrategy : ExerciseProgression
{
    public RepRange RepRange { get; private set; }  // min-target-max (e.g., 8-10-12)
    public int CurrentSetCount { get; private set; }
    public int StartingSets { get; private set; }
    public int TargetSets { get; private set; }
    public Weight CurrentWeight { get; private set; }
    public EquipmentType Equipment { get; private set; }

    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        var sets = new List<PlannedSet>();
        for (int i = 1; i <= CurrentSetCount; i++)
        {
            sets.Add(new PlannedSet(i, CurrentWeight, RepRange.Target, isAmrap: false));
        }
        return sets;
    }

    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        var evaluation = EvaluatePerformance(performance);

        switch (evaluation)
        {
            case PerformanceEvaluation.Success:
                if (CurrentSetCount < TargetSets)
                {
                    CurrentSetCount++;
                }
                else
                {
                    CurrentWeight = CurrentWeight.Add(GetWeightIncrement());
                    CurrentSetCount = StartingSets;
                }
                break;

            case PerformanceEvaluation.Failed:
                if (CurrentSetCount > 1)
                {
                    CurrentSetCount--;
                }
                else
                {
                    CurrentWeight = CurrentWeight.Subtract(GetWeightIncrement());
                }
                break;

            case PerformanceEvaluation.Maintained:
                // No change
                break;
        }
    }

    private PerformanceEvaluation EvaluatePerformance(ExercisePerformance performance)
    {
        var allHitMax = performance.CompletedSets.All(s => s.ActualReps >= RepRange.Maximum);
        if (allHitMax) return PerformanceEvaluation.Success;

        var anyBelowMin = performance.CompletedSets.Any(s => s.ActualReps < RepRange.Minimum);
        if (anyBelowMin) return PerformanceEvaluation.Failed;

        return PerformanceEvaluation.Maintained;
    }

    private Weight GetWeightIncrement()
    {
        return Equipment switch
        {
            EquipmentType.Dumbbell when CurrentWeight.Value < 10 => Weight.Kilograms(1),
            EquipmentType.Dumbbell => Weight.Kilograms(2),
            _ => Weight.Kilograms(2.5m)  // Barbell, Cable, Machine
        };
    }
}
```

### 2.6 Value Objects

```csharp
// TrainingMax - Immutable weight value with TM-specific operations
public sealed class TrainingMax : ValueObject
{
    public decimal Value { get; }
    public WeightUnit Unit { get; }

    public Weight CalculateWorkingWeight(decimal intensityPercentage)
    {
        var calculated = Value * intensityPercentage;
        return Weight.Create(RoundToIncrement(calculated, 2.5m), Unit);
    }

    public TrainingMax ApplyAdjustment(TrainingMaxAdjustment adjustment)
    {
        var newValue = adjustment.Type switch
        {
            AdjustmentType.Percentage => Value * (1 + adjustment.Amount),
            AdjustmentType.Absolute => Value + adjustment.Amount,
            _ => Value
        };
        return new TrainingMax(RoundToIncrement(newValue, 2.5m), Unit);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }
}

// RepRange - Min/Target/Max rep range for accessories
public sealed class RepRange : ValueObject
{
    public int Minimum { get; }
    public int Target { get; }
    public int Maximum { get; }

    public RepRange(int minimum, int target, int maximum)
    {
        if (minimum >= target || target >= maximum)
            throw new DomainException("Invalid rep range: min < target < max required");
        if (minimum < 1 || maximum > 30)
            throw new DomainException("Rep range must be between 1-30");

        Minimum = minimum;
        Target = target;
        Maximum = maximum;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Minimum;
        yield return Target;
        yield return Maximum;
    }
}

// PlannedSet - Immutable planned set data
public sealed class PlannedSet : ValueObject
{
    public int SetNumber { get; }
    public Weight TargetWeight { get; }
    public int TargetReps { get; }
    public bool IsAmrap { get; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SetNumber;
        yield return TargetWeight;
        yield return TargetReps;
        yield return IsAmrap;
    }
}

// CompletedSet - Recorded set with actual performance
public sealed class CompletedSet : ValueObject
{
    public int SetNumber { get; }
    public Weight Weight { get; }
    public int ActualReps { get; }
    public bool WasAmrap { get; }
    public int? Rpe { get; }  // Optional RPE 1-10

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SetNumber;
        yield return Weight;
        yield return ActualReps;
        yield return WasAmrap;
    }
}

// ExercisePerformance - Complete exercise performance for a day
public sealed class ExercisePerformance : ValueObject
{
    public ExerciseId ExerciseId { get; }
    public IReadOnlyList<PlannedSet> PlannedSets { get; }
    public IReadOnlyList<CompletedSet> CompletedSets { get; }
    public DateTime CompletedAt { get; }
    public string? Notes { get; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ExerciseId;
        yield return CompletedAt;
        foreach (var set in CompletedSets)
            yield return set;
    }
}

// WorkoutActivity - Recorded day activity (value object stored in Workout)
public sealed class WorkoutActivity : ValueObject
{
    public int WeekNumber { get; }
    public DayNumber Day { get; }
    public IReadOnlyList<ExercisePerformance> Performances { get; }
    public DateTime CompletedAt { get; }
    public WorkoutActivityStatus Status { get; }  // Completed, Skipped

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return WeekNumber;
        yield return Day;
        yield return CompletedAt;
    }
}
```

### 2.7 Domain Events

**Reference:** `research/domain-model.md` Section "Domain Events"

```csharp
public sealed record WorkoutCreated(
    WorkoutId WorkoutId,
    string Name,
    ProgramVariant Variant,
    int ExerciseCount) : IDomainEvent;

public sealed record DayCompleted(
    WorkoutId WorkoutId,
    int WeekNumber,
    DayNumber Day,
    IReadOnlyList<ExercisePerformance> Performances,
    DateTime CompletedAt) : IDomainEvent;

public sealed record WeekProgressed(
    WorkoutId WorkoutId,
    int FromWeek,
    int ToWeek,
    int NewBlockNumber,
    bool WasDeloadWeek) : IDomainEvent;

public sealed record TrainingMaxAdjusted(
    ExerciseProgressionId ProgressionId,
    TrainingMax NewTrainingMax,
    TrainingMaxAdjustment Adjustment,
    int RepDelta) : IDomainEvent;
```

### 2.8 Domain Service Interfaces

```csharp
public interface ISetCalculationService
{
    IEnumerable<PlannedSet> CalculatePlannedSetsForExercise(
        ExerciseProgression progression,
        int weekNumber,
        int blockNumber);

    Weight CalculateWorkingWeight(TrainingMax tm, decimal intensityPercentage);
}

public interface IProgressionService
{
    void ApplyLinearProgression(
        LinearProgressionStrategy strategy,
        ExercisePerformance performance);

    void ApplyRepsPerSetProgression(
        RepsPerSetStrategy strategy,
        ExercisePerformance performance);

    TrainingMaxAdjustment CalculateAmrapAdjustment(int targetReps, int actualReps);
}
```

### 2.9 Repository Interfaces

```csharp
public interface IWorkoutRepository
{
    Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct = default);
    Task<Workout?> GetActiveWorkoutAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Workout>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Workout workout, CancellationToken ct = default);
    void Update(Workout workout);
    void Remove(Workout workout);
}

public interface IUnitOfWork
{
    IWorkoutRepository Workouts { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

---

## 3. Progression Strategies Implementation

### 3.1 LinearProgressionStrategy (RTF with AMRAP)

**Reference:** `research/business-rules.md` Section 3.1

#### Algorithm Overview

The RTF (Reps To Failure) progression uses Training Max (TM) as the basis for calculating working weights. The final set is taken AMRAP (As Many Reps As Possible), and the TM adjusts based on performance vs. target.

#### Intensity and Rep Tables by Block

| Block | Weeks | Intensity Range | Rep Range |
|-------|-------|-----------------|-----------|
| 1 | 1-7 | 65-75% TM | 8-12 |
| 2 | 8-14 | 75-85% TM | 5-8 |
| 3 | 15-21 | 85-92% TM | 3-5 |

#### Delta Table for TM Adjustment

```csharp
public static class LinearProgressionConstants
{
    public static readonly IReadOnlyDictionary<int, decimal> AmrapDeltaTable =
        new Dictionary<int, decimal>
        {
            { 5, 0.03m },   // +5 or more reps: +3.0%
            { 4, 0.02m },   // +4 reps: +2.0%
            { 3, 0.015m },  // +3 reps: +1.5%
            { 2, 0.01m },   // +2 reps: +1.0%
            { 1, 0.005m },  // +1 rep: +0.5%
            { 0, 0.0m },    // Hit target: no change
            { -1, -0.02m }, // -1 rep: -2.0%
            { -2, -0.05m }, // -2 or worse: -5.0%
        };

    public static decimal GetAdjustmentPercentage(int repDelta)
    {
        if (repDelta >= 5) return AmrapDeltaTable[5];
        if (repDelta <= -2) return AmrapDeltaTable[-2];
        return AmrapDeltaTable[repDelta];
    }
}
```

#### Deload Week Handling

- Weeks 7, 14, 21 are deload weeks: `isDeload = weekNumber % 7 == 0`
- Deload uses 50-70% of normal volume
- **Policy**: AMRAP on deload does NOT adjust TM (configurable)

#### Implementation Flow

```
1. Get week/block parameters
2. Calculate intensity percentage from tables
3. Calculate working weight: TM * intensity
4. Round to nearest 2.5kg increment
5. Generate planned sets (n-1 straight sets + 1 AMRAP)
6. After completion:
   - Compare AMRAP reps to target
   - Look up delta in adjustment table
   - Apply adjustment to TM
   - Round new TM to increment
   - Raise TrainingMaxAdjusted event
```

### 3.2 RepsPerSetStrategy (Set-Building Progression)

**Reference:** `research/business-rules.md` Section 3.3

#### Algorithm Overview

Used for accessory exercises without percentage-based training. Users progressively add sets until reaching a target, then increase weight and reset sets.

#### Configuration Parameters

```csharp
public record RepsPerSetConfiguration
{
    public RepRange RepRange { get; init; }        // e.g., 8-10-12
    public int StartingSets { get; init; }         // e.g., 2
    public int TargetSets { get; init; }           // e.g., 4
    public Weight StartingWeight { get; init; }
    public EquipmentType Equipment { get; init; }
}
```

#### Performance Evaluation Logic

```csharp
public enum PerformanceEvaluation
{
    Success,    // All sets hit maximum reps
    Maintained, // All sets at least minimum reps
    Failed      // Any set below minimum reps
}

public PerformanceEvaluation Evaluate(IEnumerable<CompletedSet> sets, RepRange range)
{
    var allHitMax = sets.All(s => s.ActualReps >= range.Maximum);
    if (allHitMax) return PerformanceEvaluation.Success;

    var anyBelowMin = sets.Any(s => s.ActualReps < range.Minimum);
    if (anyBelowMin) return PerformanceEvaluation.Failed;

    return PerformanceEvaluation.Maintained;
}
```

#### Progression State Machine

```
                    ┌───────────────┐
                    │   SUCCESS     │
                    │ (All hit max) │
                    └───────┬───────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             │             │
    ┌──────────────────┐    │    ┌──────────────────┐
    │ sets < targetSets│    │    │ sets == targetSets│
    │                  │    │    │                   │
    │  Action: +1 set  │    │    │  Action: +weight  │
    │                  │    │    │          reset    │
    └──────────────────┘    │    └──────────────────┘
                            │
                    ┌───────┴───────┐
                    │   FAILED      │
                    │ (Below min)   │
                    └───────┬───────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             │             │
    ┌──────────────────┐    │    ┌──────────────────┐
    │   sets > 1       │    │    │   sets == 1      │
    │                  │    │    │                  │
    │  Action: -1 set  │    │    │  Action: -weight │
    │                  │    │    │                  │
    └──────────────────┘    │    └──────────────────┘
                            │
                    ┌───────┴───────┐
                    │   MAINTAINED  │
                    │ (Met minimum) │
                    │               │
                    │  Action: none │
                    └───────────────┘
```

#### Equipment-Based Weight Increments

```csharp
public static class WeightIncrements
{
    public static Weight GetIncrement(EquipmentType equipment, Weight currentWeight)
    {
        return equipment switch
        {
            EquipmentType.Dumbbell when currentWeight.InKilograms < 10
                => Weight.Kilograms(1),
            EquipmentType.Dumbbell
                => Weight.Kilograms(2),
            EquipmentType.Barbell
                => Weight.Kilograms(2.5m),
            EquipmentType.Cable or EquipmentType.Machine
                => Weight.Kilograms(2.5m),  // Often configurable per machine
            EquipmentType.SmithMachine
                => Weight.Kilograms(2.5m),
            EquipmentType.Bodyweight
                => Weight.Kilograms(0),     // No weight progression
            _ => Weight.Kilograms(2.5m)
        };
    }
}
```

#### Week 1 Special Handling

For Reps Per Set exercises in Week 1:
1. User MUST provide starting weight (cannot be pre-calculated)
2. System records weight as baseline
3. No progression applied in Week 1 (establishing baseline)
4. Week 2+ follows normal progression logic

---

## 4. Application Layer Design

### 4.1 CQRS Pattern with MediatR

Commands modify state, queries read state. Handlers contain orchestration logic and map between domain and DTOs.

#### Pipeline Configuration

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
```

### 4.2 Commands

#### CreateWorkout

```csharp
public sealed record CreateWorkoutCommand : IRequest<WorkoutDto>
{
    public string Name { get; init; } = string.Empty;
    public ProgramVariant Variant { get; init; }
    public IReadOnlyList<CreateExerciseDto> Exercises { get; init; } = [];
}

public sealed record CreateExerciseDto
{
    public string Name { get; init; } = string.Empty;
    public ExerciseCategory Category { get; init; }
    public EquipmentType Equipment { get; init; }
    public DayNumber AssignedDay { get; init; }
    public int OrderInDay { get; init; }

    // For Linear Progression
    public decimal? InitialTrainingMax { get; init; }
    public bool UseAmrap { get; init; } = true;

    // For Reps Per Set
    public RepRangeDto? RepRange { get; init; }
    public int? StartingSets { get; init; }
    public int? TargetSets { get; init; }
    public decimal? StartingWeight { get; init; }
}

public sealed class CreateWorkoutHandler : IRequestHandler<CreateWorkoutCommand, WorkoutDto>
{
    private readonly IUnitOfWork _uow;

    public async Task<WorkoutDto> Handle(CreateWorkoutCommand request, CancellationToken ct)
    {
        var exercises = request.Exercises.Select(MapToExercise).ToList();
        var workout = Workout.Create(request.Name, request.Variant, exercises);

        await _uow.Workouts.AddAsync(workout, ct);
        await _uow.SaveChangesAsync(ct);

        return MapToDto(workout);
    }

    private Exercise MapToExercise(CreateExerciseDto dto) { /* ... */ }
    private WorkoutDto MapToDto(Workout workout) { /* ... */ }
}
```

#### CompleteDay

```csharp
public sealed record CompleteDayCommand : IRequest<DayCompletionResultDto>
{
    public Guid WorkoutId { get; init; }
    public DayNumber Day { get; init; }
    public IReadOnlyList<ExercisePerformanceDto> Performances { get; init; } = [];
}

public sealed class CompleteDayHandler : IRequestHandler<CompleteDayCommand, DayCompletionResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IProgressionService _progressionService;

    public async Task<DayCompletionResultDto> Handle(CompleteDayCommand request, CancellationToken ct)
    {
        var workout = await _uow.Workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), ct)
            ?? throw new NotFoundException(nameof(Workout), request.WorkoutId);

        var performances = request.Performances.Select(MapToPerformance).ToList();

        workout.CompleteDay(request.Day, performances);

        await _uow.SaveChangesAsync(ct);

        return BuildResult(workout, performances);
    }
}
```

#### ProgressToNextWeek

```csharp
public sealed record ProgressToNextWeekCommand : IRequest<WeekProgressionResultDto>
{
    public Guid WorkoutId { get; init; }
}

public sealed class ProgressToNextWeekHandler : IRequestHandler<ProgressToNextWeekCommand, WeekProgressionResultDto>
{
    private readonly IUnitOfWork _uow;

    public async Task<WeekProgressionResultDto> Handle(ProgressToNextWeekCommand request, CancellationToken ct)
    {
        var workout = await _uow.Workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), ct)
            ?? throw new NotFoundException(nameof(Workout), request.WorkoutId);

        workout.ProgressToNextWeek();

        await _uow.SaveChangesAsync(ct);

        return new WeekProgressionResultDto
        {
            NewWeekNumber = workout.CurrentWeek,
            NewBlockNumber = workout.CurrentBlock,
            IsDeloadWeek = workout.IsDeloadWeek()
        };
    }
}
```

#### AdjustTrainingMax

```csharp
public sealed record AdjustTrainingMaxCommand : IRequest<TrainingMaxDto>
{
    public Guid WorkoutId { get; init; }
    public Guid ExerciseId { get; init; }
    public decimal NewTrainingMax { get; init; }
    public string? Reason { get; init; }
}
```

#### UpdateStartingWeight

For Reps Per Set exercises in Week 1.

```csharp
public sealed record UpdateStartingWeightCommand : IRequest<ExerciseDto>
{
    public Guid WorkoutId { get; init; }
    public Guid ExerciseId { get; init; }
    public decimal StartingWeight { get; init; }
}
```

### 4.3 Queries

#### GetCurrentWeek

```csharp
public sealed record GetCurrentWeekQuery : IRequest<CurrentWeekDto>
{
    public Guid WorkoutId { get; init; }
}

public sealed record CurrentWeekDto
{
    public int WeekNumber { get; init; }
    public int BlockNumber { get; init; }
    public bool IsDeloadWeek { get; init; }
    public IReadOnlyList<DayPlanDto> Days { get; init; } = [];
}

public sealed record DayPlanDto
{
    public DayNumber Day { get; init; }
    public DayStatus Status { get; init; }
    public IReadOnlyList<PlannedExerciseDto> Exercises { get; init; } = [];
}

public sealed record PlannedExerciseDto
{
    public Guid ExerciseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<PlannedSetDto> PlannedSets { get; init; } = [];
}
```

#### GetWorkoutHistory

```csharp
public sealed record GetWorkoutHistoryQuery : IRequest<WorkoutHistoryDto>
{
    public Guid WorkoutId { get; init; }
    public int? FromWeek { get; init; }
    public int? ToWeek { get; init; }
}
```

#### GetExerciseProgress

```csharp
public sealed record GetExerciseProgressQuery : IRequest<ExerciseProgressDto>
{
    public Guid WorkoutId { get; init; }
    public Guid ExerciseId { get; init; }
}

public sealed record ExerciseProgressDto
{
    public string ExerciseName { get; init; } = string.Empty;
    public ProgressionType ProgressionType { get; init; }

    // For Linear Progression
    public TrainingMaxHistoryDto? TrainingMaxHistory { get; init; }

    // For Reps Per Set
    public SetProgressionHistoryDto? SetProgressionHistory { get; init; }
}
```

#### GetPlannedSets

```csharp
public sealed record GetPlannedSetsQuery : IRequest<IReadOnlyList<PlannedSetDto>>
{
    public Guid WorkoutId { get; init; }
    public Guid ExerciseId { get; init; }
    public int? WeekNumber { get; init; }  // null = current week
}
```

### 4.4 DTOs and Mapping Strategy

Mapping is done manually in handlers, not using AutoMapper. This keeps dependencies minimal and mappings explicit.

```csharp
// In handler
private static WorkoutDto MapToDto(Workout workout)
{
    return new WorkoutDto
    {
        Id = workout.Id.Value,
        Name = workout.Name,
        Variant = workout.Variant,
        CurrentWeek = workout.CurrentWeek,
        CurrentBlock = workout.CurrentBlock,
        Status = workout.Status,
        Exercises = workout.Exercises.Select(MapExerciseToDto).ToList()
    };
}

private static ExerciseDto MapExerciseToDto(Exercise exercise)
{
    return new ExerciseDto
    {
        Id = exercise.Id.Value,
        Name = exercise.Name,
        Category = exercise.Category,
        Equipment = exercise.Equipment,
        AssignedDay = exercise.AssignedDay,
        Progression = MapProgressionToDto(exercise.Progression)
    };
}
```

### 4.5 FluentValidation Validators

```csharp
public sealed class CreateWorkoutCommandValidator : AbstractValidator<CreateWorkoutCommand>
{
    public CreateWorkoutCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Variant)
            .IsInEnum();

        RuleFor(x => x.Exercises)
            .NotEmpty()
            .WithMessage("At least one exercise is required");

        RuleForEach(x => x.Exercises)
            .SetValidator(new CreateExerciseValidator());
    }
}

public sealed class CreateExerciseValidator : AbstractValidator<CreateExerciseDto>
{
    public CreateExerciseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Category)
            .IsInEnum();

        When(x => x.Category == ExerciseCategory.MainLift ||
                  x.Category == ExerciseCategory.Auxiliary, () =>
        {
            RuleFor(x => x.InitialTrainingMax)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("Training Max required for main/auxiliary lifts");
        });

        When(x => x.Category == ExerciseCategory.Accessory, () =>
        {
            RuleFor(x => x.RepRange)
                .NotNull()
                .WithMessage("Rep range required for accessories");

            RuleFor(x => x.StartingSets)
                .NotNull()
                .InclusiveBetween(1, 10);

            RuleFor(x => x.TargetSets)
                .NotNull()
                .InclusiveBetween(1, 10)
                .GreaterThanOrEqualTo(x => x.StartingSets ?? 1);
        });
    }
}

public sealed class CompleteDayCommandValidator : AbstractValidator<CompleteDayCommand>
{
    public CompleteDayCommandValidator()
    {
        RuleFor(x => x.WorkoutId)
            .NotEmpty();

        RuleFor(x => x.Day)
            .IsInEnum();

        RuleFor(x => x.Performances)
            .NotEmpty()
            .WithMessage("At least one exercise performance required");

        RuleForEach(x => x.Performances)
            .SetValidator(new ExercisePerformanceValidator());
    }
}

public sealed class ExercisePerformanceValidator : AbstractValidator<ExercisePerformanceDto>
{
    public ExercisePerformanceValidator()
    {
        RuleFor(x => x.ExerciseId)
            .NotEmpty();

        RuleFor(x => x.CompletedSets)
            .NotEmpty();

        RuleForEach(x => x.CompletedSets)
            .ChildRules(set =>
            {
                set.RuleFor(s => s.ActualReps)
                    .GreaterThanOrEqualTo(0)
                    .LessThanOrEqualTo(50);

                set.RuleFor(s => s.Weight)
                    .GreaterThanOrEqualTo(0);
            });
    }
}
```

---

## 5. Infrastructure Layer Design

### 5.1 EF Core DbContext Configuration

```csharp
public sealed class WorkoutDbContext : DbContext
{
    public DbSet<Workout> Workouts => Set<Workout>();

    public WorkoutDbContext(DbContextOptions<WorkoutDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkoutDbContext).Assembly);

        // Global query filter for soft deletes if needed
        // modelBuilder.Entity<Workout>().HasQueryFilter(w => !w.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Dispatch domain events before saving
        var domainEvents = ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        var result = await base.SaveChangesAsync(ct);

        // Publish events after successful save (via MediatR or event bus)
        // await _domainEventDispatcher.DispatchAsync(domainEvents);

        return result;
    }
}
```

### 5.2 Entity Configurations

#### WorkoutConfiguration

```csharp
public sealed class WorkoutConfiguration : IEntityTypeConfiguration<Workout>
{
    public void Configure(EntityTypeBuilder<Workout> builder)
    {
        builder.ToTable("Workouts");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasConversion(
                id => id.Value,
                value => new WorkoutId(value));

        builder.Property(w => w.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.Variant)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // Exercises owned by Workout
        builder.HasMany(w => w.Exercises)
            .WithOne()
            .HasForeignKey("WorkoutId")
            .OnDelete(DeleteBehavior.Cascade);

        // WorkoutActivities as owned collection (JSON or separate table)
        builder.OwnsMany(w => w.Activities, ab =>
        {
            ab.ToTable("WorkoutActivities");
            ab.WithOwner().HasForeignKey("WorkoutId");
            ab.Property<int>("Id").ValueGeneratedOnAdd();
            ab.HasKey("Id");

            ab.Property(a => a.WeekNumber);
            ab.Property(a => a.Day).HasConversion<string>();
            ab.Property(a => a.CompletedAt);
            ab.Property(a => a.Status).HasConversion<string>();

            // Store performances as JSON column
            ab.Property(a => a.Performances)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOptions),
                    v => JsonSerializer.Deserialize<List<ExercisePerformance>>(v, JsonOptions)!);
        });

        // Optimistic concurrency
        builder.Property<uint>("RowVersion")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.CreatedAt);
    }
}
```

#### ExerciseConfiguration

```csharp
public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new ExerciseId(value));

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Equipment)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.AssignedDay)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Polymorphic progression - TPH
        builder.HasOne(e => e.Progression)
            .WithOne()
            .HasForeignKey<ExerciseProgression>("ExerciseId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### ExerciseProgressionConfiguration (TPH)

```csharp
public sealed class ExerciseProgressionConfiguration : IEntityTypeConfiguration<ExerciseProgression>
{
    public void Configure(EntityTypeBuilder<ExerciseProgression> builder)
    {
        builder.ToTable("ExerciseProgressions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new ExerciseProgressionId(value));

        // TPH discriminator
        builder.HasDiscriminator(p => p.ProgressionType)
            .HasValue<LinearProgressionStrategy>("Linear")
            .HasValue<RepsPerSetStrategy>("RepsPerSet");

        builder.Property(p => p.ProgressionType)
            .HasMaxLength(50);
    }
}

public sealed class LinearProgressionStrategyConfiguration : IEntityTypeConfiguration<LinearProgressionStrategy>
{
    public void Configure(EntityTypeBuilder<LinearProgressionStrategy> builder)
    {
        // TrainingMax as owned entity
        builder.OwnsOne(l => l.TrainingMax, tm =>
        {
            tm.Property(t => t.Value)
                .HasColumnName("TrainingMaxValue")
                .HasPrecision(10, 2);

            tm.Property(t => t.Unit)
                .HasColumnName("TrainingMaxUnit")
                .HasConversion<string>()
                .HasMaxLength(10);
        });

        builder.Property(l => l.UseAmrap);
        builder.Property(l => l.BaseSetsPerExercise);
    }
}

public sealed class RepsPerSetStrategyConfiguration : IEntityTypeConfiguration<RepsPerSetStrategy>
{
    public void Configure(EntityTypeBuilder<RepsPerSetStrategy> builder)
    {
        // RepRange as owned entity
        builder.OwnsOne(r => r.RepRange, rr =>
        {
            rr.Property(x => x.Minimum).HasColumnName("RepRangeMin");
            rr.Property(x => x.Target).HasColumnName("RepRangeTarget");
            rr.Property(x => x.Maximum).HasColumnName("RepRangeMax");
        });

        // CurrentWeight as owned entity
        builder.OwnsOne(r => r.CurrentWeight, w =>
        {
            w.Property(x => x.Value)
                .HasColumnName("CurrentWeightValue")
                .HasPrecision(10, 2);
            w.Property(x => x.Unit)
                .HasColumnName("CurrentWeightUnit")
                .HasConversion<string>()
                .HasMaxLength(10);
        });

        builder.Property(r => r.CurrentSetCount);
        builder.Property(r => r.StartingSets);
        builder.Property(r => r.TargetSets);
        builder.Property(r => r.Equipment).HasConversion<string>();
    }
}
```

### 5.3 Repository Implementation

```csharp
public sealed class WorkoutRepository : IWorkoutRepository
{
    private readonly WorkoutDbContext _context;

    public WorkoutRepository(WorkoutDbContext context)
    {
        _context = context;
    }

    public async Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Workout?> GetActiveWorkoutAsync(CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
                .ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Status == WorkoutStatus.Active, ct);
    }

    public async Task<IReadOnlyList<Workout>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Workouts
            .Include(w => w.Exercises)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Workout workout, CancellationToken ct = default)
    {
        await _context.Workouts.AddAsync(workout, ct);
    }

    public void Update(Workout workout)
    {
        _context.Workouts.Update(workout);
    }

    public void Remove(Workout workout)
    {
        _context.Workouts.Remove(workout);
    }
}
```

### 5.4 Unit of Work Implementation

```csharp
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly WorkoutDbContext _context;
    private IWorkoutRepository? _workouts;

    public UnitOfWork(WorkoutDbContext context)
    {
        _context = context;
    }

    public IWorkoutRepository Workouts =>
        _workouts ??= new WorkoutRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
```

### 5.5 Domain Service Implementations

#### LinearProgressionCalculator

```csharp
public sealed class LinearProgressionCalculator : IProgressionService
{
    private static readonly IReadOnlyDictionary<(int Block, int WeekInBlock), (decimal Intensity, int TargetReps)>
        WeekParameters = new Dictionary<(int, int), (decimal, int)>
        {
            // Block 1 (weeks 1-7, or week-in-block 1-7)
            { (1, 1), (0.70m, 10) },
            { (1, 2), (0.72m, 9) },
            { (1, 3), (0.74m, 8) },
            { (1, 4), (0.75m, 8) },
            { (1, 5), (0.73m, 9) },
            { (1, 6), (0.71m, 10) },
            { (1, 7), (0.65m, 6) },  // Deload

            // Block 2 (weeks 8-14)
            { (2, 1), (0.78m, 7) },
            { (2, 2), (0.80m, 6) },
            { (2, 3), (0.82m, 5) },
            { (2, 4), (0.83m, 5) },
            { (2, 5), (0.81m, 6) },
            { (2, 6), (0.79m, 7) },
            { (2, 7), (0.70m, 5) },  // Deload

            // Block 3 (weeks 15-21)
            { (3, 1), (0.85m, 4) },
            { (3, 2), (0.87m, 4) },
            { (3, 3), (0.89m, 3) },
            { (3, 4), (0.90m, 3) },
            { (3, 5), (0.88m, 4) },
            { (3, 6), (0.86m, 4) },
            { (3, 7), (0.75m, 3) },  // Deload/Test
        };

    public (decimal Intensity, int TargetReps) GetWeekParameters(int weekNumber)
    {
        var block = ((weekNumber - 1) / 7) + 1;
        var weekInBlock = ((weekNumber - 1) % 7) + 1;

        return WeekParameters.TryGetValue((block, weekInBlock), out var p)
            ? p
            : (0.75m, 5); // Default fallback
    }

    public TrainingMaxAdjustment CalculateAmrapAdjustment(int targetReps, int actualReps)
    {
        var delta = actualReps - targetReps;
        var percentage = LinearProgressionConstants.GetAdjustmentPercentage(delta);
        return TrainingMaxAdjustment.Percentage(percentage);
    }

    public void ApplyLinearProgression(
        LinearProgressionStrategy strategy,
        ExercisePerformance performance)
    {
        strategy.ApplyPerformanceResult(performance);
    }

    public void ApplyRepsPerSetProgression(
        RepsPerSetStrategy strategy,
        ExercisePerformance performance)
    {
        strategy.ApplyPerformanceResult(performance);
    }
}
```

#### RepsPerSetCalculator

```csharp
public sealed class RepsPerSetCalculator
{
    public PerformanceEvaluation EvaluatePerformance(
        IEnumerable<CompletedSet> completedSets,
        RepRange repRange)
    {
        var sets = completedSets.ToList();

        if (sets.Count == 0)
            return PerformanceEvaluation.Failed;

        var allHitMax = sets.All(s => s.ActualReps >= repRange.Maximum);
        if (allHitMax)
            return PerformanceEvaluation.Success;

        var anyBelowMin = sets.Any(s => s.ActualReps < repRange.Minimum);
        if (anyBelowMin)
            return PerformanceEvaluation.Failed;

        return PerformanceEvaluation.Maintained;
    }

    public Weight GetWeightIncrement(EquipmentType equipment, Weight currentWeight)
    {
        return WeightIncrements.GetIncrement(equipment, currentWeight);
    }

    public (int NewSetCount, Weight NewWeight) CalculateProgression(
        PerformanceEvaluation evaluation,
        int currentSetCount,
        int startingSets,
        int targetSets,
        Weight currentWeight,
        EquipmentType equipment)
    {
        var increment = GetWeightIncrement(equipment, currentWeight);

        return evaluation switch
        {
            PerformanceEvaluation.Success when currentSetCount < targetSets
                => (currentSetCount + 1, currentWeight),

            PerformanceEvaluation.Success
                => (startingSets, currentWeight.Add(increment)),

            PerformanceEvaluation.Failed when currentSetCount > 1
                => (currentSetCount - 1, currentWeight),

            PerformanceEvaluation.Failed
                => (currentSetCount, currentWeight.Subtract(increment)),

            _ => (currentSetCount, currentWeight)
        };
    }
}
```

### 5.6 Database Schema

```sql
-- Workouts table
CREATE TABLE "Workouts" (
    "Id" UUID PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Variant" VARCHAR(50) NOT NULL,
    "TotalWeeks" INTEGER NOT NULL DEFAULT 21,
    "CurrentWeek" INTEGER NOT NULL DEFAULT 1,
    "CurrentBlock" INTEGER NOT NULL DEFAULT 1,
    "Status" VARCHAR(50) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL,
    "StartedAt" TIMESTAMPTZ,
    "RowVersion" INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX "IX_Workouts_Status" ON "Workouts" ("Status");
CREATE INDEX "IX_Workouts_CreatedAt" ON "Workouts" ("CreatedAt" DESC);

-- Exercises table
CREATE TABLE "Exercises" (
    "Id" UUID PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "Workouts"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(100) NOT NULL,
    "Category" VARCHAR(50) NOT NULL,
    "Equipment" VARCHAR(50) NOT NULL,
    "AssignedDay" VARCHAR(20) NOT NULL,
    "OrderInDay" INTEGER NOT NULL
);

CREATE INDEX "IX_Exercises_WorkoutId" ON "Exercises" ("WorkoutId");

-- ExerciseProgressions table (TPH)
CREATE TABLE "ExerciseProgressions" (
    "Id" UUID PRIMARY KEY,
    "ExerciseId" UUID NOT NULL UNIQUE REFERENCES "Exercises"("Id") ON DELETE CASCADE,
    "ProgressionType" VARCHAR(50) NOT NULL,

    -- LinearProgressionStrategy columns
    "TrainingMaxValue" DECIMAL(10, 2),
    "TrainingMaxUnit" VARCHAR(10),
    "UseAmrap" BOOLEAN,
    "BaseSetsPerExercise" INTEGER,

    -- RepsPerSetStrategy columns
    "RepRangeMin" INTEGER,
    "RepRangeTarget" INTEGER,
    "RepRangeMax" INTEGER,
    "CurrentSetCount" INTEGER,
    "StartingSets" INTEGER,
    "TargetSets" INTEGER,
    "CurrentWeightValue" DECIMAL(10, 2),
    "CurrentWeightUnit" VARCHAR(10),
    "Equipment" VARCHAR(50)
);

CREATE INDEX "IX_ExerciseProgressions_ExerciseId" ON "ExerciseProgressions" ("ExerciseId");
CREATE INDEX "IX_ExerciseProgressions_ProgressionType" ON "ExerciseProgressions" ("ProgressionType");

-- WorkoutActivities table (owned collection)
CREATE TABLE "WorkoutActivities" (
    "Id" SERIAL PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "Workouts"("Id") ON DELETE CASCADE,
    "WeekNumber" INTEGER NOT NULL,
    "Day" VARCHAR(20) NOT NULL,
    "CompletedAt" TIMESTAMPTZ NOT NULL,
    "Status" VARCHAR(50) NOT NULL,
    "Performances" JSONB NOT NULL
);

CREATE INDEX "IX_WorkoutActivities_WorkoutId" ON "WorkoutActivities" ("WorkoutId");
CREATE INDEX "IX_WorkoutActivities_WeekNumber" ON "WorkoutActivities" ("WeekNumber");

-- TrainingMaxHistory table (for audit trail)
CREATE TABLE "TrainingMaxHistory" (
    "Id" UUID PRIMARY KEY,
    "ExerciseProgressionId" UUID NOT NULL REFERENCES "ExerciseProgressions"("Id") ON DELETE CASCADE,
    "OldValue" DECIMAL(10, 2) NOT NULL,
    "NewValue" DECIMAL(10, 2) NOT NULL,
    "AdjustmentPercentage" DECIMAL(5, 4),
    "RepDelta" INTEGER,
    "Source" VARCHAR(50) NOT NULL, -- 'AMRAP', 'Manual', 'Initial'
    "RecordedAt" TIMESTAMPTZ NOT NULL
);

CREATE INDEX "IX_TrainingMaxHistory_ProgressionId" ON "TrainingMaxHistory" ("ExerciseProgressionId");
CREATE INDEX "IX_TrainingMaxHistory_RecordedAt" ON "TrainingMaxHistory" ("RecordedAt" DESC);
```

---

## 6. Phased Implementation Plan

### Phase 1: Foundation and Domain Core (Week 1-2)

**Objective:** Establish solution structure and implement core domain entities with full test coverage.

#### Deliverables

1. **Solution Structure**
   - Create .NET 9 solution with all projects
   - Configure project references and dependencies
   - Set up Directory.Build.props for shared settings

2. **Coding Standards Documentation**
   - Create CODING_STANDARDS.md
   - Define naming conventions
   - Establish code review checklist
   - Set up EditorConfig and .NET analyzers

3. **Base Classes**
   ```csharp
   // Entity.cs
   public abstract class Entity
   {
       public override bool Equals(object? obj);
       public override int GetHashCode();
       public static bool operator ==(Entity? left, Entity? right);
   }

   // AggregateRoot.cs
   public abstract class AggregateRoot : Entity
   {
       private readonly List<IDomainEvent> _domainEvents = new();
       public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
       protected void AddDomainEvent(IDomainEvent domainEvent);
       public IEnumerable<IDomainEvent> PopDomainEvents();
   }

   // ValueObject.cs
   public abstract class ValueObject
   {
       protected abstract IEnumerable<object> GetEqualityComponents();
       public override bool Equals(object? obj);
       public override int GetHashCode();
   }
   ```

4. **Core Value Objects**
   - `TrainingMax` with arithmetic operations
   - `Weight` with unit conversion
   - `RepRange` with validation
   - `PlannedSet` and `CompletedSet`
   - `ExercisePerformance`
   - `WorkoutActivity`
   - `TrainingMaxAdjustment`

#### Success Criteria
- All value objects have unit tests for equality and operations
- Base classes enforce invariants correctly
- Solution compiles with zero warnings
- Test coverage > 90% on domain primitives

---

### Phase 2: Workout Aggregate (Week 2-3)

**Objective:** Implement the complete Workout aggregate with all entities and invariants.

#### Deliverables

1. **Workout Aggregate Root**
   - Factory method with creation validation
   - State transitions (Created -> Active -> Completed)
   - CompleteDay method with performance recording
   - ProgressToNextWeek with block transitions
   - Invariant enforcement (single active program, sequential weeks)

2. **Exercise Entity**
   - Exercise creation and configuration
   - Day assignment and ordering
   - Relationship to progression strategy

3. **ExerciseProgression Hierarchy**
   - Abstract base with common interface
   - `LinearProgressionStrategy` implementation
   - `RepsPerSetStrategy` implementation
   - Polymorphic behavior tests

4. **Domain Events**
   - `WorkoutCreated`
   - `DayCompleted`
   - `WeekProgressed`
   - `TrainingMaxAdjusted`

5. **Business Rule Validation**
   - Week number bounds (1-21)
   - Block calculation correctness
   - Deload week detection
   - TM adjustment bounds (max +/- 10% per session)

#### Success Criteria
- Aggregate enforces all invariants from business-rules.md
- Domain events raised at correct points
- All progression algorithms match specification
- Integration tests for complete workflows
- Test coverage > 85% on aggregate

---

### Phase 3: Domain Services (Week 3-4)

**Objective:** Implement domain services for set calculation and progression logic.

#### Deliverables

1. **ISetCalculationService Implementation**
   - Calculate planned sets for any week/exercise
   - Working weight calculation with rounding
   - Intensity percentage lookup tables
   - Rep target calculation by block

2. **IProgressionService Implementation**
   - AMRAP delta table lookup
   - TM adjustment calculation
   - Reps Per Set evaluation logic
   - Set/weight progression state machine

3. **Progression Algorithm Tests**
   - Every delta value tested (-5 to +5)
   - Edge cases: 0 reps, 30+ reps
   - Rounding to nearest 2.5kg increment
   - Equipment-specific weight increments

4. **Performance Evaluation Logic**
   - Success/Maintained/Failed classification
   - Set addition/removal rules
   - Weight increase/decrease triggers

#### Success Criteria
- All examples from business-rules.md pass as tests
- Progression calculations match specification exactly
- Edge cases handled gracefully
- Performance evaluation logic fully tested

---

### Phase 4: Infrastructure - Persistence (Week 4-5)

**Objective:** Implement EF Core persistence with PostgreSQL.

#### Deliverables

1. **EF Core DbContext**
   - Entity configurations
   - Value object mappings (owned entities)
   - TPH configuration for progression strategies
   - Optimistic concurrency setup

2. **Entity Configurations**
   - `WorkoutConfiguration`
   - `ExerciseConfiguration`
   - `ExerciseProgressionConfiguration`
   - JSON column configuration for complex types

3. **Repository Implementations**
   - `WorkoutRepository` with eager loading
   - Query optimization (Include patterns)

4. **Unit of Work**
   - SaveChangesAsync with domain event dispatch
   - Transaction coordination

5. **Initial Migration**
   - Database schema creation
   - Seed data for testing
   - Migration script generation

#### Success Criteria
- All entities persist and load correctly
- TPH discrimination works for progression types
- Value objects stored appropriately (owned entities)
- Optimistic concurrency prevents lost updates
- Repository tests pass with in-memory/test database

---

### Phase 5: Application Layer (Week 5-6)

**Objective:** Implement CQRS commands, queries, and validation pipeline.

#### Deliverables

1. **MediatR Pipeline Setup**
   - `LoggingBehavior<T>` for request tracing
   - `ValidationBehavior<T>` for FluentValidation integration
   - Exception handling behavior

2. **Core Commands with Handlers**
   - `CreateWorkoutCommand/Handler`
   - `CompleteDayCommand/Handler`
   - `ProgressToNextWeekCommand/Handler`
   - `AdjustTrainingMaxCommand/Handler`
   - `UpdateStartingWeightCommand/Handler`

3. **Core Queries with Handlers**
   - `GetCurrentWeekQuery/Handler`
   - `GetWorkoutHistoryQuery/Handler`
   - `GetExerciseProgressQuery/Handler`
   - `GetPlannedSetsQuery/Handler`

4. **DTOs**
   - Request DTOs (command payloads)
   - Response DTOs (query results)
   - Manual mapping in handlers

5. **FluentValidation Validators**
   - All command validators
   - Nested validators for complex DTOs
   - Custom validation rules

#### Success Criteria
- All commands execute complete workflows
- Validation errors return proper problem details
- Queries return correctly shaped DTOs
- Handler tests cover happy path and error cases

---

### Phase 6: API and Web Layer (Week 6-8)

**Objective:** Implement ASP.NET Core API and React frontend scaffold.

#### Deliverables

1. **ASP.NET Core Web API**
   - `WorkoutsController` - CRUD and workflow endpoints
   - `ExercisesController` - Exercise-specific operations
   - Route conventions and versioning
   - OpenAPI/Swagger documentation

2. **Dependency Injection Setup**
   - Service registration extension methods
   - DbContext configuration
   - MediatR registration
   - Repository and service bindings

3. **Middleware**
   - Global exception handling
   - Request logging
   - CORS configuration
   - Response compression

4. **React Frontend Scaffold**
   - Vite + React + TypeScript setup
   - Project structure (pages, components, hooks, services)
   - API client with Axios/fetch
   - TypeScript types matching DTOs
   - Basic routing setup

5. **Initial UI Components**
   - Workout list/dashboard
   - Current week view
   - Day completion form
   - Exercise progress view

#### Success Criteria
- API endpoints return correct responses
- Swagger documentation complete
- Frontend communicates with API
- Basic workflow possible end-to-end
- Error handling displays user-friendly messages

---

## 7. Data Model and Persistence Strategy

### 7.1 Value Object Persistence

| Value Object | Strategy | Reason |
|--------------|----------|--------|
| `TrainingMax` | Owned Entity | Needs separate columns for value/unit |
| `Weight` | Owned Entity | Same as TrainingMax |
| `RepRange` | Owned Entity | Three related columns (min/target/max) |
| `PlannedSet` | JSON Column | Complex, read-heavy, rarely queried individually |
| `CompletedSet` | JSON Column | Same as PlannedSet |
| `ExercisePerformance` | JSON Column | Complex nested structure |
| `WorkoutActivity` | Separate Table + JSON | Need to query by week, performances as JSON |

### 7.2 Polymorphic Strategy Persistence (TPH)

Table Per Hierarchy chosen for `ExerciseProgression`:

**Advantages:**
- Single table, simpler queries
- No joins for polymorphic access
- EF Core handles discriminator automatically

**Table Structure:**
- Discriminator column: `ProgressionType`
- All strategy-specific columns nullable
- Index on discriminator for filtered queries

**Alternative Considered (TPC/TPT):**
- TPT would require joins for every access
- TPC complicates migrations
- TPH works well for 2-3 concrete types

### 7.3 Foreign Key Constraints and Cascades

```
Workout (1) ─────────────┬────────────────────────────── (*) WorkoutActivity
   │                     │ CASCADE DELETE
   │
   │ CASCADE DELETE
   │
   └──────────────────── (*) Exercise (1) ───────────── (1) ExerciseProgression
                                           │               CASCADE DELETE
                                           │
                                           └─────────────── (*) TrainingMaxHistory
                                                            CASCADE DELETE
```

### 7.4 Index Strategy

| Table | Index | Columns | Purpose |
|-------|-------|---------|---------|
| Workouts | IX_Status | Status | Filter active workouts |
| Workouts | IX_CreatedAt | CreatedAt DESC | Order by creation |
| Exercises | IX_WorkoutId | WorkoutId | FK lookup |
| ExerciseProgressions | IX_ExerciseId | ExerciseId | FK lookup |
| ExerciseProgressions | IX_ProgressionType | ProgressionType | TPH filtering |
| WorkoutActivities | IX_WorkoutId | WorkoutId | FK lookup |
| WorkoutActivities | IX_WeekNumber | WeekNumber | Week queries |
| TrainingMaxHistory | IX_ProgressionId | ExerciseProgressionId | History lookup |
| TrainingMaxHistory | IX_RecordedAt | RecordedAt DESC | Chronological queries |

---

## 8. Testing Strategy

### 8.1 Test Pyramid

```
                        /\
                       /  \        E2E Tests (5%)
                      /    \       - Full API workflow tests
                     /______\      - Browser automation (optional)
                    /        \
                   /          \    Integration Tests (20%)
                  /   INT      \   - Repository + DB tests
                 /   TESTS      \  - Handler + EF Core tests
                /______________\
               /                \
              /                  \  Unit Tests (75%)
             /     UNIT TESTS     \ - Domain entities
            /                      \ - Value objects
           /________________________\ - Progression algorithms
                                      - Validators
```

### 8.2 Unit Test Categories

#### Domain Logic Tests

```csharp
public class LinearProgressionStrategyTests
{
    [Theory]
    [InlineData(5, 0.03)]   // +5 or more = +3%
    [InlineData(4, 0.02)]   // +4 = +2%
    [InlineData(3, 0.015)]  // +3 = +1.5%
    [InlineData(2, 0.01)]   // +2 = +1%
    [InlineData(1, 0.005)]  // +1 = +0.5%
    [InlineData(0, 0.0)]    // 0 = no change
    [InlineData(-1, -0.02)] // -1 = -2%
    [InlineData(-2, -0.05)] // -2 or worse = -5%
    [InlineData(-5, -0.05)] // -5 = still -5%
    public void CalculateAmrapAdjustment_ReturnsCorrectPercentage(
        int repDelta,
        decimal expectedAdjustment)
    {
        // Arrange
        var strategy = CreateLinearProgressionStrategy(trainingMax: 100);

        // Act
        var adjustment = strategy.CalculateAdjustmentFromDelta(repDelta);

        // Assert
        Assert.Equal(expectedAdjustment, adjustment.Amount);
    }

    [Fact]
    public void ApplyPerformanceResult_UpdatesTrainingMax()
    {
        // Arrange
        var strategy = CreateLinearProgressionStrategy(trainingMax: 100);
        var performance = CreatePerformance(targetReps: 6, actualReps: 9); // +3 = +1.5%

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        Assert.Equal(101.5m, strategy.TrainingMax.Value);
    }
}

public class RepsPerSetStrategyTests
{
    [Fact]
    public void EvaluatePerformance_AllHitMax_ReturnsSuccess()
    {
        // Arrange
        var repRange = new RepRange(8, 10, 12);
        var completedSets = new[]
        {
            new CompletedSet(1, Weight.Kilograms(50), 12),
            new CompletedSet(2, Weight.Kilograms(50), 12),
            new CompletedSet(3, Weight.Kilograms(50), 12)
        };

        // Act
        var result = _calculator.EvaluatePerformance(completedSets, repRange);

        // Assert
        Assert.Equal(PerformanceEvaluation.Success, result);
    }

    [Theory]
    [InlineData(EquipmentType.Dumbbell, 8, 1)]   // < 10kg = 1kg increment
    [InlineData(EquipmentType.Dumbbell, 15, 2)]  // >= 10kg = 2kg increment
    [InlineData(EquipmentType.Barbell, 100, 2.5)] // Barbell = 2.5kg
    [InlineData(EquipmentType.Cable, 50, 2.5)]    // Cable = 2.5kg
    public void GetWeightIncrement_ReturnsCorrectIncrement(
        EquipmentType equipment,
        decimal currentWeight,
        decimal expectedIncrement)
    {
        // Act
        var increment = WeightIncrements.GetIncrement(
            equipment,
            Weight.Kilograms(currentWeight));

        // Assert
        Assert.Equal(expectedIncrement, increment.Value);
    }
}
```

#### Invariant Enforcement Tests

```csharp
public class WorkoutAggregateTests
{
    [Fact]
    public void CompleteDay_WhenWorkoutNotStarted_ThrowsDomainException()
    {
        // Arrange
        var workout = Workout.Create("Test", ProgramVariant.RTF, exercises);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            workout.CompleteDay(DayNumber.Monday, performances));
    }

    [Fact]
    public void ProgressToNextWeek_WhenWeekNotComplete_ThrowsDomainException()
    {
        // Arrange
        var workout = CreateStartedWorkout();
        // Don't complete any days

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            workout.ProgressToNextWeek());
    }

    [Fact]
    public void Create_WithNoExercises_ThrowsDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            Workout.Create("Test", ProgramVariant.RTF, Enumerable.Empty<ExerciseDefinition>()));
    }
}
```

### 8.3 Integration Tests

#### Repository Tests

```csharp
public class WorkoutRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly WorkoutDbContext _context;

    [Fact]
    public async Task GetByIdAsync_WithExercises_LoadsProgressions()
    {
        // Arrange
        var workout = CreateWorkoutWithExercises();
        await _context.Workouts.AddAsync(workout);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var repository = new WorkoutRepository(_context);

        // Act
        var loaded = await repository.GetByIdAsync(workout.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.NotEmpty(loaded.Exercises);
        Assert.All(loaded.Exercises, e => Assert.NotNull(e.Progression));
    }

    [Fact]
    public async Task SaveChangesAsync_WithConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange - Simulate concurrent modification
        // ...
    }
}
```

#### Handler Integration Tests

```csharp
public class CompleteDayHandlerTests : IClassFixture<ApplicationFixture>
{
    [Fact]
    public async Task Handle_ValidRequest_UpdatesTrainingMax()
    {
        // Arrange
        var workout = await CreateAndStartWorkout();
        var command = new CompleteDayCommand
        {
            WorkoutId = workout.Id,
            Day = DayNumber.Monday,
            Performances = new[]
            {
                new ExercisePerformanceDto
                {
                    ExerciseId = workout.Exercises.First().Id,
                    CompletedSets = new[]
                    {
                        new CompletedSetDto { SetNumber = 1, ActualReps = 8, Weight = 80 },
                        new CompletedSetDto { SetNumber = 2, ActualReps = 8, Weight = 80 },
                        new CompletedSetDto { SetNumber = 3, ActualReps = 8, Weight = 80 },
                        new CompletedSetDto { SetNumber = 4, ActualReps = 11, Weight = 80 } // AMRAP +3
                    }
                }
            }
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        var updatedWorkout = await _repository.GetByIdAsync(workout.Id);
        var progression = updatedWorkout.Exercises.First().Progression as LinearProgressionStrategy;
        Assert.Equal(101.5m, progression.TrainingMax.Value); // +1.5% adjustment
    }
}
```

### 8.4 Test Data Builders

```csharp
public class WorkoutBuilder
{
    private string _name = "Test Workout";
    private ProgramVariant _variant = ProgramVariant.RTF;
    private readonly List<Exercise> _exercises = new();
    private WorkoutStatus _status = WorkoutStatus.Created;

    public WorkoutBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorkoutBuilder WithVariant(ProgramVariant variant)
    {
        _variant = variant;
        return this;
    }

    public WorkoutBuilder WithLinearExercise(
        string name,
        decimal trainingMax,
        DayNumber day = DayNumber.Monday)
    {
        var exercise = new ExerciseBuilder()
            .WithName(name)
            .WithLinearProgression(trainingMax)
            .OnDay(day)
            .Build();
        _exercises.Add(exercise);
        return this;
    }

    public WorkoutBuilder WithRepsPerSetExercise(
        string name,
        RepRange repRange,
        decimal startingWeight,
        DayNumber day = DayNumber.Monday)
    {
        var exercise = new ExerciseBuilder()
            .WithName(name)
            .WithRepsPerSetProgression(repRange, startingWeight)
            .OnDay(day)
            .Build();
        _exercises.Add(exercise);
        return this;
    }

    public WorkoutBuilder Started()
    {
        _status = WorkoutStatus.Active;
        return this;
    }

    public Workout Build()
    {
        var workout = Workout.Create(_name, _variant, _exercises);
        if (_status == WorkoutStatus.Active)
            workout.Start();
        return workout;
    }
}

// Usage
var workout = new WorkoutBuilder()
    .WithName("A2S 4-Day")
    .WithVariant(ProgramVariant.RTF)
    .WithLinearExercise("Squat", 100m, DayNumber.Monday)
    .WithLinearExercise("Bench", 80m, DayNumber.Monday)
    .WithRepsPerSetExercise("Lat Pulldown", new RepRange(8, 10, 12), 50m, DayNumber.Monday)
    .Started()
    .Build();
```

---

## 9. Key Design Decisions

### 9.1 Why Workout is the Aggregate Root

**Decision:** `Workout` serves as the aggregate root, not separate `WeeklyExercisePlan` or `WorkoutSession` aggregates.

**Rationale:**
1. **Transactional Consistency**: All exercise progressions and TM updates happen within a single aggregate transaction
2. **Invariant Protection**: Week progression rules require knowledge of all exercises
3. **Simpler Model**: Avoids coordination between multiple aggregates for common operations
4. **Query Efficiency**: Single load gives complete workout state

**Reference:** `research/approach-comparison.md` Section 2 "Aggregate Design Options"

**Trade-offs:**
- Larger aggregate than minimal
- More data loaded per request
- Acceptable for single-user workout tracking

### 9.2 Why PlannedSets are Regenerated Each Week

**Decision:** `PlannedSet` values are calculated on-demand from current TM and week parameters, not persisted long-term.

**Rationale:**
1. **TM Changes**: Manual TM adjustments should immediately reflect in planned sets
2. **Data Freshness**: No stale data if progression algorithms change
3. **Storage Efficiency**: No need to store 21 weeks x n exercises of planned sets
4. **Calculation Speed**: Simple percentage lookups are sub-millisecond

**Implementation:**
```csharp
// Calculate on-demand
public IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
{
    var (intensity, targetReps) = GetWeekParameters(weekNumber, blockNumber);
    var workingWeight = TrainingMax.CalculateWorkingWeight(intensity);
    // ... generate sets
}
```

### 9.3 Polymorphic Strategy Pattern for ExerciseProgression

**Decision:** Use polymorphic inheritance for `ExerciseProgression` with TPH persistence.

**Rationale:**
1. **Clean Separation**: Each progression type encapsulates its own logic
2. **Open/Closed Principle**: New progression types can be added without modifying existing code
3. **Type Safety**: Compile-time checking of strategy-specific properties
4. **EF Core Support**: TPH is well-supported and efficient

**Reference:** `research/domain-model.md` Section 6 "Weekly Exercise Plan"

**Alternatives Considered:**
- Composition with strategy interface: More flexible but more complex
- Single class with type enum: Violates SRP, messy switch statements

### 9.4 Week Transition and Progression Application Timing

**Decision:** Progression is applied immediately upon day completion. Week transition requires all days completed/skipped.

**Flow:**
```
1. User completes day with AMRAP results
2. TM adjustments calculated and applied immediately
3. Domain events raised (TrainingMaxAdjusted)
4. When all days complete/skipped, user can progress to next week
5. Week progression recalculates planned sets with new TMs
```

**Rationale:**
1. **Immediate Feedback**: User sees TM change right away
2. **No Lost Data**: If app crashes after day completion, TM is already saved
3. **Explicit Week Transition**: Prevents accidental week advancement

### 9.5 Equipment-Based Weight Increment Handling

**Decision:** Weight increments are calculated based on equipment type, with special handling for dumbbells.

**Reference:** `research/business-rules.md` Section 3.3 "Equipment-Based Weight Increments"

**Implementation:**
```csharp
// Dumbbells have smaller jumps at lower weights
if (equipmentType == EquipmentType.Dumbbell)
    return currentWeight < 10 ? Weight.Kilograms(1) : Weight.Kilograms(2);

// Everything else uses standard 2.5kg
return Weight.Kilograms(2.5m);
```

**Extensibility:** Equipment-specific increments could become configurable per exercise.

---

## 10. Implementation Sequence

### Step-by-Step Ordering

```
Week 1-2: Domain Layer
├── 1. Set up solution structure
├── 2. Implement base classes (Entity, AggregateRoot, ValueObject)
├── 3. Implement core value objects
│   ├── Weight, WeightUnit
│   ├── TrainingMax, TrainingMaxAdjustment
│   ├── RepRange
│   ├── PlannedSet, CompletedSet
│   └── ExercisePerformance, WorkoutActivity
├── 4. Implement domain events
├── 5. Implement ExerciseProgression hierarchy
│   ├── ExerciseProgression (abstract)
│   ├── LinearProgressionStrategy
│   └── RepsPerSetStrategy
├── 6. Implement Exercise entity
├── 7. Implement Workout aggregate root
└── 8. Write unit tests for all domain logic

Week 3-4: Domain Services
├── 9. Define service interfaces
├── 10. Implement LinearProgressionCalculator
├── 11. Implement RepsPerSetCalculator
├── 12. Write comprehensive algorithm tests
└── 13. Define repository interfaces

Week 4-5: Infrastructure
├── 14. Set up EF Core DbContext
├── 15. Create entity configurations
│   ├── WorkoutConfiguration
│   ├── ExerciseConfiguration
│   └── ExerciseProgressionConfiguration (TPH)
├── 16. Implement WorkoutRepository
├── 17. Implement UnitOfWork
├── 18. Create initial migration
├── 19. Set up PostgreSQL connection
└── 20. Write repository integration tests

Week 5-6: Application Layer
├── 21. Set up MediatR with pipeline behaviors
├── 22. Implement CreateWorkout command/handler
├── 23. Implement CompleteDay command/handler
├── 24. Implement ProgressToNextWeek command/handler
├── 25. Implement AdjustTrainingMax command/handler
├── 26. Implement query handlers
├── 27. Create FluentValidation validators
├── 28. Create DTOs and mapping logic
└── 29. Write handler integration tests

Week 6-8: API & Web Layer
├── 30. Set up ASP.NET Core Web API
├── 31. Configure dependency injection
├── 32. Implement WorkoutsController
├── 33. Implement ExercisesController
├── 34. Add exception handling middleware
├── 35. Configure Swagger/OpenAPI
├── 36. Set up React project with TypeScript
├── 37. Create API client service
├── 38. Implement core UI components
├── 39. Connect frontend to backend
└── 40. End-to-end testing
```

---

## 11. Success Criteria

### 11.1 Domain Layer

| Criterion | Measurement |
|-----------|-------------|
| All progression algorithms match spec | All examples from business-rules.md pass as unit tests |
| Aggregate invariants enforced | Exception thrown for invalid operations |
| Value objects immutable | No public setters, all methods return new instances |
| Domain events raised correctly | Events captured in unit tests |
| Test coverage | > 90% line coverage on domain code |

### 11.2 Progression Algorithms

| Criterion | Measurement |
|-----------|-------------|
| AMRAP delta table correct | All 8 delta values tested |
| TM rounding correct | Rounds to nearest 2.5kg |
| Reps Per Set evaluation correct | Success/Maintained/Failed classification tests |
| Equipment increments correct | All equipment types tested |
| Deload handling correct | Week 7/14/21 identified correctly |

### 11.3 Week-to-Week Flow

| Criterion | Measurement |
|-----------|-------------|
| Day completion records properly | Performance data persisted |
| TM updates apply immediately | Query returns new TM after completion |
| Week progression works | CurrentWeek increments correctly |
| Block transitions correct | Block 1->2, 2->3 detected |
| Full 21-week cycle | Integration test from week 1 to 21 |

### 11.4 Database

| Criterion | Measurement |
|-----------|-------------|
| Schema supports all use cases | All entity configurations work |
| TPH discrimination correct | Both progression types persist/load |
| Concurrent updates handled | Optimistic concurrency exception on conflict |
| Performance acceptable | < 100ms for common queries |
| Data integrity maintained | No orphaned records, FK constraints hold |

### 11.5 API

| Criterion | Measurement |
|-----------|-------------|
| All endpoints functional | Swagger UI can execute all operations |
| Validation errors return 400 | Invalid requests rejected with details |
| Not found returns 404 | Missing resources return proper status |
| CORS configured | Frontend can access API |
| Documentation complete | OpenAPI spec covers all endpoints |

---

## 12. Risk Mitigation

**Reference:** `research/constraints-and-risks.md`

### 12.1 Concurrent Workout Completion

**Risk:** User completes same day on multiple devices simultaneously.

**Mitigation:** Optimistic concurrency with row version.

```csharp
// Entity configuration
builder.Property<uint>("RowVersion")
    .IsRowVersion();

// In repository
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    throw new ConcurrencyException("Workout was modified by another request");
}
```

### 12.2 Week Boundary Edge Cases

**Risk:** Unclear behavior when completing workouts out of order or retroactively.

**Mitigation:** Clear state transition rules.

```csharp
public void CompleteDay(DayNumber day, IEnumerable<ExercisePerformance> performances)
{
    if (Status != WorkoutStatus.Active)
        throw new DomainException("Cannot complete day on inactive workout");

    if (IsActivityAlreadyRecorded(CurrentWeek, day))
        throw new DomainException("Day already completed for this week");

    // Proceed with completion
}
```

### 12.3 Training Max Bounds

**Risk:** TM becomes unreasonably high or low through extreme AMRAP results.

**Mitigation:** Validation constraints.

```csharp
public TrainingMax ApplyAdjustment(TrainingMaxAdjustment adjustment)
{
    var newValue = Value * (1 + adjustment.Amount);

    // Cap single-session adjustment at +/- 10%
    if (Math.Abs(adjustment.Amount) > 0.10m)
        throw new DomainException("Single adjustment cannot exceed 10%");

    // Ensure minimum TM
    if (newValue < MinimumTrainingMax)
        return new TrainingMax(MinimumTrainingMax, Unit);

    return new TrainingMax(RoundToIncrement(newValue), Unit);
}
```

### 12.4 Data Consistency

**Risk:** Partial updates leave aggregate in inconsistent state.

**Mitigation:** Aggregate transactional boundaries.

```csharp
public async Task<DayCompletionResultDto> Handle(
    CompleteDayCommand request,
    CancellationToken ct)
{
    // Load aggregate
    var workout = await _uow.Workouts.GetByIdAsync(id, ct);

    // All changes within aggregate
    workout.CompleteDay(request.Day, performances);

    // Single SaveChanges - all or nothing
    await _uow.SaveChangesAsync(ct);

    return result;
}
```

### 12.5 Complex UI State

**Risk:** Frontend state becomes out of sync with backend.

**Mitigation:** Query after mutation pattern.

```typescript
// After completing a day
async function completeDay(data: CompleteDayRequest): Promise<void> {
    await api.post(`/workouts/${id}/complete-day`, data);

    // Refresh state from server
    await refetchCurrentWeek();
    await refetchExerciseProgress();
}
```

### 12.6 Performance with Large History

**Risk:** Loading full workout with 21 weeks of history becomes slow.

**Mitigation:** Lazy loading and pagination.

```csharp
// Don't load activities by default
public async Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct)
{
    return await _context.Workouts
        .Include(w => w.Exercises)
            .ThenInclude(e => e.Progression)
        // Activities loaded separately when needed
        .FirstOrDefaultAsync(w => w.Id == id, ct);
}

// Separate query for history
public async Task<IReadOnlyList<WorkoutActivity>> GetActivitiesAsync(
    WorkoutId id,
    int? fromWeek = null,
    int? toWeek = null,
    CancellationToken ct = default)
{
    var query = _context.Entry(workout)
        .Collection(w => w.Activities)
        .Query();

    if (fromWeek.HasValue)
        query = query.Where(a => a.WeekNumber >= fromWeek.Value);
    if (toWeek.HasValue)
        query = query.Where(a => a.WeekNumber <= toWeek.Value);

    return await query.ToListAsync(ct);
}
```

---

## Appendix A: Technology Versions

| Technology | Version | Notes |
|------------|---------|-------|
| .NET | 9.0 | LTS target |
| ASP.NET Core | 9.0 | Web API framework |
| Entity Framework Core | 9.0 | ORM |
| PostgreSQL | 16.x | Database |
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.x | EF Core provider |
| MediatR | 12.x | CQRS mediator |
| FluentValidation | 11.x | Validation |
| Serilog | 4.x | Logging |
| React | 18.x | Frontend framework |
| TypeScript | 5.x | Frontend language |
| Vite | 5.x | Frontend build tool |
| xUnit | 2.x | Testing framework |
| Moq | 4.x | Mocking framework |

---

## Appendix B: Glossary

| Term | Definition |
|------|------------|
| **TM (Training Max)** | Working maximum, typically 85-90% of 1RM, used to calculate training weights |
| **AMRAP** | As Many Reps As Possible - final set taken to near-failure |
| **RTF** | Reps To Failure - progression variant using AMRAP for autoregulation |
| **RIR** | Reps In Reserve - how many more reps could be performed |
| **Block** | 7-week mesocycle within the 21-week program |
| **Deload** | Reduced volume/intensity week for recovery (weeks 7, 14, 21) |
| **TPH** | Table Per Hierarchy - EF Core inheritance mapping strategy |
| **CQRS** | Command Query Responsibility Segregation |
| **DDD** | Domain-Driven Design |

---

## Appendix C: Reference Documents

- `research/business-rules.md` - Complete progression algorithms and validation rules
- `research/domain-model.md` - Entity relationships and aggregate structure
- `research/use-cases.md` - User journeys and scenarios
- `research/approach-comparison.md` - Architecture decision analysis
- `research/constraints-and-risks.md` - Technical constraints and mitigation
- `research/recommendations.md` - Phase 1 conclusions and recommendations
- `research/integration-analysis.md` - External integrations and extensibility
