# Use Cases: A2S Workout Tracking Application

## Executive Summary

This document defines the key user journeys and scenarios for the A2S Workout Tracking Application. It covers the complete lifecycle from program creation through completion, including happy paths and error scenarios.

---

## Actors

### Primary Actor: Lifter (User)
- Individual using the application to track their strength training
- May be novice to advanced experience level
- Uses application on mobile or web

### Secondary Actors
- System (automated processes like TM calculations)
- Timer (rest period tracking)

---

## Use Case Categories

1. **Program Management** - Creating, starting, pausing programs
2. **Workout Execution** - Performing and logging workouts
3. **Progression Tracking** - TM adjustments and progress visualization
4. **Data Management** - History, export, settings

---

## UC-1: Create New Training Program

### Description
User creates a new A2S training program with their exercise selections and initial training maxes.

### Preconditions
- User has an account (or is using app locally)
- No active program exists (or user confirms replacement)

### Main Flow
1. User selects "Create New Program"
2. System prompts for program variant (RTF, RIR, Linear)
3. User selects variant
4. System displays default exercises for each training day
5. User reviews and optionally modifies exercise selections:
   a. Add/remove exercises
   b. Reorder exercises within days
   c. Substitute exercises
6. System prompts for Training Max for each exercise
7. User enters known 1RM or estimated max for each exercise
8. System calculates initial TM (typically 90% of entered value)
9. User confirms settings
10. System creates program in "Created" state
11. System displays program overview

### Alternative Flows

**4a. User wants custom days**
1. User selects "Customize Training Days"
2. System allows adding/removing days (2-6)
3. User assigns exercises to custom days
4. Continue from step 5

**7a. User doesn't know their 1RM**
1. User selects "Estimate 1RM"
2. System prompts for recent set (weight x reps)
3. System calculates estimated 1RM using Brzycki or similar formula
4. Continue from step 8

**9a. User wants to adjust TM percentage**
1. User selects "Advanced Settings"
2. User adjusts TM percentage (default 90%, range 80-95%)
3. System recalculates all TMs
4. Continue from step 9

### Postconditions
- New program exists in Created state
- All exercises have initial TM values
- Program ready to be started

### Business Rules Applied
- BR-2.1: Initial TM at 85-90% of 1RM
- BR-4.2: Set count rules applied
- BR-5.1: Main lifts must be included

---

## UC-2: Start Training Program

### Description
User activates their program and begins Week 1.

### Preconditions
- Program exists in Created state
- All required configuration complete

### Main Flow
1. User opens program
2. User selects "Start Program"
3. System confirms program start
4. System transitions program to Active state
5. System creates Week 1 with status Pending
6. System displays Week 1 schedule
7. System shows first scheduled workout

### Alternative Flows

**2a. User has existing active program**
1. System warns about existing program
2. User confirms abandonment of existing program
3. System marks existing program as Abandoned
4. Continue from step 3

### Postconditions
- Program in Active state
- Week 1 in Pending state
- Workouts scheduled for Week 1

### Domain Events Raised
- `ProgramStarted`
- `WeekStarted`

---

## UC-3: Complete a Workout Day

### Description
User performs and logs all exercises for a single training day.

### Preconditions
- Active program exists
- Current week has scheduled workouts
- Workout day is scheduled (not already completed)

### Main Flow
1. User opens today's workout (or selects a workout day)
2. System displays list of exercises with target sets/reps/weights
3. User selects "Start Workout"
4. System starts session timer
5. For each exercise:
   a. System displays exercise with targets
   b. User performs and logs each set:
      - For non-AMRAP sets: confirms completion (or logs actual reps if different)
      - For AMRAP set: enters actual reps performed
   c. System records set data
   d. System provides rest timer between sets (optional)
   e. After all sets, system marks exercise complete
   f. System calculates and displays TM adjustment (if applicable)
6. After all exercises complete:
   a. System marks workout as Completed
   b. System applies all TM adjustments atomically
   c. System records workout duration
7. System displays workout summary with:
   - Total volume
   - TM changes
   - Personal records (if any)

### Alternative Flows

**5b-1. User completes more reps than target on non-AMRAP set**
1. User logs actual reps (e.g., 10 instead of target 8)
2. System records actual value
3. System shows warning (optional: "Consider increasing weight?")
4. Continue normally

**5b-2. User fails to complete target reps on non-AMRAP set**
1. User logs actual reps (e.g., 6 instead of target 8)
2. System records actual value
3. System shows concern indicator
4. Continue normally (AMRAP result will determine TM adjustment)

**5b-3. User needs to change weight mid-exercise**
1. User selects "Adjust Weight"
2. User enters new weight
3. System records weight change
4. Continue with new weight

