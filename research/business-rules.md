# Business Rules: A2S Workout Tracking Application

## Executive Summary

This document defines all invariants, validation rules, domain constraints, and the detailed progression algorithms for the A2S Workout Tracking Application. These rules are derived from Greg Nuckols' Stronger By Science methodology.

---

## 1. Program Structure Rules

### 1.1 Program Duration
- **Rule**: Standard program is 21 weeks, divided into 3 blocks of 7 weeks each
- **Invariant**: `totalWeeks = 21` (unless using Novice Linear variant)
- **Exception**: Novice Linear Progression runs indefinitely until stalling

### 1.2 Block Structure
| Block | Weeks | Intensity | Reps Per Set | Character |
|-------|-------|-----------|--------------|-----------|
| 1 | 1-7 | Lowest | Highest | Volume accumulation |
| 2 | 8-14 | Medium | Medium | Intensity transition |
| 3 | 15-21 | Highest | Lowest | Peak/Test preparation |

**Invariant**: `Block N+1 intensity > Block N intensity`
**Invariant**: `Block N+1 reps < Block N reps`

### 1.3 Deload Weeks
- **Rule**: Week 7, 14, and 21 are designated deload weeks
- **Invariant**: `isDeloadWeek = (weekNumber % 7 == 0)`
- **Behavior**: Reduced volume and/or intensity for recovery
- **Typical Deload**: 50-70% of normal volume, same or reduced intensity

### 1.4 Training Days
- **Rule**: Programs support 2-6 training days per week
- **Default**: 4 days per week
- **Constraint**: `2 <= trainingDaysPerWeek <= 6`
- **Validation**: Each main lift must be trained at least once per week

---

## 2. Training Max (TM) Rules

### 2.1 Initial Training Max Setup
- **Rule**: TM should be set at 85-90% of known or estimated 1RM
- **Recommendation**: When in doubt, start conservative (85%)
- **Formula**: `initialTM = known1RM * 0.85` to `known1RM * 0.90`

### 2.2 Training Max Bounds
- **Minimum**: Must be positive (`TM > 0`)
- **Maximum**: Should not exceed 100% of estimated 1RM
- **Validation**: `0 < TM <= estimated1RM`

### 2.3 Training Max Update Frequency
- **Rule**: TM is evaluated and potentially updated after each workout where the exercise is performed
- **Invariant**: TM updates are atomic and immediate upon workout completion

---

## 3. Progression Algorithms

### 3.1 RTF (Reps To Failure) Progression

This is the primary A2S 2.0 progression method. The final set is taken to technical failure, and the TM adjusts based on performance vs. target.

#### AMRAP Set Rules
- **Rule**: The final set is ALWAYS taken as AMRAP (As Many Reps As Possible)
- **Definition**: AMRAP means performing reps until form breakdown or muscular failure
- **Invariant**: `sets[lastIndex].isAMRAP = true`

#### Rep Target Calculation
```
repTarget = baseRepsForWeek - (currentBlock - 1) * blockRepReduction

Example (Week 5, Block 1):
- Base reps for week 5 might be 8
- Block 1, so no reduction
- Target = 8 reps
```

#### Training Max Adjustment Formula (RTF)

| Reps vs Target | TM Adjustment |
|----------------|---------------|
| +5 or more | +3.0% |
| +4 | +2.0% |
| +3 | +1.5% |
| +2 | +1.0% |
| +1 | +0.5% |
| 0 (hit target) | 0% (no change) |
| -1 | -2.0% |
| -2 or worse | -5.0% |

**Formula**:
```
repsOverTarget = actualReps - targetReps

if repsOverTarget >= 5:
    adjustment = 0.03
elif repsOverTarget == 4:
    adjustment = 0.02
elif repsOverTarget == 3:
    adjustment = 0.015
elif repsOverTarget == 2:
    adjustment = 0.01
elif repsOverTarget == 1:
    adjustment = 0.005
elif repsOverTarget == 0:
    adjustment = 0.0
elif repsOverTarget == -1:
    adjustment = -0.02
else:  # -2 or worse
    adjustment = -0.05

newTM = currentTM * (1 + adjustment)
```

#### Example Calculations

