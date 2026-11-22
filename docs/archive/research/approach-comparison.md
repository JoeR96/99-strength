# Approach Comparison: A2S Workout Tracking Application

## Executive Summary

This document analyzes different architectural approaches for the A2S Workout Tracking Application. It evaluates aggregate boundaries, entity relationships, and structural decisions with trade-off analysis.

---

## 1. Aggregate Boundary Analysis

### Critical Question: Where Should the Aggregate Boundaries Be?

The choice of aggregate boundaries significantly impacts:
- Consistency guarantees
- Transactional scope
- Performance characteristics
- Query complexity
- Concurrent access handling

---

## 2. Aggregate Design Options

### Option A: Monolithic Program Aggregate

**Structure:**
```
WorkoutProgram (Aggregate Root)
├── All Weeks (1-21)
│   └── All Daily Plans
│       └── All Exercise Plans
├── All Exercise Configurations
├── All Workout Sessions
│   └── All Exercise Performances
│       └── All Sets
└── All Training Max History
```

**Pros:**
- Perfect consistency across entire program
- Simple transactional semantics
- Easy to reason about state

**Cons:**
- Large aggregate size (potentially 100+ entities)
- Locking entire program for single set recording
- Poor performance on mobile/low-memory devices
- Difficult to sync incrementally

**Verdict:** Too large. Violates aggregate sizing guidelines.

---

### Option B: Week as Primary Aggregate

**Structure:**
```
TrainingWeek (Aggregate Root)
├── Week Number
├── Block Number
├── Status
├── Daily Workout Plans[]
│   └── Exercise Plans[]
└── Reference to Program ID

WorkoutSession (Separate Aggregate)
├── Session ID
├── Week Reference
├── Completed Exercises[]
└── TM Adjustments (pending)

TrainingMaxRegistry (Separate Aggregate)
├── Exercise → Current TM mapping
└── TM History per exercise
```

**Pros:**
- Natural boundary around a meaningful time period
- Week is the progression unit
- Reasonable size (5-10 workout days, 20-50 exercise plans)
- Supports week-by-week sync

**Cons:**
- TM updates span aggregates (week completes, TM updates)
- Need eventual consistency for cross-week queries
- Block transitions require coordination

**Verdict:** Good balance but TM management needs careful design.

---

### Option C: Daily Session as Primary Aggregate (Recommended)

**Structure:**
```
ProgramConfiguration (Aggregate Root)
├── Program ID
├── User ID
├── Settings
├── Exercise Registry
│   └── ExerciseId → TrainingMax
└── Status

WeeklySchedule (Aggregate Root)
├── Week Number
├── Block Number
├── Scheduled Days[]
│   └── PlannedExercises[] (value objects)
└── Status

WorkoutSession (Aggregate Root)
├── Session ID
├── Program ID (reference)
├── Week/Day identifiers
├── Exercise Performances[]
│   └── Sets[]
├── Calculated TM Adjustments
└── Timestamps

TrainingMaxLedger (Aggregate Root)
├── Exercise ID
├── Current Value
├── History[]
│   └── Value, Date, Source (workout/manual)
```

**Pros:**
- Small, focused aggregates
- Single workout can be recorded offline
- TM updates are atomic within ledger
- Clear separation of concerns
- Optimal for mobile-first design

**Cons:**
- More aggregates to manage
- Need to coordinate TM updates after workout
- Requires event-driven architecture for consistency

**Verdict:** Best fit for mobile workout tracking with offline support.

---

### Option D: Event-Sourced Sessions

**Structure:**
```
ProgramStream (Event Stream)
├── ProgramCreated
├── ProgramStarted
├── ExerciseAdded
├── TrainingMaxSet
└── ...

SessionStream (Event Stream per Session)
├── WorkoutStarted
├── SetCompleted
├── ExerciseFinished
├── AMRAPRecorded
└── WorkoutCompleted

Projections (Read Models)
├── CurrentProgramState
├── WeeklyScheduleView
├── TrainingMaxProjection
└── ProgressAnalytics
```

**Pros:**
- Perfect audit trail
- Easy to replay/reconstruct state
- Natural fit for undo/redo
- Excellent for analytics
- Supports offline with event merge

**Cons:**
- Higher complexity
- More infrastructure
- Learning curve
- Event versioning challenges

**Verdict:** Powerful but potentially over-engineered for v1.

---

## 3. WeeklyExercisePlan: Embedded vs. Separate

### The Question

Should `WeeklyExercisePlan` (the planned sets/reps/weight for an exercise in a specific week) be:
1. Embedded within the Week aggregate
2. A separate aggregate referenced by Week

### Analysis

#### Embedding (Recommended for this domain)

```
WeeklySchedule {
    weekNumber: 5
    exercises: [
        { exerciseId: "squat", sets: 4, reps: 6, intensity: 0.80, weight: 80kg },
        { exerciseId: "bench", sets: 4, reps: 6, intensity: 0.80, weight: 64kg },
        ...
    ]
}
```

**Rationale:**
- Exercise plans are immutable once week starts
- Never modified independently of week
- Always loaded/displayed together
- No concurrent access to individual plans
- Simple value object semantics

