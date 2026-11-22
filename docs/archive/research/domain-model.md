# Domain Model: A2S Workout Tracking Application

## Executive Summary

This document defines the core domain model for the Average 2 Savage (A2S) Workout Tracking Application. The domain centers around tracking strength training workouts with autoregulated progression based on Greg Nuckols' Stronger By Science methodology.

---

## Core Domain Concepts

### 1. Training Program

A **Training Program** represents the overall structure of a user's training plan. In A2S, this is typically a 21-week program divided into three 7-week blocks.

**Attributes:**
- Program ID (unique identifier)
- Name (e.g., "A2S Linear Progression", "A2S RTF")
- Program Variant (Linear Progression, Reps To Failure, RIR)
- Total Weeks (default: 21)
- Current Block (1, 2, or 3)
- Start Date
- Status (Active, Paused, Completed)

### 2. Training Block

A **Training Block** is a mesocycle within the program. Each block has distinct intensity and volume characteristics.

**Attributes:**
- Block Number (1, 2, or 3)
- Week Range (1-7, 8-14, 15-21)
- Intensity Profile (Block 1: lightest/highest reps, Block 3: heaviest/lowest reps)
- Deload Week (Week 7 of each block)

### 3. Training Week

A **Training Week** contains the scheduled workout days and tracks weekly progression.

**Attributes:**
- Week Number (1-21)
- Block Number (derived from week number)
- Training Days (collection of WorkoutDays)
- Is Deload Week (boolean)
- Status (Pending, InProgress, Completed)

### 4. Workout Day

A **Workout Day** represents a single training session with multiple exercises.

**Attributes:**
- Day ID
- Day of Week (Monday, Tuesday, etc.)
- Day Number within Week (1-5 typically)
- Scheduled Exercises (ordered collection)
- Actual Completion Time
- Status (Scheduled, InProgress, Completed, Skipped)

### 5. Exercise Definition

An **Exercise Definition** is the template for an exercise, independent of any specific workout.

**Attributes:**
- Exercise ID
- Name (e.g., "Squat", "Bench Press", "Deadlift")
- Category (Main Lift, Auxiliary Lift, Accessory)
- Movement Pattern (Squat, Hinge, Push, Pull)
- Equipment Required (Barbell, Dumbbell, Machine, Bodyweight)
- Is Bilateral (boolean)

### 6. Weekly Exercise Plan

A **Weekly Exercise Plan** defines how a specific exercise should be performed in a given week.

**Attributes:**
- Exercise Definition (reference)
- Week Number
- Progression Strategy (reference - polymorphic)
- Target Sets (calculated based on strategy)
- Planned Sets (collection of PlannedSet value objects)

**Progression Strategies (Polymorphic):**

#### 6.1 Linear Progression Strategy (RTF/RIR variants)
For main and auxiliary lifts using percentage-based training with Training Max.

**Attributes:**
- Training Max (current TM for this exercise)
- Intensity Percentage (% of TM for this week)
- Target Reps Per Set
- AMRAP Target Reps (for last set)
- Is Primary Lift (boolean - affects progression rate)
- Progression Method (RTF vs RIR)
- Min/Max Sets Threshold (for RIR only)

**Progression Logic:**
- RTF: AMRAP performance drives TM adjustment
- RIR: Set count before hitting RIR threshold drives TM adjustment

#### 6.2 Reps Per Set Strategy
For accessory exercises without Training Max (isolation work, machines, cables).

**Attributes:**
- Rep Range (min-target-max, e.g., 8-10-12)
- Current Set Count (e.g., 2, 3, 4)
- Target Set Count (goal before weight increase, e.g., 4)
- Starting Set Count (reset point after weight increase, e.g., 2)
- Current Weight (actual weight being used)
- Equipment Type (Barbell, Dumbbell, Cable, Machine, Bodyweight)
- Weight Increment (based on equipment type)

**Progression Logic:**
- Success (all sets hit max reps) → Add set OR increase weight
- Maintained (all sets hit min reps) → No change
- Failed (any set below min reps) → Remove set OR decrease weight

