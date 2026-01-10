# Recommendations: A2S Workout Tracking Application

## Executive Summary

This document synthesizes findings from the research phase and provides concrete recommendations for the A2S Workout Tracking Application. It identifies gaps in the initial understanding, proposes the optimal approach, and outlines next steps.

---

## 1. Research Findings Summary

### 1.1 Domain Clarity

The A2S (Average 2 Savage / Stronger By Science) methodology is well-defined with clear rules:

| Aspect | Clarity | Notes |
|--------|---------|-------|
| Program structure | High | 21 weeks, 3 blocks, deload weeks |
| Progression algorithm (RTF) | High | Well-documented formulas |
| Progression algorithm (RIR) | Medium | Set thresholds need clarification |
| Progression algorithm (Linear) | Medium | Simpler but less documented |
| Workout structure | High | Sets/reps/AMRAP clear |
| TM management | High | Percentage-based adjustments |

### 1.2 Key Domain Concepts Confirmed

1. **Training Max (TM)** is the core concept - all programming derives from it
2. **AMRAP sets** are the primary autoregulation mechanism for RTF variant
3. **Block periodization** (3 x 7 weeks) structures intensity progression
4. **Deload weeks** are mandatory for recovery (every 7th week)
5. **Single-session TM adjustments** maintain momentum without large swings

### 1.3 Identified Gaps in Original Understanding

| Gap | Impact | Recommendation |
|-----|--------|----------------|
| No specification document existed | Created from research | Validate with user |
| RIR variant set thresholds | Medium | Default 4-6, make configurable |
| Deload week TM policy | Low | Recommend no TM changes during deload |
| Exercise substitution handling | Low | Maintain separate TM per exercise |
| Multi-device sync strategy | High | Local-first for v1, defer cloud |
| Exact rep targets per week | Medium | Need block-specific rep table |

---

## 2. Specification Completeness Assessment

### 2.1 What We Now Have

After research, we have comprehensive documentation for:
- Domain model with entities, value objects, aggregates
- Business rules including progression algorithms
- Use cases covering key user journeys
- Architecture approach comparison
- Constraints and risks analysis
- Integration analysis and extensibility

### 2.2 What Remains Unclear

| Item | Status | Action Needed |
|------|--------|---------------|
| Exact rep targets per week/block | Unclear | Define specific tables |
| Intensity percentages per week | Unclear | Research or derive |
| User preferences for units (kg/lbs) | Assumed | Add to requirements |
| Multiple program history | Assumed | One at a time confirmed |
| Accessory exercise handling | Assumed | Simplified tracking |

### 2.3 Assumptions Made During Research

1. **Single active program at a time** - User focuses on one program
2. **4 training days default** - Can be configured to 2-6
3. **English only for v1** - Internationalization deferred
4. **Mobile-first** - Web dashboard is secondary
5. **Offline-first** - Core use case is gym with poor connectivity
6. **No social features v1** - Personal tracking only

---

## 3. Recommended Architecture

### 3.1 Overall Approach

**Recommendation: Clean Architecture with Domain-Driven Design**