**Example 1: Beat target by 3 reps**
```
Current TM: 100 kg
Target Reps: 6
Actual Reps: 9
Reps Over: +3
Adjustment: +1.5%
New TM: 100 * 1.015 = 101.5 kg
```

**Example 2: Missed target by 2 reps**
```
Current TM: 100 kg
Target Reps: 6
Actual Reps: 4
Reps Over: -2
Adjustment: -5.0%
New TM: 100 * 0.95 = 95 kg
```

**Example 3: Hit target exactly**
```
Current TM: 100 kg
Target Reps: 6
Actual Reps: 6
Reps Over: 0
Adjustment: 0%
New TM: 100 kg (no change)
```

### 3.2 RIR (Reps In Reserve) Progression

Alternative method based on completing sets until reaching an RIR threshold.

#### Set Threshold Rules
- **Default Min Sets**: 4
- **Default Max Sets**: 6
- **Target RIR**: 2-3 (stop set when 2-3 reps remain "in the tank")

#### Progression Logic
```
if setsCompletedBeforeRIRTarget < minSetsThreshold:
    adjustment = -0.05  # Decrease TM 5%
elif setsCompletedBeforeRIRTarget > maxSetsThreshold:
    adjustment = +0.02  # Increase TM 2%
else:
    adjustment = 0.0    # Maintain TM
```

#### Customizable Thresholds
- **Constraint**: `minSetsThreshold < maxSetsThreshold`
- **Validation**: `1 <= minSetsThreshold <= 10`
- **Validation**: `2 <= maxSetsThreshold <= 15`

### 3.3 Reps Per Set Progression

This progression method is used for accessory exercises where percentage-based training doesn't apply (e.g., lat pulldowns, curls, tricep extensions). Users progressively add sets until reaching a target, then increase weight.

#### Configuration Parameters
- **Rep Range**: Minimum-Target-Maximum (e.g., 8-10-12)
  - `minimumReps`: Lowest acceptable reps per set
  - `targetReps`: Ideal rep count
  - `maximumReps`: Upper bound for rep range
- **Starting Sets**: Initial set count (typically 2)
- **Target Sets**: Goal set count before weight increase (typically 4)
- **Starting Weight**: User-provided weight for first session
- **Weight Increment**: Based on equipment type

#### Progression Algorithm

```
Performance Evaluation:
- SUCCESS: All sets hit maximumReps
- MAINTAINED: All sets hit at least minimumReps
- FAILED: Any set falls below minimumReps

Progression Logic:
if performance == SUCCESS:
    if currentSets < targetSets:
        currentSets += 1  # Add one set
    else:
        weight += getWeightIncrement()
        currentSets = startingSets  # Reset to minimum

elif performance == FAILED:
    if currentSets > 1:
        currentSets -= 1  # Remove one set
    else:
        weight -= getWeightIncrement()  # Reduce weight

else:  # MAINTAINED
    # No change - keep building proficiency
```

#### Equipment-Based Weight Increments

| Equipment Type | Increment Rules |
|----------------|-----------------|
| Barbell | Configured increment (typically 2.5kg) |
| Smith Machine | Configured increment (typically 2.5kg) |
| Dumbbell | 1kg if weight < 10kg, else 2kg |
| Cable/Machine | Configured increment (typically 2.5kg) |
| Bodyweight | N/A (progression via sets/reps only) |

**Formula**:
```
if equipmentType == Dumbbell:
    increment = 1kg if currentWeight < 10kg else 2kg
else:
    increment = configuredWeightProgression
```

#### Example Progression Sequence

**Scenario**: Lat Pulldown, Rep Range 8-10-12, Starting Sets 2, Target Sets 4

| Week | Sets | Weight | Performance | Action |
|------|------|--------|-------------|--------|
| 1 | 2 | 50kg | 2x12 (all hit max) | Add set → 3 sets |
| 2 | 3 | 50kg | 3x12 (all hit max) | Add set → 4 sets |
| 3 | 4 | 50kg | 4x12 (all hit max) | Increase weight, reset sets |
| 4 | 2 | 52.5kg | 2x11 (maintained) | No change |
| 5 | 2 | 52.5kg | 2x12 (all hit max) | Add set → 3 sets |
| 6 | 3 | 52.5kg | 1x12, 1x11, 1x7 (failed) | Remove set → 2 sets |