### 7. Exercise Performance

An **Exercise Performance** captures the actual execution of an exercise during a workout.

**Attributes:**
- Exercise Definition (reference)
- Sets Performed (collection of Set data)
- AMRAP Result (reps achieved on final set)
- RIR Estimate (for RIR variant)
- Notes
- Timestamp
- Calculated Training Max Adjustment

### 8. Set

A **Set** is a single bout of exercise within a workout.

**Attributes:**
- Set Number
- Target Reps
- Actual Reps Completed
- Weight Used
- Is AMRAP Set (boolean)
- RPE/RIR (optional rating)
- Notes (e.g., "felt easy", "grinder")

### 9. Training Max

A **Training Max** is the working maximum used to calculate percentages for programming. It is NOT the actual 1RM but typically 85-90% of it.

**Attributes:**
- Exercise Definition (reference)
- Value (weight in chosen unit)
- Unit (kg or lbs)
- Last Updated
- Update History (for auditing/rollback)

---

## Value Objects

### Weight
```
Weight {
    value: decimal
    unit: WeightUnit (KG | LBS)
}
```

### RepTarget
```
RepTarget {
    minimum: integer
    target: integer
    maximum: integer (for AMRAP calculations)
}
```

### Percentage
```
Percentage {
    value: decimal (0.0 - 1.0)
}
```

### TrainingMaxAdjustment
```
TrainingMaxAdjustment {
    amount: decimal
    type: AdjustmentType (PERCENTAGE | ABSOLUTE)
    direction: Direction (INCREASE | DECREASE | NONE)
}
```

### SetResult
```
SetResult {
    weight: Weight
    targetReps: integer
    actualReps: integer
    isAMRAP: boolean
    rpe: optional<decimal>
}
```

---

## Aggregate Structure

### Primary Aggregate: Workout Program

The **Workout Program** serves as the aggregate root, containing:

```
WorkoutProgram (Aggregate Root)
├── ProgramId
├── UserId
├── ProgramSettings
│   ├── Variant (LP, RTF, RIR)
│   ├── TrainingDaysPerWeek
│   └── ProgressionParameters
├── ExerciseConfigurations[]
│   ├── ExerciseDefinitionId
│   ├── TrainingMax
│   └── CustomSettings
├── TrainingBlocks[]
│   └── TrainingWeeks[]
│       ├── WeekNumber
│       ├── WeeklyExercisePlans[]
│       └── Status
└── CurrentState
    ├── CurrentWeek
    ├── CurrentDay
    └── ProgramStatus
```

### Secondary Aggregate: Workout Session

The **Workout Session** captures the actual performance:

```
WorkoutSession (Aggregate Root)
├── SessionId
├── ProgramId (reference)
├── WeekNumber
├── DayNumber
├── ScheduledAt
├── StartedAt
├── CompletedAt
├── ExercisePerformances[]
│   ├── ExerciseDefinitionId
│   ├── PlannedSets
│   ├── ActualSets[]
│   │   ├── SetNumber
│   │   ├── Weight
│   │   ├── TargetReps
│   │   ├── ActualReps
│   │   └── IsAMRAP
│   └── TrainingMaxAdjustment (calculated)
└── SessionNotes
```

### Reference Aggregate: Exercise Catalog

```
ExerciseCatalog (Aggregate Root)
├── CatalogId
├── Exercises[]
│   ├── ExerciseId
│   ├── Name
│   ├── Category
│   ├── MovementPattern
│   └── DefaultProgressionSettings
└── CustomExercises[] (user-defined)
```

---