```
┌─────────────────────────────────────────────────────────────┐
│                      Presentation Layer                      │
│                                                              │
│   React Native (Mobile)  │  React (Web, future)             │
│   - Screens              │  - Pages                         │
│   - Components           │  - Components                    │
│   - State Management     │  - State Management              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Application Layer                       │
│                                                              │
│   Use Cases / Commands / Queries                            │
│   - CreateProgram         - GetCurrentWorkout               │
│   - RecordSet             - GetProgressAnalytics            │
│   - CompleteWorkout       - GetTrainingMaxHistory           │
│   - AdjustTrainingMax                                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        Domain Layer                          │
│                                                              │
│   Aggregates              Value Objects                      │
│   - WorkoutProgram        - Weight                          │
│   - WorkoutSession        - TrainingMaxAdjustment           │
│   - TrainingMaxLedger     - SetResult                       │
│                                                              │
│   Domain Services         Domain Events                      │
│   - ProgressionCalculator - WorkoutCompleted                │
│   - WeekGenerator         - TrainingMaxUpdated              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                     │
│                                                              │
│   Repositories            External Services                  │
│   - SQLiteProgramRepo     - NotificationService             │
│   - SQLiteSessionRepo     - AnalyticsService                │
│   - SQLiteTMRepo          - ExportService                   │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Aggregate Boundaries

**Recommendation: Three Primary Aggregates**

1. **ProgramConfiguration** (Aggregate Root)
   - Program settings
   - Exercise list with current TMs
   - Current state (week, status)
   - Immutable after program starts (except TM updates)

2. **WorkoutSession** (Aggregate Root)
   - Single workout's performance data
   - All sets recorded
   - TM adjustments calculated
   - Small, focused, offline-friendly

3. **WeeklySchedule** (Aggregate Root)
   - Planned workouts for a week
   - Derived from TMs and week number
   - Can be regenerated if TMs change

**Rationale:**
- Small aggregates support offline recording
- Clear boundaries reduce locking contention
- Each aggregate has single responsibility

### 3.3 WeeklyExercisePlan Decision

**Recommendation: Embed as Value Object**

The `WeeklyExercisePlan` should be embedded within `WeeklySchedule` because:
- It is derived data (calculated from TM + week parameters)
- Never modified independently
- Always loaded and displayed together with schedule
- Simple value semantics

```typescript
// Value Object
interface WeeklyExercisePlan {
  readonly exerciseId: string;
  readonly sets: number;
  readonly reps: number;
  readonly intensityPercent: number;
  readonly workingWeight: Weight;
}

// Aggregate
interface WeeklySchedule {
  readonly weekNumber: number;
  readonly blockNumber: number;
  readonly exercises: ReadonlyArray<WeeklyExercisePlan>;
  readonly status: WeekStatus;
}
```

---

## 4. Technical Recommendations

### 4.1 Technology Stack

| Layer | Recommendation | Rationale |
|-------|----------------|-----------|
| Mobile | React Native + TypeScript | Cross-platform, strong typing |
| Database | SQLite via react-native-sqlite-storage | Mature, reliable, offline |
| State | Zustand or Jotai | Lightweight, TypeScript friendly |
| Navigation | React Navigation | Standard for RN |
| Testing | Jest + React Native Testing Library | Industry standard |

### 4.2 Data Persistence

**Recommendation: SQLite with Repository Pattern**

Schema approach:
```sql
-- Programs table
CREATE TABLE programs (
  id TEXT PRIMARY KEY,
  user_id TEXT,
  variant TEXT,
  settings JSON,
  status TEXT,
  current_week INTEGER,
  created_at TEXT,
  updated_at TEXT
);

-- Training maxes table
CREATE TABLE training_maxes (
  id TEXT PRIMARY KEY,
  program_id TEXT,
  exercise_id TEXT,
  current_value REAL,
  unit TEXT,
  updated_at TEXT,
  FOREIGN KEY (program_id) REFERENCES programs(id)
);

-- Training max history
CREATE TABLE tm_history (
  id TEXT PRIMARY KEY,
  tm_id TEXT,
  value REAL,
  source TEXT,
  source_id TEXT,
  recorded_at TEXT,
  FOREIGN KEY (tm_id) REFERENCES training_maxes(id)
);

-- Workout sessions
CREATE TABLE sessions (
  id TEXT PRIMARY KEY,
  program_id TEXT,
  week_number INTEGER,
  day_number INTEGER,
  status TEXT,
  started_at TEXT,
  completed_at TEXT,
  FOREIGN KEY (program_id) REFERENCES programs(id)
);