#### Validation Rules
- **Rep Range**: `1 <= minimumReps < targetReps < maximumReps <= 20`
- **Set Range**: `1 <= startingSets <= targetSets <= 10`
- **Weight**: `weight >= 0` (bodyweight can be 0)
- **Invariant**: All sets in a session use the same weight

#### Week 1 Special Handling
For Reps Per Set exercises in the first session:
1. User must provide actual starting weight (cannot be pre-calculated)
2. System records this weight as baseline
3. No progression applied in Week 1 (establishing baseline)
4. Subsequent weeks follow normal progression logic

#### Edge Cases

**Scenario 1: Stuck at minimum sets and weight**
- User repeatedly fails at minimum configuration
- **Solution**: Suggest form check or exercise substitution

**Scenario 2: Rapid progression (hitting max sets immediately)**
- User is too strong for starting weight
- **Solution**: Allow user to manually set higher starting weight

**Scenario 3: Bodyweight exercises**
- Weight is always 0 (or bodyweight)
- **Progression**: Use sets/reps only, no weight increase
- **Alternative**: Add external load (weight vest, dip belt)

### 3.4 Linear Progression (Novice)

Simpler progression for newer lifters with fixed set/rep schemes.

#### Progression Trigger
```
estimatedRepsInReserve = userEstimate after final set

if estimatedRepsInReserve >= upperThreshold:
    adjustment = +0.02  # Increase 2% (default)
elif estimatedRepsInReserve <= lowerThreshold:
    adjustment = -0.05  # Decrease 5% (default)
else:
    adjustment = 0.0
```

#### Default Thresholds
- **Upper threshold**: 4+ RIR (felt too easy)
- **Lower threshold**: 0 RIR (went to failure unexpectedly)

---

## 4. Set and Rep Rules

### 4.1 Standard Set Structure
- **Non-AMRAP sets**: Perform target reps exactly, same weight
- **AMRAP set**: Final set, same weight, max reps
- **Invariant**: All sets in an exercise use the same weight

### 4.2 Set Count Rules
| Program Variant | Default Sets | Adjustable Range |
|-----------------|--------------|------------------|
| RTF | 4 + AMRAP | 3-6 + AMRAP |
| RIR | 4-6 | 3-10 (threshold based) |
| Linear | 4 | 3-5 |

### 4.3 Rep Range by Block
| Block | Typical Rep Range |
|-------|-------------------|
| 1 | 8-12 reps |
| 2 | 5-8 reps |
| 3 | 3-5 reps |

**Invariant**: `block3Reps < block2Reps < block1Reps`

### 4.4 Weight Calculation
```
workingWeight = TM * intensityPercentage

Where intensityPercentage varies by:
- Week within block
- Block number
- Exercise category (main vs auxiliary)
```

---

## 5. Exercise Category Rules

### 5.1 Main Lifts (T1)
- Squat, Bench Press, Deadlift, Overhead Press
- **Rule**: Must be included in every program
- **Frequency**: At least 1x per week each
- **AMRAP**: Always performed on main lifts

### 5.2 Auxiliary Lifts (T2)
- Close variations of main lifts (e.g., Front Squat, Close-Grip Bench)
- **Rule**: AMRAP optional (configurable)
- **TM Progression**: Same algorithm as main lifts

### 5.3 Accessories (T3)
- Isolation and supplementary work
- **Rule**: No TM tracking required
- **Progression**: Simple rep/weight targets, user discretion

---

## 6. Validation Rules

### 6.1 Workout Logging Validation
```
- actualReps >= 0
- weight > 0
- setNumber >= 1
- if isAMRAP: actualReps must be provided
- if not isAMRAP: actualReps should equal targetReps (warning if different)
```

### 6.2 Program Setup Validation
```
- All main lifts must have initial TM
- TM must be positive number
- Training days must be between 2-6
- At least one exercise per training day
```

### 6.3 Progression Validation
```
- Cannot apply progression without completing AMRAP set
- TM adjustment cannot exceed +/-10% in single session
- New TM must remain positive
```

---

## 7. State Transition Rules

