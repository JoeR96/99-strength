# Implementation Plan: 21-Week Workout Flow Testing with MinimalSets Strategy

## Overview

This plan implements comprehensive E2E and Integration tests for a 21-week A2S workout cycle, including a new "MinimalSets" progression strategy. The CSV spreadsheet is the source of truth for all test assertions.

---

## Part 1: New Domain - MinimalSetsStrategy

### 1.1 Create MinimalSetsStrategy.cs

**File:** `src/A2S.Domain/Aggregates/Workout/MinimalSetsStrategy.cs`

This is the "yellow" exercise type from your spreadsheet (Assisted Dips, Assisted Pullups).

**Properties:**
- `CurrentWeight` - The assistance weight (e.g., 32kg, 30kg)
- `TargetTotalReps` - Goal reps to complete (e.g., 40)
- `CurrentSetCount` - Current number of sets allowed
- `MinimumSets` - Floor for set count (e.g., 2)
- `Equipment` - EquipmentType.Machine

**Progression Logic (from spreadsheet analysis):**
```
Week 1: Assisted Dips - 32kg, 3 sets, 40 total reps
Week 2: Assisted Dips - 30kg, 4 sets, 40 total reps (weight reduced, sets increased)
Week 3+: Assisted Dips - 30kg, 3 sets, 40 total reps (back to 3 sets)

SUCCESS: Complete 40 reps in FEWER sets than CurrentSetCount
  -> Reduce CurrentSetCount by 1 (progress = efficiency)

MAINTAINED: Complete 40 reps in EXACTLY CurrentSetCount sets
  -> No change

FAILED: Cannot complete 40 reps in CurrentSetCount sets
  -> Add 1 set OR reduce weight
```

### 1.2 Update Exercise.cs

Add factory method:
```csharp
public static Exercise CreateWithMinimalSetsProgression(
    string name,
    ExerciseCategory category,
    EquipmentType equipment,
    DayNumber assignedDay,
    int orderInDay,
    Weight startingWeight,
    int targetTotalReps,
    int startingSets,
    int minimumSets = 2)
```

### 1.3 Update ExercisePerformance.cs

Add method for MinimalSets:
```csharp
public int GetTotalRepsCompleted() => CompletedSets.Sum(s => s.ActualReps);
public int GetSetsUsed() => CompletedSets.Count;
```

### 1.4 Update RepsPerSetStrategy for Unilateral Support

**File:** `src/A2S.Domain/Aggregates/Workout/RepsPerSetStrategy.cs`

Add property:
```csharp
public bool IsUnilateral { get; private set; }

// Max sets logic:
// - Bilateral (IsUnilateral = false): max 5 sets
// - Unilateral (IsUnilateral = true): max 3 sets per side (displayed as 3, but understood as 6 total)
public int MaxSets => IsUnilateral ? 3 : 5;
```

Update `ApplyPerformanceResult()` to use `MaxSets` instead of hardcoded value.

### 1.5 EF Core Configuration

Update TPH discriminator in `ExerciseProgressionConfiguration.cs` to include "MinimalSets".
Add `IsUnilateral` column mapping for RepsPerSetStrategy.

---

## Part 2: Application Layer - Commands & Handlers

### 2.1 CompleteDayCommand

**Files to create:**
- `src/A2S.Application/Commands/CompleteDay/CompleteDayCommand.cs`
- `src/A2S.Application/Commands/CompleteDay/CompleteDayCommandHandler.cs`
- `src/A2S.Application/Commands/CompleteDay/CompleteDayCommandValidator.cs`

**Command:**
```csharp
public sealed record CompleteDayCommand(
    Guid WorkoutId,
    DayNumber Day,
    IReadOnlyList<ExercisePerformanceRequest> Performances
) : ICommand<Result<CompleteDayResult>>;

public sealed record ExercisePerformanceRequest
{
    public Guid ExerciseId { get; init; }
    public IReadOnlyList<CompletedSetRequest> CompletedSets { get; init; }
}

public sealed record CompletedSetRequest
{
    public int SetNumber { get; init; }
    public decimal Weight { get; init; }
    public WeightUnit WeightUnit { get; init; }
    public int ActualReps { get; init; }
    public bool WasAmrap { get; init; }
}
```

### 2.2 ProgressWeekCommand

**Files to create:**
- `src/A2S.Application/Commands/ProgressWeek/ProgressWeekCommand.cs`
- `src/A2S.Application/Commands/ProgressWeek/ProgressWeekCommandHandler.cs`