-- Exercise performances within sessions
CREATE TABLE exercise_performances (
  id TEXT PRIMARY KEY,
  session_id TEXT,
  exercise_id TEXT,
  sets JSON,
  tm_adjustment REAL,
  FOREIGN KEY (session_id) REFERENCES sessions(id)
);
```

### 4.3 Offline Strategy

**Recommendation: Local-First with Optional Cloud Sync**

```
v1.0: Pure local (no cloud)
- All data in SQLite
- Export/import for backup
- No account required

v1.5+: Optional cloud sync
- User can enable cloud backup
- Sync on demand or automatic
- Conflict resolution UI
```

---

## 5. Domain Logic Placement

### 5.1 Where Business Logic Lives

| Logic | Location | Reason |
|-------|----------|--------|
| TM adjustment calculation | Domain Service | Pure business rule |
| Rep target calculation | Domain Service | Derived from week/block |
| Working weight calculation | Value Object | Simple derivation |
| Workout completion flow | Application Use Case | Orchestration |
| Week advancement | Application Use Case | Orchestration |
| AMRAP validation | Domain Entity | Entity invariant |
| Program creation | Application Use Case | Orchestration |

### 5.2 Domain Services

```typescript
// Progression Calculator - Core domain service
interface ProgressionCalculator {
  calculateAdjustment(
    variant: ProgressionVariant,
    targetReps: number,
    actualReps: number
  ): TrainingMaxAdjustment;

  calculateNextWeekParameters(
    currentWeek: number,
    blockNumber: number,
    trainingMax: Weight
  ): WeekParameters;
}

// Week Generator - Derives weekly schedule
interface WeekGenerator {
  generateWeekSchedule(
    programConfig: ProgramConfiguration,
    weekNumber: number
  ): WeeklySchedule;
}
```

---

## 6. Testing Strategy

### 6.1 Test Pyramid

```
                    /\
                   /  \  E2E (5%)
                  /    \  - Happy path flows
                 /______\  - Critical user journeys
                /        \
               /   INT    \  Integration (20%)
              /   TESTS    \  - Repository tests
             /______________\  - Use case + DB
            /                \
           /    UNIT TESTS    \  Unit (75%)
          /                    \  - Domain logic
         /______________________\  - Value objects
                                   - Progression calc