**5e-1. User skips an exercise**
1. User selects "Skip Exercise"
2. System prompts for reason (optional)
3. System marks exercise as skipped
4. No TM adjustment for skipped exercise
5. Continue to next exercise

**6-1. User abandons workout partway through**
1. User selects "End Workout"
2. System confirms intention
3. System saves partial data
4. Completed exercises get TM adjustments
5. Workout remains in InProgress state
6. User can resume later

### Postconditions
- Workout marked as Completed
- All exercise performances recorded
- TM adjustments applied
- Volume/analytics updated

### Domain Events Raised
- `WorkoutStarted`
- `ExerciseCompleted` (per exercise)
- `WorkoutCompleted`
- `TrainingMaxIncreased/Decreased/Maintained` (per exercise)

### Business Rules Applied
- BR-3.1: RTF progression algorithm
- BR-4.1: Standard set structure
- BR-6.1: Workout logging validation

---

## UC-4: Record AMRAP Set Result

### Description
User completes the final (AMRAP) set for an exercise and the system calculates progression.

### Preconditions
- Workout in progress
- All non-AMRAP sets for exercise completed
- Exercise uses AMRAP progression

### Main Flow
1. System displays AMRAP set prompt
2. System shows:
   - Weight to use
   - Target reps (minimum to maintain TM)
   - Current TM for context
3. User performs set to near-failure
4. User enters actual reps completed
5. System calculates reps vs target
6. System determines TM adjustment using progression table
7. System displays:
   - Performance feedback (e.g., "+3 reps over target!")
   - TM change preview (e.g., "+1.5% TM")
   - New TM value (e.g., "New TM: 102.5 kg")
8. System stores AMRAP result
9. System queues TM adjustment (applied at workout end)

### Alternative Flows

**4a. User performed 0 reps (failed immediately)**
1. User enters 0
2. System calculates -2 or worse
3. System shows 5% TM decrease
4. System suggests: "Consider reducing TM manually or taking deload"

**4b. User exceeded max tracking (+10 or more)**
1. User enters large number
2. System caps at +5 for calculation
3. System applies +3% (maximum increase)
4. System suggests: "TM may be too conservative"

**4c. User wants to record RPE**
1. After entering reps, system prompts for optional RPE
2. User enters RPE (1-10)
3. System stores for analytics
4. Continue normally

### Postconditions
- AMRAP result recorded
- TM adjustment calculated
- Adjustment queued for workout completion

### Validation
- Reps must be >= 0
- Reps should not exceed 30 (warning if exceeded)

---

## UC-5: Progress to Next Week

### Description
System transitions from one training week to the next.

### Preconditions
- All scheduled workouts for current week are Completed or Skipped
- Current week < 21 (not final week)

### Main Flow
1. User completes final workout of week
2. System marks current week as Completed
3. System checks if this is week 7/14/21 (deload week completion)
4. System creates next week with appropriate parameters:
   - New rep targets (based on week number)
   - Intensity percentages
   - Updated working weights (from new TMs)
5. System transitions to next week as Active
6. If block transition (week 7 or 14):
   a. System updates block number
   b. System applies block-specific intensity changes
7. System displays next week preview

### Alternative Flows

**3a. Completing deload week (7, 14, 21)**
1. System acknowledges deload completion
2. For weeks 7 and 14: Continue to next block
3. For week 21: Trigger UC-6 (Program Completion)

**4a. Significant TM changes during week**
1. System recalculates all working weights for next week
2. Large changes (>10% cumulative) trigger warning
3. User can confirm or request TM review

### Postconditions
- Previous week marked Completed
- New week created and Active
- All exercise targets updated

### Domain Events Raised
- `WeekCompleted`
- `WeekStarted`
- `BlockCompleted` (if applicable)

---

## UC-6: Complete Training Program

### Description
User finishes the 21-week program.

### Preconditions
- Week 21 completed
- All blocks completed

### Main Flow
1. User completes final workout of Week 21
2. System processes final week completion
3. System marks program as Completed
4. System generates program summary:
   - Starting vs ending TMs
   - Total volume lifted
   - Progression over time
   - PRs set during program
5. System presents completion celebration
6. System offers options:
   - View detailed analytics
   - Start new program (with current TMs)
   - Export data

### Alternative Flows

**2a. Program ended early (abandoned)**
1. User selects "End Program"
2. System confirms abandonment
3. System marks program as Abandoned
4. System still generates partial summary
5. Data preserved for history

### Postconditions
- Program in Completed or Abandoned state
- All data preserved
- Program becomes read-only

### Domain Events Raised
- `ProgramCompleted`
- `BlockCompleted` (Block 3)
- `WeekCompleted` (Week 21)

---

## UC-7: Handle Failed Workout

### Description
User significantly underperforms or cannot complete a workout.

### Preconditions
- Active workout in progress
- User experiencing difficulty