#### Separation (When it would make sense)

Separate aggregates would be appropriate if:
- Exercise plans could be modified independently
- Different users could access same plan
- Plans needed independent versioning
- Complex shared logic between plans

**These conditions do NOT apply to A2S.**

### Verdict: Embed as Value Objects

The `WeeklyExercisePlan` should be a **Value Object** embedded within the `WeeklySchedule` aggregate. It represents derived, read-only data calculated from:
- Current Training Max
- Week number (determines rep scheme)
- Block number (determines intensity)

---

## 4. Training Max Management Approaches

### Approach A: Embedded in Program

```
ProgramConfiguration {
    exercises: {
        "squat": { currentTM: 100, history: [...] },
        "bench": { currentTM: 80, history: [...] }
    }
}
```

**Issue:** Updating TM requires locking entire program configuration.

### Approach B: Separate TM Per Exercise (Recommended)

```
TrainingMaxRecord (Aggregate) {
    exerciseId: "squat"
    programId: "prog-123"
    currentValue: 100kg
    history: [
        { value: 100, date: "2024-01-15", source: "workout-456" },
        { value: 98.5, date: "2024-01-08", source: "workout-455" }
    ]
}
```

**Pros:**
- Fine-grained updates
- Independent exercise progression
- Clear audit trail
- Easy rollback per exercise

**Cons:**
- More records to manage
- Need to aggregate for views

### Approach C: Event-Sourced TM

```
Events:
- TrainingMaxSet { exercise: "squat", value: 90, reason: "initial" }
- TrainingMaxAdjusted { exercise: "squat", delta: +1.5, from: "workout-123" }
- TrainingMaxAdjusted { exercise: "squat", delta: -2.0, from: "workout-124" }
```

**Pros:**
- Perfect history
- Easy analytics
- Natural fit for progression tracking

**Cons:**
- Requires event infrastructure

### Verdict: Approach B for v1, consider C for v2

---

## 5. Workout Session Recording Models

### Model A: All-at-Once Recording

User records entire workout, then submits.

```
Session {
    exercises: [
        { id: "squat", sets: [set1, set2, set3, amrapSet] },
        { id: "rdl", sets: [...] }
    ]
}
// Submit all at once
```

**Pros:**
- Atomic submission
- Simple transaction
- No partial states

**Cons:**
- Lose data on app crash
- Can't track rest times in real-time
- No mid-workout persistence

### Model B: Progressive Recording (Recommended)

Each set recorded individually, session builds up.

```
recordSet(sessionId, exerciseId, setData) {
    // Append to session immediately
    // Local persistence
    // Background sync
}

completeWorkout(sessionId) {
    // Mark complete
    // Calculate TM adjustments
    // Apply changes
}
```

**Pros:**
- No data loss on crash
- Real-time persistence
- Supports rest timer integration
- Better UX

**Cons:**
- More complex state management
- Need incomplete session handling
- Sync complexity

### Verdict: Progressive Recording

Better user experience and data safety outweigh complexity costs.

---

## 6. Offline-First Architecture

### Requirements
- Record workouts without internet
- Sync when connection available
- Handle conflicts gracefully

### Strategy A: Local-First with Sync

```
┌──────────────────┐      ┌──────────────────┐
│   Local SQLite   │ ←──→ │   Remote API     │
│   (Source of     │      │   (Backup &      │
│    Truth)        │      │    Sync)         │
└──────────────────┘      └──────────────────┘
```

**Characteristics:**
- Local DB is authoritative
- Remote is backup/sync target
- Sync on connectivity
- Conflict resolution on sync

### Strategy B: Remote-First with Local Cache

```
┌──────────────────┐      ┌──────────────────┐
│   Local Cache    │ ←──→ │   Remote API     │
│   (Temporary)    │      │   (Source of     │
│                  │      │    Truth)        │
└──────────────────┘      └──────────────────┘
```

**Issues for workout app:**
- Requires connectivity to work
- Poor gym experience (no signal)
- Not suitable

### Strategy C: CRDT-Based Sync

```
┌──────────────────┐      ┌──────────────────┐
│   Local CRDT     │ ←──→ │   Remote CRDT    │
│   Replicas       │      │   Merge Point    │
└──────────────────┘      └──────────────────┘
```

**Characteristics:**
- Automatic conflict resolution
- Eventually consistent
- Works offline indefinitely
- Complex to implement

### Verdict: Strategy A (Local-First)

Most practical for v1. CRDT can be considered for multi-device sync in v2.

---

## 7. Architecture Style Comparison

### Clean Architecture (Recommended)

```
┌─────────────────────────────────────────────────┐
│                  Presentation                    │
│            (React/React Native UI)               │
├─────────────────────────────────────────────────┤
│                 Application                      │
│    (Use Cases, Commands, Queries, DTOs)         │
├─────────────────────────────────────────────────┤
│                   Domain                         │
│  (Entities, Value Objects, Domain Services)     │
├─────────────────────────────────────────────────┤
│               Infrastructure                     │
│   (Repositories, External Services, DB)         │
└─────────────────────────────────────────────────┘
```