**Command:**
```csharp
public sealed record ProgressWeekCommand(Guid WorkoutId) : ICommand<Result<ProgressWeekResult>>;

public sealed record ProgressWeekResult(
    int PreviousWeek,
    int NewWeek,
    int NewBlock,
    bool IsDeloadWeek,
    bool IsProgramComplete
);
```

---

## Part 3: API Endpoints

### 3.1 Add to WorkoutsController.cs

```csharp
/// POST /api/v1/workouts/{id}/days/{day}/complete
[HttpPost("{id:guid}/days/{day}/complete")]
public async Task<IActionResult> CompleteDay(
    [FromRoute] Guid id,
    [FromRoute] DayNumber day,
    [FromBody] CompleteDayRequest request,
    CancellationToken cancellationToken)

/// POST /api/v1/workouts/{id}/progress-week
[HttpPost("{id:guid}/progress-week")]
public async Task<IActionResult> ProgressToNextWeek(
    [FromRoute] Guid id,
    CancellationToken cancellationToken)
```

---

## Part 4: Test Data from Spreadsheet

### 4.1 Exercise Configuration (from CSV lines 1-60)

**Day 1 (7 exercises):**
| Exercise | Type | Week 1 Config |
|----------|------|---------------|
| Lat Pulldown | RepsPerSet (green) | 3 sets x 12 reps, yes |
| Overhead Press Smith Machine | Linear (red) | TM: 65kg, Weight: 42.5, Reps: 12, Target: 15, Sets: 4, AMRAP: 19 |
| Cable Low Row | RepsPerSet (green) | 4 sets x 12 reps, yes |
| Cable Lateral Raise | RepsPerSet (green) | 4 sets x 8 reps, yes |
| Cable Bicep Curl | RepsPerSet (green) | 4 sets x 20 reps, yes |
| Cable Tricep Pushdown | RepsPerSet (green) | 4 sets x 20 reps, no (FAILED) |
| Rear Delt Flyes | RepsPerSet (green) | 4 sets x 12 reps, yes |

**Day 2 (6 exercises):**
| Exercise | Type | Week 1 Config |
|----------|------|---------------|
| Smith Squat | Linear (red) | TM: 107.5kg, Weight: 75, Reps: 5, Target: 10, Sets: 5, AMRAP: 16 |
| Single Leg Lunge Smith Machine | RepsPerSet (green) | 4 sets x 9 reps, yes |
| Lying Leg Curl | RepsPerSet (green) | 4 sets x 12 reps, yes |
| Hip Abduction | RepsPerSet (green) | 3 sets x 12 reps, yes |
| Calf Raises | RepsPerSet (green) | 3 sets x 15 reps, yes |

**Day 3 (7 exercises):**
| Exercise | Type | Week 1 Config |
|----------|------|---------------|
| Assisted Dips | MinimalSets (yellow) | Weight: 32, Sets: 3, Total: 40 |
| Assisted Pullups | MinimalSets (yellow) | Weight: 32, Sets: 6, Total: 40 |
| Concentration Curl | RepsPerSet (green) | 4 sets x 15 reps, no (FAILED) |
| Ez Curl | RepsPerSet (green) | 3 sets x 15 reps, yes |
| Single Arm Tricep Pushdown | RepsPerSet (green) | 6 sets x 25 reps, yes |
| Lateral Raises | RepsPerSet (green) | 3 sets x 20 reps, yes |
| Chest Flye | RepsPerSet (green) | 3 sets x 8 reps, yes |

**Day 4 (5 exercises):**
| Exercise | Type | Week 1 Config |
|----------|------|---------------|
| Booty Builder | RepsPerSet (green) | 3 sets x 8 reps, yes |
| Front Squat | Linear (red) | TM: 80kg, Weight: 47.5, Reps: 7, Target: 14, Sets: 5, AMRAP: 17 |
| Single Leg Press | RepsPerSet (green) | 4 sets x 12 reps, yes |
| Leg Extension | RepsPerSet (green) | 4 sets x 12 reps, (no status) |
| Hip Adduction | RepsPerSet (green) | 4 sets, (no reps listed) |

**Total: 25 exercises across 4 days**

### 4.2 Week-by-Week Linear Exercise Progression (from CSV)

**Overhead Press Smith Machine (Day 1):**
| Week | Weight | Reps | Target | Sets | Notes |
|------|--------|------|--------|------|-------|
| 1 | 42.5 | 12 | 15 | 4 | AMRAP: 19 |
| 2 | 45 | 11 | 13 | 4 | |
| 3 | 47.5 | 10 | 12 | 4 | |
| 4 | 45 | 11 | 13 | 4 | |
| 5 | 47.5 | 10 | 12 | 4 | |
| 6 | 47.5 | 9 | 11 | 4 | |
| 7 | 40 | 5 | n/a | 4 | DELOAD |
| 8 | 45 | 11 | 13 | 4 | Block 2 |
| ... | ... | ... | ... | ... | |
| 14 | 40 | 5 | n/a | 4 | DELOAD |
| 21 | 40 | 5 | n/a | 4 | DELOAD |