### 7.1 Workout States
```
Scheduled → InProgress → Completed
         └→ Skipped

Transitions:
- Scheduled → InProgress: User starts workout
- InProgress → Completed: All exercises logged
- Scheduled → Skipped: User marks as skipped
- InProgress → Skipped: User abandons mid-workout (with confirmation)
```

### 7.2 Week States
```
Pending → InProgress → Completed

Transitions:
- Pending → InProgress: First workout of week started
- InProgress → Completed: All scheduled workouts done OR skipped
```

### 7.3 Program States
```
Created → Active → Completed
              └→ Paused → Active
              └→ Abandoned

Transitions:
- Created → Active: User starts program
- Active → Paused: User pauses (vacation, injury, etc.)
- Paused → Active: User resumes
- Active → Completed: Week 21 finished
- Active → Abandoned: User quits early
```

---

## 8. Constraint Summary

### Hard Constraints (Invariants)
1. TM must always be positive
2. AMRAP is always the final set
3. Blocks progress in intensity (1 < 2 < 3)
4. Deload weeks occur every 7th week
5. Cannot exceed +/-10% TM adjustment per session
6. All sets in an exercise use same weight

### Soft Constraints (Warnings)
1. TM should be 85-95% of estimated 1RM
2. Non-AMRAP sets should hit target reps
3. Missing 3+ workouts in a row triggers pause suggestion
4. Large TM decreases (>10% cumulative) suggest TM reset

### Business Policy Constraints
1. One active program per user at a time
2. Program must be explicitly started (not auto-start)
3. Completed programs are immutable (read-only)
4. Historical data retained indefinitely

---

## 9. Edge Cases

### 9.1 Week Transitions
- **Scenario**: User completes Wednesday workout, skips Friday
- **Rule**: Week marked complete if all workouts either completed OR skipped
- **TM Effect**: Skipped workouts do not affect TM

### 9.2 Partial Workout Completion
- **Scenario**: User completes 3 of 5 exercises, abandons workout
- **Rule**: Partial data is saved, workout marked incomplete
- **TM Effect**: Only completed exercises get TM updates

### 9.3 Deload Week Handling
- **Scenario**: User performs well on deload, wants TM increase
- **Options**:
  1. Deload weeks do not affect TM (strict interpretation)
  2. Deload weeks can trigger TM increase but not decrease (lenient)
- **Recommendation**: Make this configurable per user preference

### 9.4 Program Reset/Restart
- **Scenario**: User wants to restart program from week 1
- **Rule**: Creates new program instance, preserves old data
- **TM Handling**: Can carry over current TM or reset to original

### 9.5 Exercise Substitution
- **Scenario**: User cannot perform Barbell Squat, substitutes Safety Bar
- **Rule**: Each exercise maintains separate TM
- **Progression**: Substitute exercise follows same progression rules

---

## 10. Configuration Parameters

### Default Values (Adjustable)
```yaml
program:
  totalWeeks: 21
  blocksCount: 3
  weeksPerBlock: 7
  trainingDaysPerWeek: 4

progression:
  rtf:
    repBonusTable:
      5: 0.03
      4: 0.02
      3: 0.015
      2: 0.01
      1: 0.005
      0: 0.0
      -1: -0.02
      -2: -0.05
    maxAdjustmentPercent: 0.10

  rir:
    minSetsThreshold: 4
    maxSetsThreshold: 6
    increasePercent: 0.02
    decreasePercent: -0.05

  linear:
    upperRIRThreshold: 4
    lowerRIRThreshold: 0
    increasePercent: 0.02
    decreasePercent: -0.05

trainingMax:
  initialPercentOf1RM: 0.90
  minimumWeight: 20  # lbs or kg based on unit
  roundingIncrement: 2.5  # lbs or 1 kg

sets:
  defaultSetsPerExercise: 4
  minSetsPerExercise: 3
  maxSetsPerExercise: 8
```

---

## 11. Rounding Rules

### Weight Rounding
```
roundedWeight = round(calculatedWeight / increment) * increment

Example:
- Calculated: 102.3 kg
- Increment: 2.5 kg
- Rounded: 102.5 kg
```

### TM Rounding
```
newTM = round(currentTM * (1 + adjustment) / increment) * increment
```

### Percentage Display
```
displayPercentage = round(percentage * 100, 1) + "%"
```