**Pros:**
- Clear dependency direction
- Testable domain logic
- Framework-agnostic core
- Easy to maintain

**Cons:**
- More initial structure
- Learning curve

### Hexagonal Architecture

```
         ┌─────────────────────┐
         │       Domain        │
         │    (Core Logic)     │
         └──────────┬──────────┘
                    │
    ┌───────────────┼───────────────┐
    │               │               │
┌───┴───┐      ┌────┴────┐     ┌────┴────┐
│ REST  │      │ SQLite  │     │ Timer   │
│Adapter│      │ Adapter │     │ Adapter │
└───────┘      └─────────┘     └─────────┘
```

**Pros:**
- Port/adapter clarity
- Easy to swap implementations
- Great for testing

**Cons:**
- More abstractions
- Port definition overhead

### CQRS (Command Query Responsibility Segregation)

```
┌─────────────────┐     ┌─────────────────┐
│    Commands     │     │     Queries     │
│  (Write Model)  │     │  (Read Model)   │
└────────┬────────┘     └────────┬────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│   Aggregates    │ ──→ │   Projections   │
│   (Domain)      │     │   (Views)       │
└─────────────────┘     └─────────────────┘
```

**Pros:**
- Optimized read models
- Scalable queries
- Clear command handling

**Cons:**
- Eventual consistency
- More complexity

### Verdict: Clean Architecture with CQRS Elements

Use Clean Architecture as the foundation, with CQRS patterns for workout recording (commands) and analytics (queries).

---

## 8. State Management Approaches

### Approach A: Single Global Store

```javascript
store = {
    program: { ... },
    currentWeek: { ... },
    currentSession: { ... },
    trainingMaxes: { ... },
    ui: { ... }
}
```

**Suitable for:** React/Redux, simple apps

### Approach B: Domain-Aligned Stores

```javascript
programStore = { ... }
sessionStore = { ... }
tmStore = { ... }
analyticsStore = { ... }
```

**Suitable for:** MobX, Zustand, complex apps

### Approach C: Aggregate-Based Repositories

```javascript
programRepository.getById(id)
sessionRepository.save(session)
tmRepository.getForExercise(exerciseId)
```

**Suitable for:** DDD approach, testable, clean

### Verdict: Approach C with Approach B for UI

Domain uses repositories, UI uses lightweight stores that project from domain.

---

## 9. Technology Stack Considerations

### Option A: React Native + SQLite

**Stack:**
- React Native (cross-platform)
- SQLite (local storage)
- TypeScript (type safety)
- Expo or bare React Native

**Pros:**
- Single codebase for iOS/Android
- Rich ecosystem
- Good offline support
- Many developers available

**Cons:**
- Performance overhead vs native
- Some native features harder

### Option B: Native (Swift/Kotlin) + SQLite

**Stack:**
- Swift for iOS
- Kotlin for Android
- SQLite or Core Data/Room

**Pros:**
- Best performance
- Full platform features
- Best UX possible

**Cons:**
- Two codebases
- Higher development cost
- Different skill sets needed

### Option C: Flutter + SQLite

**Stack:**
- Flutter (Dart)
- sqflite (SQLite for Flutter)

**Pros:**
- Single codebase
- Good performance
- Rich widget library

**Cons:**
- Smaller ecosystem than React Native
- Fewer third-party libraries

### Option D: Progressive Web App

**Stack:**
- React/Vue/Svelte
- IndexedDB or SQLite (via wasm)
- Service Workers

**Pros:**
- Works on all platforms
- No app store
- Easy updates

**Cons:**
- Less native feel
- Push notification limitations
- Storage limits on iOS

### Verdict: React Native for v1

Best balance of development speed, cross-platform support, and ecosystem for a fitness tracking app.

---

## 10. Summary Matrix

| Aspect | Recommended Approach | Rationale |
|--------|---------------------|-----------|
| Primary Aggregate | WorkoutSession | Small, focused, offline-friendly |
| TM Management | Separate per-exercise | Independent progression, audit trail |
| WeeklyExercisePlan | Embedded value object | Immutable, always loaded with week |
| Recording Model | Progressive | Data safety, better UX |
| Offline Strategy | Local-first | Gym-friendly, reliable |
| Architecture | Clean + CQRS elements | Maintainable, testable |
| State Management | Repository pattern | DDD alignment |
| Tech Stack | React Native + SQLite | Cross-platform, good ecosystem |

---

## 11. Risks of Recommended Approach

1. **Aggregate Coordination Complexity**
   - Risk: TM updates after workout need coordination
   - Mitigation: Use domain events, clear boundaries

2. **Offline Sync Conflicts**
   - Risk: Multiple device edits cause conflicts
   - Mitigation: Last-write-wins for v1, CRDT for v2

3. **Local Storage Limits**
   - Risk: 21 weeks of data fills device
   - Mitigation: Efficient schema, archive old programs

4. **Testing Complexity**
   - Risk: Many aggregates harder to test end-to-end
   - Mitigation: Strong unit tests, focused integration tests

5. **Over-Engineering**
   - Risk: Too much structure for simple app
   - Mitigation: Start minimal, add only when needed