## Entity Relationships Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          WORKOUT PROGRAM                                 │
│  (Aggregate Root)                                                        │
│                                                                          │
│  ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐   │
│  │ Program         │     │ Exercise        │     │ Training Block  │   │
│  │ Settings        │     │ Configurations  │     │ (1, 2, 3)       │   │
│  │                 │     │                 │     │                 │   │
│  │ - Variant       │     │ - ExerciseId    │     │ - BlockNumber   │   │
│  │ - Days/Week     │     │ - TrainingMax   │     │ - Weeks[]       │   │
│  │ - Progression   │     │ - MinSets       │     │                 │   │
│  │   Parameters    │     │ - MaxSets       │     │                 │   │
│  └─────────────────┘     └─────────────────┘     └────────┬────────┘   │
│                                                            │            │
│                                    ┌───────────────────────┘            │
│                                    │                                    │
│                                    ▼                                    │
│                          ┌─────────────────┐                            │
│                          │ Training Week   │                            │
│                          │                 │                            │
│                          │ - WeekNumber    │                            │
│                          │ - IsDeload      │                            │
│                          │ - Status        │                            │
│                          │ - ExercisePlans │                            │
│                          └────────┬────────┘                            │
│                                   │                                     │
│                                   ▼                                     │
│                     ┌──────────────────────────┐                        │
│                     │ Weekly Exercise Plan     │                        │
│                     │                          │                        │
│                     │ - ExerciseId             │                        │
│                     │ - TargetSets             │                        │
│                     │ - TargetReps             │                        │
│                     │ - IntensityPercentage    │                        │
│                     │ - WorkingWeight          │                        │
│                     └──────────────────────────┘                        │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                          WORKOUT SESSION                                 │
│  (Aggregate Root)                                                        │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────┐        │
│  │ Session                                                      │        │
│  │                                                              │        │
│  │ - SessionId                                                  │        │
│  │ - ProgramId (reference)                                      │        │
│  │ - WeekNumber, DayNumber                                      │        │
│  │ - Timestamps (scheduled, started, completed)                 │        │
│  └──────────────────────────────────────────────────────────────┘        │
│                                    │                                     │
│                                    ▼                                     │
│                     ┌──────────────────────────┐                        │
│                     │ Exercise Performance     │                        │
│                     │                          │                        │
│                     │ - ExerciseId             │                        │
│                     │ - Sets[]                 │                        │
│                     │ - TMadjustment           │                        │
│                     └──────────────────────────┘                        │
│                                    │                                     │
│                                    ▼                                     │
│                          ┌─────────────────┐                            │
│                          │ Set             │                            │
│                          │                 │                            │
│                          │ - SetNumber     │                            │
│                          │ - Weight        │                            │
│                          │ - TargetReps    │                            │
│                          │ - ActualReps    │                            │
│                          │ - IsAMRAP       │                            │
│                          └─────────────────┘                            │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                          EXERCISE CATALOG                                │
│  (Reference Data / Aggregate Root)                                       │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────┐        │
│  │ Exercise Definition                                          │        │
│  │                                                              │        │
│  │ - ExerciseId                                                 │        │
│  │ - Name                                                       │        │
│  │ - Category (Main, Auxiliary, Accessory)                      │        │
│  │ - MovementPattern (Squat, Hinge, Push, Pull)                 │        │
│  │ - Equipment                                                  │        │
│  │ - DefaultProgressionSettings                                 │        │
│  └──────────────────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Domain Events

### Program Lifecycle Events
- `ProgramCreated` - New training program initialized
- `ProgramStarted` - User begins the program
- `ProgramPaused` - User temporarily pauses training
- `ProgramResumed` - User resumes training after pause
- `ProgramCompleted` - All 21 weeks completed
- `ProgramAbandoned` - User quits program early

### Week/Block Progression Events
- `WeekStarted` - New training week begins
- `WeekCompleted` - All scheduled workouts for week done
- `BlockCompleted` - 7-week block finished
- `DeloadWeekStarted` - Deload week (week 7, 14, 21) begins

### Workout Session Events
- `WorkoutScheduled` - Workout appears on schedule
- `WorkoutStarted` - User begins training session
- `ExerciseCompleted` - Single exercise finished within workout
- `WorkoutCompleted` - All exercises in session done
- `WorkoutSkipped` - User marks workout as skipped