### Main Flow
1. User logs sets with reps significantly below target
2. For AMRAP set, user fails to hit target reps
3. System calculates TM decrease per algorithm:
   - -1 rep: -2% TM
   - -2 or more: -5% TM
4. System applies TM decrease
5. System displays adjusted TM
6. System provides supportive feedback:
   - "Bad days happen"
   - "Next workout will be more manageable"
   - "Consider if you need extra rest"

### Alternative Flows

**2a. User fails multiple exercises in single workout**
1. System applies TM decrease to each exercise
2. System notes pattern
3. After workout, suggests: "Consider if recovery is adequate"

**2b. User fails same exercise multiple weeks in row**
1. System detects pattern
2. System suggests: "Consider manual TM reduction of 10%"
3. System offers one-click TM reset option

### Postconditions
- Reduced TMs applied
- Pattern tracked for analysis
- User supported with actionable feedback

---

## UC-8: Skip Workout Day

### Description
User cannot complete a scheduled workout and marks it as skipped.

### Preconditions
- Scheduled workout exists
- Workout not yet completed

### Main Flow
1. User opens scheduled workout
2. User selects "Skip Workout"
3. System prompts for optional reason:
   - Illness
   - Injury
   - Time constraint
   - Life event
   - Other
4. User confirms skip
5. System marks workout as Skipped
6. System does NOT adjust any TMs
7. System checks if week can still be completed
8. Week remains in progress until other workouts handled

### Alternative Flows

**7a. All remaining workouts skipped**
1. System marks week as Completed (with skipped workouts noted)
2. System allows progression to next week

**7b. Multiple consecutive skips**
1. After 3+ skipped workouts, system suggests: "Consider pausing program"
2. User can continue skipping or pause

### Postconditions
- Workout marked as Skipped
- No TM changes
- Skip reason recorded (if provided)

### Domain Events Raised
- `WorkoutSkipped`

---

## UC-9: Pause and Resume Program

### Description
User temporarily pauses their program and later resumes.

### Preconditions
- Active program exists

### Main Flow (Pause)
1. User selects "Pause Program"
2. System prompts for expected duration (optional)
3. User confirms pause
4. System marks program as Paused
5. System records pause date
6. System stops week progression

### Main Flow (Resume)
1. User opens paused program
2. User selects "Resume Program"
3. System asks: "Resume where you left off?"
4. User confirms
5. System reactivates program
6. If pause was > 2 weeks:
   a. System suggests: "Consider reducing TMs by 5-10%"
   b. User can accept suggestion or continue as-is
7. System shows current workout

### Alternative Flows

**6a. Long pause (> 4 weeks)**
1. System strongly recommends TM reduction
2. System offers: "Start fresh with current week reset"
3. User chooses approach

### Postconditions
- Program in Paused or Active state
- Resume date recorded
- TM adjustments applied if chosen

### Domain Events Raised
- `ProgramPaused`
- `ProgramResumed`

---

## UC-10: View Progress and Analytics

### Description
User reviews their training progress over time.

### Preconditions
- Historical workout data exists

### Main Flow
1. User selects "View Progress"
2. System displays dashboard with:
   - Current TMs for each exercise
   - TM progression graphs over time
   - Volume trends (sets, reps, tonnage)
   - Current week/block position
3. User can filter by:
   - Exercise
   - Date range
   - Block
4. User can drill into specific exercises to see:
   - All AMRAP results
   - TM change history
   - Session-by-session performance

### Alternative Flows

**2a. No historical data**
1. System displays: "Complete your first workout to see progress"
2. Offers quick link to start workout

### Postconditions
- None (read-only operation)

---

## UC-11: Substitute Exercise

### Description
User replaces an exercise with an alternative.

### Preconditions
- Active program exists
- Exercise to be replaced identified

### Main Flow
1. User opens exercise in program settings
2. User selects "Substitute Exercise"
3. System displays compatible alternatives:
   - Same movement pattern
   - Similar muscle groups
4. User selects replacement
5. System prompts for new TM:
   - Carry over current TM (if similar exercise)
   - Enter new TM
   - Estimate from recent performance
6. User confirms substitution
7. System updates program with new exercise
8. Old exercise data preserved in history

### Alternative Flows

**3a. User wants custom exercise**
1. User selects "Add Custom Exercise"
2. User enters exercise details:
   - Name
   - Category
   - Movement pattern
   - Notes
3. System creates custom exercise
4. Continue from step 5

**5a. Substituting for injury**
1. User indicates injury substitution
2. System marks as temporary substitution
3. System can prompt to revert after recovery

### Postconditions
- New exercise in program
- TM established for new exercise
- Historical data linked

---

## UC-12: Manually Adjust Training Max

### Description
User overrides the calculated TM for an exercise.

### Preconditions
- Exercise exists in program with TM