```

### 6.2 Critical Test Cases

1. **Progression Algorithm Tests**
   - All AMRAP scenarios (+5 to -5 reps)
   - Boundary conditions (exact target)
   - TM clamping (min/max)

2. **Workout Flow Tests**
   - Complete workout end-to-end
   - Partial workout abandonment
   - Skip workout

3. **Week Transition Tests**
   - Normal week advance
   - Deload week handling
   - Block transition

4. **Data Integrity Tests**
   - TM update atomicity
   - Session recovery after crash
   - Data migration

---

## 7. Implementation Phases

### Phase 1: Domain Core (Week 1-2)

**Deliverables:**
- Domain entities and value objects
- Progression calculator with all variants
- Unit tests for all domain logic
- Repository interfaces

**Success Criteria:**
- All business rules implemented
- 100% test coverage on domain logic
- Clean, documented code

### Phase 2: Infrastructure (Week 2-3)

**Deliverables:**
- SQLite repository implementations
- Data migration framework
- Export/import functionality

**Success Criteria:**
- CRUD operations working
- Data survives app restart
- Export produces valid CSV/JSON

### Phase 3: Application Layer (Week 3-4)

**Deliverables:**
- All use cases implemented
- Command/query handlers
- Integration tests

**Success Criteria:**
- Complete user flows functional
- Use cases tested end-to-end
- No UI yet, all headless

### Phase 4: Presentation (Week 4-6)

**Deliverables:**
- React Native app shell
- Workout recording screens
- Program setup flow
- Progress visualization

**Success Criteria:**
- App runs on iOS/Android
- Core flows functional
- Basic styling (polish later)

### Phase 5: Polish & Release (Week 6-8)

**Deliverables:**
- UI refinement
- Error handling UX
- Performance optimization
- App store submission

**Success Criteria:**
- App store approved
- No critical bugs
- Performance targets met

---

## 8. Identified Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Incorrect progression formula | Low | High | Verify with SBS docs, test thoroughly |
| Data loss during workout | Medium | High | Auto-save every action |
| Performance issues with history | Low | Medium | Lazy loading, pagination |
| User confusion with variants | Medium | Medium | Clear onboarding, tooltips |
| Offline sync conflicts | Low | Medium | v1 is local-only, defer |

---

## 9. Open Questions for Stakeholder

1. **Exact rep targets per week**: Do you have the specific rep tables for each week of each block?

2. **Intensity percentages**: What exact percentages should be used for each week?

3. **Accessories handling**: Should accessories have TM tracking or simple logging?

4. **Deload week volume**: What specific reduction (50%? 60%?) for deload weeks?

5. **Unit preferences**: Should the app support both kg and lbs with conversion?

6. **Notification preferences**: What workout reminders are desired?

7. **Analytics priority**: Which progress metrics are most important?

---

## 10. Final Recommendation

### Build This Application

The A2S Workout Tracking Application addresses a real problem: tracking strength training with autoregulated progression is tedious with spreadsheets. The domain is well-understood, the business rules are clear, and the technical approach is straightforward.

### Recommended First Release (MVP)

Focus on:
1. **RTF variant only** - Most popular, best understood
2. **4-day program template** - Most common usage
3. **Local storage only** - Simplest, gym-friendly
4. **Core workout flow** - Record sets, AMRAP, see TM updates
5. **Basic progress view** - TM over time chart

Defer for v1.5+:
- RIR and Linear variants
- Cloud sync
- Custom program builder
- Web dashboard
- Social features

### Success Metrics

| Metric | Target |
|--------|--------|
| Workout recording time | < 30 seconds per exercise |
| App launch to workout | < 5 seconds |
| Crash rate | < 0.1% |
| Data loss incidents | 0 |
| User retention (7 day) | > 50% |

---

## Appendix: Validated AMRAP Progression Examples

### Example 1: Exceeding Target (+3)
```
Inputs:
- Current TM: 100 kg
- Target reps: 6
- Actual AMRAP reps: 9

Calculation:
- Reps over target: 9 - 6 = +3
- Lookup table: +3 = +1.5%
- New TM: 100 * 1.015 = 101.5 kg
- Rounded: 102 kg (to nearest 2.5 kg)

Output: TM increases from 100 kg to 102 kg
```

### Example 2: Missing Target (-1)
```
Inputs:
- Current TM: 100 kg
- Target reps: 6
- Actual AMRAP reps: 5

Calculation:
- Reps over target: 5 - 6 = -1
- Lookup table: -1 = -2%
- New TM: 100 * 0.98 = 98 kg

Output: TM decreases from 100 kg to 98 kg
```

### Example 3: Hitting Target Exactly
```
Inputs:
- Current TM: 100 kg
- Target reps: 6
- Actual AMRAP reps: 6

Calculation:
- Reps over target: 6 - 6 = 0
- Lookup table: 0 = 0%
- New TM: 100 * 1.0 = 100 kg

Output: TM unchanged at 100 kg
```

### Example 4: Large Excess (+7)
```
Inputs:
- Current TM: 100 kg
- Target reps: 6
- Actual AMRAP reps: 13

Calculation:
- Reps over target: 13 - 6 = +7
- Capped to +5 for calculation
- Lookup table: +5 = +3%
- New TM: 100 * 1.03 = 103 kg

Output: TM increases from 100 kg to 103 kg
Note: Maximum single-session increase is 3%
```

### Example 5: Severe Miss (-3)
```
Inputs:
- Current TM: 100 kg
- Target reps: 6
- Actual AMRAP reps: 3

Calculation:
- Reps over target: 3 - 6 = -3
- Lookup table: -2 or worse = -5%
- New TM: 100 * 0.95 = 95 kg

Output: TM decreases from 100 kg to 95 kg
```

These examples validate the progression algorithm is correctly understood and can be implemented accurately.