**Smith Squat (Day 2):**
| Week | Weight | Reps | Target | Sets | Notes |
|------|--------|------|--------|------|-------|
| 1 | 75 | 5 | 10 | 5 | AMRAP: 16 |
| 2 | 85 | 4 | 8 | 5 | |
| 3 | 90 | 3 | 6 | 5 | |
| 4 | 80 | 5 | 9 | 5 | |
| 5 | 85 | 4 | 7 | 5 | |
| 6 | 90 | 3 | 5 | 5 | |
| 7 | 65 | 5 | n/a | 4 | DELOAD |
| ... | ... | ... | ... | ... | |
| 21 | 65 | 5 | n/a | 4 | DELOAD |

**Front Squat (Day 4):**
| Week | Weight | Reps | Target | Sets | Notes |
|------|--------|------|--------|------|-------|
| 1 | 47.5 | 7 | 14 | 5 | AMRAP: 17 |
| 2 | 52.5 | 6 | 12 | 5 | |
| 3 | 57.5 | 5 | 10 | 5 | |
| ... | ... | ... | ... | ... | |
| 21 | 47.5 | 5 | n/a | 4 | DELOAD |

### 4.3 RepsPerSet Progression Examples (from CSV)

**Lat Pulldown (shows set progression):**
| Week | Sets | Reps | Status | Notes |
|------|------|------|--------|-------|
| 1 | 3 | 12 | yes | |
| 2 | 4 | 12 | yes | +1 set |
| 3 | 5 | 12 | - | +1 set |
| 4+ | 5 | 12 | - | Maxed |

**Cable Tricep Pushdown (shows failure):**
| Week | Sets | Reps | Status | Notes |
|------|------|------|--------|-------|
| 1 | 4 | 20 | no | FAILED |
| 2 | 4 | 20 | yes | No change (maintained) |
| 3 | 5 | 20 | - | +1 set |

**Calf Raises (shows failure):**
| Week | Sets | Reps | Status | Notes |
|------|------|------|--------|-------|
| 1 | 3 | 15 | yes | |
| 2 | 4 | 15 | no | +1 set, then FAILED |
| 3+ | 4 | 15 | - | Maintained |

### 4.4 MinimalSets Progression (from CSV)

**Assisted Dips:**
| Week | Weight | Sets | Total Reps | Notes |
|------|--------|------|------------|-------|
| 1 | 32 | 3 | 40 | |
| 2 | 30 | 4 | 40 | Weight reduced, sets increased |
| 3+ | 30 | 3 | 40 | Back to 3 sets |

**Assisted Pullups:**
| Week | Weight | Sets | Total Reps | Notes |
|------|--------|------|------------|-------|
| 1 | 32 | 6 | 40 | |
| 2 | 30 | 6 | 40 | Weight reduced |
| 3+ | 30 | 3 | 40 | Sets halved |

---

## Part 5: Integration Tests

### 5.1 Test Files to Create

```
tests/A2S.Api.Tests/
├── WorkoutFlowIntegrationTests.cs      # Full 21-week cycle test
├── CompleteDayIntegrationTests.cs      # Day completion tests
├── ProgressWeekIntegrationTests.cs     # Week progression tests
├── LinearProgressionIntegrationTests.cs # RTF/AMRAP tests
├── RepsPerSetProgressionIntegrationTests.cs
└── MinimalSetsProgressionIntegrationTests.cs
```

### 5.2 Test Scenarios

**Linear Progression (RTF) Tests:**
```csharp
[Theory]
[InlineData(15, 19, 0.02)]   // +4 reps = +2% TM (Week 1 OHP)
[InlineData(10, 16, 0.03)]   // +6 reps = +3% TM (Week 1 Smith Squat)
[InlineData(14, 17, 0.015)]  // +3 reps = +1.5% TM (Week 1 Front Squat)
[InlineData(12, 12, 0.0)]    // 0 reps = no change
[InlineData(12, 10, -0.05)]  // -2 reps = -5% TM (FAILED)
public async Task LinearProgression_AppliesCorrectTMAdjustment(
    int targetReps, int actualReps, decimal expectedAdjustment)
```