### Main Flow
1. User opens exercise settings
2. User selects "Adjust Training Max"
3. System displays:
   - Current TM
   - Historical TM values
   - Recent AMRAP performances
4. User enters new TM value
5. System validates: new TM > 0, reasonable range
6. System applies new TM
7. System recalculates working weights for current/future weeks
8. System logs manual adjustment with timestamp

### Alternative Flows

**5a. TM seems unreasonable**
1. System warns: "This TM is X% different from calculated"
2. User confirms or adjusts
3. Continue normally

### Postconditions
- New TM applied
- Working weights recalculated
- Adjustment logged in history

### Business Rules Applied
- BR-2.2: TM bounds validation
- BR-6.3: Progression validation

---

## UC-13: Deload Week Handling

### Description
User completes the reduced-volume deload week.

### Preconditions
- Current week is 7, 14, or 21
- Entering deload week

### Main Flow
1. System presents deload week schedule:
   - Reduced volume (typically 50-60%)
   - Same or slightly reduced intensity
   - Same exercises
2. User performs deload workouts
3. System tracks completion but:
   - Option A: No AMRAP sets during deload
   - Option B: AMRAP sets don't affect TM
4. User completes deload week
5. System advances to next block (or completes program for week 21)

### Alternative Flows

**1a. User wants to skip deload**
1. User selects "Skip Deload Week"
2. System warns about recovery importance
3. If confirmed, system marks week complete
4. TMs unchanged

**3a. User performs well on deload AMRAP**
1. System records performance
2. System does NOT increase TM (deload policy)
3. System notes strong deload for analytics

### Postconditions
- Deload week completed
- Ready for next block
- Recovery period acknowledged

---

## Scenario Walkthroughs

### Scenario 1: First Week New Lifter

**Context**: New user starting A2S Linear Progression

1. Creates program with 4-day template
2. Enters estimated 1RMs from gym experience:
   - Squat: 100 kg
   - Bench: 80 kg
   - Deadlift: 140 kg
   - OHP: 50 kg
3. System calculates TMs at 90%:
   - Squat TM: 90 kg
   - Bench TM: 72 kg
   - Deadlift TM: 126 kg
   - OHP TM: 45 kg
4. Week 1 Day 1: Squat day
   - 4 sets x 8 reps @ 67.5 kg (75% of TM)
   - AMRAP set @ 67.5 kg: Hits 12 reps (+4 over target)
   - System calculates: +2% TM
   - New Squat TM: 91.8 kg (rounds to 92 kg)
5. Continues through week, building momentum

### Scenario 2: Mid-Program Stall

**Context**: Week 12, lifter struggling with bench press

1. Week 10: Bench AMRAP - hit target exactly (0 adjustment)
2. Week 11: Bench AMRAP - missed by 1 rep (-2% TM)
3. Week 12: Bench AMRAP - missed by 2 reps (-5% TM)
4. System detects pattern
5. Suggests: "Consider manual TM adjustment for Bench Press"
6. User reduces TM by additional 5%
7. Week 13: Bench AMRAP - exceeds by 2 reps (+1% TM)
8. Back on track with manageable weights

### Scenario 3: Vacation Mid-Program

**Context**: Week 8, user going on 2-week vacation

1. User completes Week 8 Day 3
2. User pauses program
3. Records expected return date
4. Two weeks pass
5. User resumes program
6. System suggests 5% TM reduction
7. User accepts
8. TMs adjusted
9. Week 8 Day 4 continues with adjusted weights
10. User successfully completes workout

### Scenario 4: Injury Substitution

**Context**: Week 5, user has shoulder issue

1. User cannot perform overhead press
2. Opens exercise settings
3. Substitutes OHP with Landmine Press
4. Enters new TM estimate: 40 kg
5. Continues program with substitution
6. Week 10: Shoulder recovered
7. Reverts to OHP
8. Enters conservative TM: 42 kg
9. Progression continues

---

## Error Scenarios

### E-1: Network Loss During Workout
- **Trigger**: Connection lost while logging sets
- **Handling**: All data queued locally
- **Resolution**: Auto-sync when connection restored
- **User Experience**: Seamless, with sync indicator

### E-2: Invalid Rep Entry
- **Trigger**: User enters negative reps or impossible value
- **Handling**: Validation error displayed
- **Resolution**: User corrects input
- **Prevention**: Number-only keyboard, reasonable max (99)

### E-3: Duplicate Workout Submission
- **Trigger**: User accidentally submits workout twice
- **Handling**: System detects duplicate by timestamp/ID
- **Resolution**: Second submission rejected
- **User Experience**: "Workout already recorded" message

### E-4: Program Data Corruption
- **Trigger**: App crash during TM calculation
- **Handling**: Transactional updates (all-or-nothing)
- **Resolution**: Rollback to last consistent state
- **Prevention**: Robust transaction handling