### Progression Events
- `TrainingMaxIncreased` - TM goes up based on performance
- `TrainingMaxDecreased` - TM goes down due to missed targets
- `TrainingMaxMaintained` - TM stays same (hit target exactly)
- `AMRAPRecorded` - AMRAP set result captured
- `ProgressionCalculated` - New TM calculated from performance

---

## Bounded Context Map

```
┌─────────────────────────────────────────────────────────────────┐
│                     A2S WORKOUT TRACKER                          │
│                                                                  │
│  ┌───────────────────┐         ┌───────────────────┐            │
│  │   PROGRAMMING     │         │   EXECUTION       │            │
│  │   CONTEXT         │ ──────► │   CONTEXT         │            │
│  │                   │         │                   │            │
│  │ • Program Setup   │         │ • Workout Logging │            │
│  │ • Week Planning   │         │ • Set Recording   │            │
│  │ • Exercise Config │         │ • Timer/Rest      │            │
│  │ • Block Structure │         │ • AMRAP Tracking  │            │
│  └───────────────────┘         └───────────────────┘            │
│           │                             │                        │
│           │                             │                        │
│           ▼                             ▼                        │
│  ┌───────────────────────────────────────────────────┐          │
│  │              PROGRESSION CONTEXT                   │          │
│  │                                                    │          │
│  │  • Training Max Calculations                       │          │
│  │  • AMRAP → TM Adjustment Algorithm                │          │
│  │  • RIR/RTF Evaluation                             │          │
│  │  • Week-over-Week Progression                     │          │
│  └───────────────────────────────────────────────────┘          │
│                             │                                    │
│                             ▼                                    │
│  ┌───────────────────────────────────────────────────┐          │
│  │              ANALYTICS CONTEXT                     │          │
│  │                                                    │          │
│  │  • Progress Visualization                          │          │
│  │  • Volume Tracking                                 │          │
│  │  • Strength Trends                                 │          │
│  │  • Historical Comparisons                          │          │
│  └───────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Domain Invariants

1. **Training Max Range**: Training Max should be 85-95% of actual 1RM
2. **Block Progression**: Block 1 < Block 2 < Block 3 in intensity
3. **Rep Relationship**: Higher intensity = lower target reps
4. **Deload Placement**: Every 7th week is a deload week
5. **AMRAP Minimum**: AMRAP set must achieve at least target reps for TM maintenance
6. **Set Ordering**: AMRAP set is always the final set for an exercise
7. **Program Continuity**: Cannot skip weeks; must complete or mark skipped

---

## Ubiquitous Language Glossary

| Term | Definition |
|------|------------|
| **Training Max (TM)** | Working maximum (typically 85-90% of 1RM) used to calculate training weights |
| **1RM** | One Rep Maximum - the heaviest weight that can be lifted for one repetition |
| **AMRAP** | As Many Reps As Possible - final set taken to near-failure |
| **RTF** | Reps To Failure - variant where final set is taken to true failure |
| **RIR** | Reps In Reserve - how many reps could be done before failure |
| **RPE** | Rate of Perceived Exertion - subjective effort scale (1-10) |
| **Block** | 7-week mesocycle within the 21-week program |
| **Deload** | Reduced volume/intensity week for recovery |
| **Main Lift** | Primary compound movement (Squat, Bench, Deadlift, OHP) |
| **Auxiliary Lift** | Close variation of main lift |
| **Accessory** | Supplementary exercise for muscle development |
| **Progression** | The systematic increase in training stimulus |
| **Autoregulation** | Adjusting training based on daily performance |

---

## Notes on Model Evolution

This domain model is designed to support:

1. **Multiple Program Variants** - LP, RTF, RIR can share core structures
2. **Exercise Extensibility** - User-defined exercises integrate seamlessly
3. **Progression Flexibility** - Algorithm can be swapped/tuned without structural changes
4. **Historical Tracking** - All performance data retained for analytics
5. **Offline-First** - Session can be recorded without connectivity

The aggregate boundaries are drawn to ensure:
- Program configuration is consistent
- Workout sessions are independently recordable
- Training Max updates are atomic
- Week transitions are explicit state changes