**RepsPerSet Progression Tests:**
```csharp
[Theory]
[InlineData(3, 12, true, false, 4)]   // Lat Pulldown W1->W2: 3 sets + yes = 4 sets (bilateral)
[InlineData(4, 20, false, false, 4)]  // Cable Tricep W1: 4 sets + no = 4 sets (maintain)
[InlineData(4, 20, true, false, 5)]   // Cable Tricep W2->W3: 4 sets + yes = 5 sets
[InlineData(5, 12, true, false, 5)]   // At max (5) + success = increase weight, keep 5 sets (bilateral)
[InlineData(3, 15, true, true, 3)]    // Single Arm Tricep: 3 sets per side + yes = at max (unilateral)
public async Task RepsPerSetProgression_AdjustsSetsCorrectly(
    int currentSets, int targetReps, bool allCompleted, bool isUnilateral, int expectedSets)
```

**MinimalSets Progression Tests:**
```csharp
[Theory]
[InlineData(40, 40, 3, 3, 3)]   // Hit target in expected sets = maintain
[InlineData(40, 40, 2, 3, 2)]   // Hit target in fewer sets = progress
[InlineData(40, 35, 3, 3, 4)]   // Missed target = add set
public async Task MinimalSetsProgression_AdjustsCorrectly(
    int targetReps, int actualReps, int setsUsed, int currentSets, int expectedSets)
```

**Deload Week Tests:**
```csharp
[Theory]
[InlineData(7, 1)]   // End of Block 1
[InlineData(14, 2)]  // End of Block 2
[InlineData(21, 3)]  // End of Block 3 (program complete)
public async Task DeloadWeek_HasReducedIntensityAndVolume(int week, int block)
{
    // Verify:
    // - Linear exercises use ~65% intensity (not progression-based)
    // - Rep target is "n/a" in spreadsheet
    // - Sets may be reduced (e.g., 4 instead of 5)
}
```

### 5.3 Full 21-Week Cycle Test

```csharp
[Fact]
public async Task Complete21WeekCycle_FollowsSpreadsheetProgression()
{
    // 1. Create workout with 25 exercises matching spreadsheet
    // 2. For each week 1-21:
    //    a. Complete Day 1-4 with spreadsheet performance data
    //    b. Assert progression outcomes match spreadsheet next-week values
    //    c. Progress to next week (except week 21)
    // 3. Assert program completes after week 21
    // 4. Assert final TM values match spreadsheet week 21 values
}
```

---

## Part 6: E2E Tests

### 6.1 Test Files to Create

```
tests/A2S.E2ETests/
├── WorkoutFlowE2ETests.cs         # UI-driven workout completion
├── CompleteDayE2ETests.cs         # Day completion via UI
└── ProgressWeekE2ETests.cs        # Week progression via UI
```

### 6.2 E2E Test Scenarios

```csharp
[Fact]
public async Task CompleteDay_ThroughUI_UpdatesProgressions()
{
    // 1. Login and create workout
    // 2. Navigate to Day 1
    // 3. Fill in performance data via UI forms
    // 4. Click "Complete Day"
    // 5. Assert UI shows updated exercise state
    // 6. Assert API returns correct progression values
}

[Fact]
public async Task ProgressWeek_ThroughUI_ShowsCorrectBlockAndDeload()
{
    // 1. Complete all 4 days of current week
    // 2. Click "Progress to Next Week"
    // 3. Assert week number increments
    // 4. Assert block number is correct
    // 5. Assert deload indicator shows for weeks 7, 14, 21
}
```

---

## Part 7: Test Data Classes

### 7.1 Create Test Data Structure

**File:** `tests/A2S.Tests.Shared/TestData/SpreadsheetTestData.cs`

```csharp
public static class SpreadsheetTestData
{
    // Exercise configurations from spreadsheet
    public static readonly ExerciseConfig[] Day1Exercises = new[]
    {
        new ExerciseConfig("Lat Pulldown", DayNumber.Day1, 1,
            ProgressionType.RepsPerSet, EquipmentType.Cable,
            StartingSets: 3, TargetReps: 12),
        new ExerciseConfig("Overhead Press Smith Machine", DayNumber.Day1, 2,
            ProgressionType.Linear, EquipmentType.Machine,
            TrainingMax: 65m),
        // ... etc
    };

    // Week-by-week expected values
    public static readonly WeekData[] AllWeeks = new[]
    {
        new WeekData(1, new[]
        {
            new ExerciseWeekData("Lat Pulldown", Sets: 3, Reps: 12, Completed: true),
            new ExerciseWeekData("Overhead Press Smith Machine",
                Weight: 42.5m, Reps: 12, Target: 15, Sets: 4, AmrapResult: 19),
            // ... all 25 exercises
        }),
        // ... weeks 2-21
    };
}
```

---

## Part 8: Implementation Order

### Phase 1: Domain Layer
1. [ ] Create `MinimalSetsStrategy.cs`
2. [ ] Add `CreateWithMinimalSetsProgression()` to `Exercise.cs`
3. [ ] Add `GetTotalRepsCompleted()` to `ExercisePerformance.cs`
4. [ ] Update EF Core TPH configuration
5. [ ] Add domain unit tests

### Phase 2: Application Layer
6. [ ] Create `CompleteDayCommand` + Handler + Validator
7. [ ] Create `ProgressWeekCommand` + Handler
8. [ ] Add application layer unit tests

### Phase 3: API Layer
9. [ ] Add `POST /api/v1/workouts/{id}/days/{day}/complete` endpoint
10. [ ] Add `POST /api/v1/workouts/{id}/progress-week` endpoint
11. [ ] Add request/response DTOs

### Phase 4: Test Infrastructure
12. [ ] Create `SpreadsheetTestData.cs` with all 21 weeks of data
13. [ ] Create test helper methods for exercise assertions

### Phase 5: Integration Tests
14. [ ] `CompleteDayIntegrationTests.cs`
15. [ ] `ProgressWeekIntegrationTests.cs`
16. [ ] `LinearProgressionIntegrationTests.cs`
17. [ ] `RepsPerSetProgressionIntegrationTests.cs`
18. [ ] `MinimalSetsProgressionIntegrationTests.cs`
19. [ ] `WorkoutFlowIntegrationTests.cs` (full 21-week test)

### Phase 6: E2E Tests
20. [ ] `CompleteDayE2ETests.cs`
21. [ ] `ProgressWeekE2ETests.cs`
22. [ ] `WorkoutFlowE2ETests.cs`

---

## Key Assertions from Spreadsheet

### Linear Progression Assertions
- Week 1 OHP: 42.5kg x 12 reps, AMRAP target 15, actual 19 (+4) -> TM increases ~2%
- Week 1 Smith Squat: 75kg x 5 reps, AMRAP target 10, actual 16 (+6) -> TM increases ~3%
- Week 7 is DELOAD: Weight drops, n/a target, reduced sets

### RepsPerSet Assertions
- Lat Pulldown: Week 1 (3 sets, yes) -> Week 2 (4 sets) -> Week 3 (5 sets) -> maxes at 5
- Cable Tricep Pushdown: Week 1 (4 sets, no/FAILED) -> Week 2 (4 sets, yes) -> Week 3 (5 sets)
- Cable Low Row: Week 1 (4 sets, yes) -> Week 2 (5 sets, yes) -> Week 3 (6 sets, yes) -> Week 4 (4 sets x 13 reps) - reps increased after max sets

### MinimalSets Assertions
- Assisted Dips: Week 1 (32kg, 3 sets) -> Week 2 (30kg, 4 sets) - weight adjustment
- Assisted Pullups: Week 1 (32kg, 6 sets) -> Week 2 (30kg, 6 sets) -> Week 3 (30kg, 3 sets) - dramatic improvement

---

## Clarified Requirements

### MinimalSets Progression (CONFIRMED)
- **Sets only** - Weight is user-controlled, not auto-adjusted
- Progression metric is completing target reps in FEWER sets
- SUCCESS: Complete target reps in fewer sets than current -> reduce set count
- MAINTAINED: Complete target reps in same number of sets -> no change
- FAILED: Cannot complete target reps -> add a set (user may manually reduce weight)

### RepsPerSet Max Sets (CONFIRMED)
- **Non-unilateral exercises**: 5 sets max, then increase weight and reset
- **Unilateral exercises**: 3 sets per side (6 total), then increase weight and reset
- Need to add `IsUnilateral` flag to exercise configuration
- Examples of unilateral: Single Leg Lunge, Single Leg Press, Single Arm Tricep Pushdown, Concentration Curl

### Deload Week Logic (CONFIRMED)
- **Calculated values** - Use formula-based deload (65% of TM, reduced reps)
- Tests verify the calculation is correct, not exact spreadsheet matches
- This allows the system to work with any TM, not just spreadsheet-specific values

### Exercises Requiring IsUnilateral Flag
Based on spreadsheet exercise names:
- Single Leg Lunge Smith Machine (unilateral)
- Single Leg Press (unilateral)
- Single Arm Tricep Pushdown (unilateral)
- Concentration Curl (unilateral - done one arm at a time)

All other exercises are bilateral (both sides simultaneously).
