# Cardio as Exercises Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stair "finisher" track and the fixed 10-minute incline cool-down with two real cardio exercises on each Plan v3 workout day, each driven by a new `CardioStageStrategy` progression.

**Architecture:** A fourth `ExerciseProgression` subtype, `CardioStageStrategy`, is one generic engine configured by a list of stages (continuous or intervals) plus a lane rule set (Hard or Easy). Its prescription is a `PlannedCardioBlock` (not weight × reps sets) and its performance is a `CardioResult` carried on `ExercisePerformance`. The two days of a lane (stairs on Upper A/B, walk on Lower A/B) share one progression: the workout copies a lane's state to its sibling after every change. Gym mode, history, Hevy and Plan v3 then treat cardio as an ordinary exercise; a startup backfill moves the running plan over and seeds the stairs from the old track.

**Tech Stack:** .NET 10 / C# 13, EF Core 10 (PostgreSQL jsonb), MediatR, xUnit + FluentAssertions + NSubstitute; React 19 + TypeScript + Vitest.

**Spec:** "Cardio as exercises — design" (Claude doc, https://claude.ai/artifact/Qj6kT7wpAhDCcD5SgAXqih, as of 23 Nov 2026). Sections 2–6 are the requirements; section 6 records the decisions.

## Global Constraints

- Lifting strategies (Linear/A2S2, RepsPerSet, MinimalSets) and the cardio-track strategies (`CardioStrategies.cs`) must not change behaviour. Their existing tests must pass unchanged.
- Domain has no external dependencies; handlers stay thin (load → domain method → save); `Result<T>` for business errors.
- Every Workout-mutating command carries `[ChangesSyncArea(SyncArea.Workout)]`; any new endpoint the offline queue calls carries `[Idempotent]`.
- Effort buttons map Easy / Solid / Brutal → `EffortRating.Easy/Solid/Brutal` (RPE 4 / 7 / 9).
- "Pain or stopped early" = `Pain != PainFlag.None || !Finished`.
- Stair climber (hard lane): warm-up **3 min at level 3**, cool-down **2 min at level 3**; Stage 1 continuous **6 → 12 min at level 6, +1 min**; Stage 2 intervals **4 × 2/2 → 4 × 3/3 → 4 × 4/3** (hard min / easy min), easy minutes at **level 3**, hard level asked on entry with suggestion **continuous level + 2**; Stage 3 **4 × 4/3, hard level +1** per step (cap level 20). Recoveries sit only between rounds, so sessions are 11 → 17, 19, 26, 30 min.
- Incline walk (easy lane): no warm-up or cool-down; Stage 1 **10 → 30 min, +2 min** at the starting incline (asked, **5 %** suggested); Stage 2 **30 min, incline +1 % to 12 %**; Stage 3 **30 min, speed +0.3 km/h and incline back to 8 %**, then incline +1 % to 12 % again, repeating. Speed is asked on the first session (no cap).
- Hard rule: Easy → step up now; Solid → bank one, 2 banked → step up; Brutal → hold, banked Solids kept; pain/stopped early → hold, 2 in a row → step back one.
- Easy rule: Easy → step up; Solid → hold; Brutal → step back one; pain/stopped early → hold, 2 in a row → step back one.
- Stage changes are automatic (no "accept next stage" prompt). "Step back one" returns to the exact previous prescription, across a stage boundary if needed.
- Starting values are blank until gym mode asks for them; they can be edited later and every change is an audit entry ("progression history").
- Seed: the 23 Nov stair session (6 min at level 6, Easy) puts the stair climber at **7 min at level 6**.
- Library names: the exercises are named `Stair Machine (Floors)` (Hevy `4377A52B`) and `Treadmill` (Hevy `243710DE`), equipment `Machine`, category `Cardio`, last on their day.

## Design decisions made in planning (not in the spec)

1. **A lane progresses once across both its days.** The spec's timings (9 step-ups ≈ 5 weeks at Easy) only hold with two sessions a week feeding one progression. Each day keeps its own `Exercise`, and `Workout` copies a lane's state to the other exercise(s) with the same `CardioLane` after completion, settings edits and undo.
2. **Red readiness** skips the hard-lane cardio by default in gym mode, as it skips finishers today (`KeepFinisher` keeps it). The easy lane always shows.
3. **Hevy pull** completes a cardio exercise only when the Hevy set has an RPE (→ effort); otherwise it leaves the exercise out, so its progression is unchanged.
4. **The old stair track** is paused by the backfill (history kept) so nothing logs to it twice.

## Review Focus

1. Undo after a cardio day must restore both days of the lane, not just the one completed (Task 3 test `Undo_RestoresLaneSiblingToo`).
2. Finishing gym mode with the cardio left unticked must leave its progression untouched, exactly like a skipped lift (Task 6 test `CompleteDay_WithoutCardioPerformance_KeepsCardioProgression`).
3. A gym draft saved before this change (version 1) must still load after the upgrade, without losing ticked sets (Task 9 test `migrateGymSession upgrades a v1 draft`).
4. Completing a day while a cardio setting is still blank must be refused with a clear message, not a 500 (Task 3 test `Complete_WhileSettingsPending_Throws` and Task 6 test `CompleteDay_PendingCardioSettings_ReturnsValidationError`).
5. Running the startup backfill twice must not add the cardio exercises twice or re-seed a lane that has progressed (Task 8 test `Backfill_IsIdempotent`).

---

## File Structure

**Domain (`src/A2S.Domain`)**
- Create `Aggregates/Workout/Cardio/CardioStageDefinition.cs` — `CardioLane`, `CardioStageKind`, `CardioStepAxis`, `IntervalFormat`, `CardioSegment`, `CardioStageDefinition`.
- Create `Aggregates/Workout/Cardio/CardioStageState.cs` — `CardioStageState`, `CardioPosition` (the memento; also the snapshot payload).
- Create `Aggregates/Workout/Cardio/CardioLadder.cs` — pure step-up / step-back / enter-stage over definitions + state.
- Create `Aggregates/Workout/Cardio/CardioLaneRules.cs` — the hard and easy rule tables.
- Create `Aggregates/Workout/Cardio/CardioLanes.cs` — the two presets (`StairClimber()`, `InclineWalk()`).
- Create `Aggregates/Workout/CardioStageStrategy.cs` — the `ExerciseProgression` subtype.
- Create `ValueObjects/PlannedCardioBlock.cs`, `ValueObjects/CardioResult.cs`.
- Modify `Aggregates/Workout/ExerciseProgression.cs`, `ProgressionConfig.cs`, `Exercise.cs`, `Workout.cs`, `ProgressionAuditEntry.cs`, `ValueObjects/ExercisePerformance.cs`, `ValueObjects/ProgressionSnapshot.cs`, `Enums/ExerciseCategory.cs`, `Enums/AuditEntryType.cs`.

**Infrastructure** — `Persistence/Configurations/CardioStageStrategyConfiguration.cs` (new), `ExerciseProgressionConfiguration.cs`, `WorkoutConfiguration.cs`, migration `AddCardioStageProgression`, `SeedData/CardioExerciseBackfill.cs` (new), `src/A2S.Api/Program.cs`.

**Application** — `Queries/GetWeekPlan/*`, `DTOs/WorkoutDto.cs`, `Queries/GetWorkout/GetCurrentWorkoutQueryHandler.cs`, `Queries/Plans/GetUpcomingDaysQuery.cs`, `Services/DayBudgetCalculator.cs`, `Services/ConfiguredExerciseFactory.cs`, `Commands/AddExercise/AddExerciseCommandValidator.cs`, `Commands/CreateWorkout/CreateWorkoutCommand.cs`, `Commands/CompleteDay/*`, `Commands/GymSessions/FinishGymSession*`, `Commands/CardioSettings/SetCardioSettingsCommand.cs` (new), history query handlers, `Hevy/HevyWorkoutPoster.cs`, `Hevy/RoutineBuilder.cs`, `Hevy/HevySessionCompleter.cs`, `Hevy/HevyCardioTrackLogger.cs`, `SuggestedPlans/PlanV3BaseBuildTransition.cs`, `SuggestedPlans/PlanTemplatePreview.cs`, `Services/PlanTemplateApplier.cs`.

**API** — `Controllers/ExercisesController.cs` (cardio settings route).

**Web (`src/A2S.Web/src`)** — `types/workout.ts`, `types/workoutDtos.ts`, `types/gym.ts`, `api/workouts.ts`, `features/gym/*` (session state, checklist, new `GymCardioCard.tsx`, new `CardioSettingsDialog.tsx`, wizard input), `features/gym/finisher/finisherPlan.ts`, `features/workout/*`, `features/history/*`, `features/current/*`.

---

### Task 1: Cardio ladder and prescription (domain, pure)

**Files:**
- Create: `src/A2S.Domain/Aggregates/Workout/Cardio/CardioStageDefinition.cs`, `CardioStageState.cs`, `CardioLadder.cs`, `CardioLanes.cs`
- Create: `src/A2S.Domain/ValueObjects/PlannedCardioBlock.cs`
- Test: `tests/A2S.Domain.Tests/Strategies/Cardio/CardioLadderTests.cs`, `PlannedCardioBlockTests.cs`

**Interfaces:**
- Produces:
  - `enum CardioLane { Hard = 1, Easy = 2 }`, `enum CardioStageKind { Continuous = 1, Intervals = 2 }`, `enum CardioStepAxis { Minutes = 1, IntervalFormat = 2, Intensity = 3 }`
  - `record IntervalFormat(int Rounds, int HardMinutes, int EasyMinutes)`
  - `record CardioSegment(int Minutes, decimal Level)`
  - `record CardioStageDefinition(string Name, CardioStageKind Kind, CardioStepAxis Axis, IntensityUnit IntensityUnit, int? MinMinutes, int? MaxMinutes, int? MinuteStep, int? FixedMinutes, IReadOnlyList<IntervalFormat>? Formats, decimal? IntensityOnEntry, bool AsksForIntensity, decimal? AskSuggestValue, decimal? AskSuggestOffset, decimal IntensityStep, decimal? IntensityCap, decimal? SpeedStepOnEntry, bool RepeatsAtTop, decimal? RecoveryLevel)` (reuse `IntensityUnit` from `Aggregates/Cardio/CardioTrackEnums.cs`)
  - `record CardioPosition(int StageIndex, int? Minutes, int? FormatIndex, decimal? Intensity, decimal? Speed)`
  - `record CardioStageState(int StageIndex, int? Minutes, int? FormatIndex, decimal? Intensity, decimal? SuggestedIntensity, decimal? Speed, int SolidsBanked, int MissStreak, IReadOnlyList<CardioPosition> History, IReadOnlyDictionary<int, decimal> ChosenIntensities)` with `static CardioStageState Start(IReadOnlyList<CardioStageDefinition> stages)`
  - `static class CardioLadder { CardioStageState StepUp(IReadOnlyList<CardioStageDefinition>, CardioStageState); CardioStageState StepBack(CardioStageState); CardioStageState Enter(IReadOnlyList<CardioStageDefinition>, CardioStageState, int stageIndex); CardioStageState Choose(CardioStageState, decimal? intensity, decimal? speed); }` — `Choose` sets the current Intensity (and `ChosenIntensities[StageIndex]`) and/or Speed, clearing `SuggestedIntensity`; null leaves a value as it is.
  - `static class CardioLanes { CardioLanePreset StairClimber(); CardioLanePreset InclineWalk(); }` with `record CardioLanePreset(CardioLane Lane, IReadOnlyList<CardioStageDefinition> Stages, CardioSegment? WarmUp, CardioSegment? CoolDown, bool RequiresSpeed)`
  - `PlannedCardioBlock` (ValueObject): `CardioLane Lane, string StageName, int StageNumber, int StageCount, CardioSegment? WarmUp, CardioSegment? CoolDown, CardioStageKind Kind, int WorkMinutes, IntervalFormat? Intervals, decimal? RecoveryLevel, IntensityUnit IntensityUnit, decimal? Intensity, decimal? SuggestedIntensity, decimal? Speed, bool IsSettingsPending, int TotalMinutes, string Text`; `static PlannedCardioBlock From(CardioLane lane, IReadOnlyList<CardioStageDefinition> stages, CardioSegment? warmUp, CardioSegment? coolDown, bool requiresSpeed, CardioStageState state)`; `IsSettingsPending` = Intensity null, or `requiresSpeed` and Speed null

Ladder semantics (the part a signature does not determine):
- `StepUp` pushes the current position onto `History` (keep the last 50), then on the current stage's axis: Minutes → `+MinuteStep` while ≤ `MaxMinutes`; IntervalFormat → next format; Intensity → `+IntensityStep` while ≤ `IntensityCap`. Past the end: `RepeatsAtTop` → `Enter(same stage)`, else `Enter(next)`; no next stage → return the state unchanged (and do not push).
- `Enter(i)`: Minutes = `MinMinutes ?? FixedMinutes`; FormatIndex = 0 when `Formats` is set; Intensity = `ChosenIntensities[i]` if present, else (`AsksForIntensity` → null with `SuggestedIntensity = AskSuggestValue ?? current Intensity + AskSuggestOffset`), else `IntensityOnEntry ?? current Intensity`; Speed += `SpeedStepOnEntry ?? 0`.
- `StepBack` pops `History` into the position; empty history → unchanged. Both `StepUp`/`StepBack` reset `SolidsBanked` and `MissStreak` to 0.
- `TotalMinutes` = warm-up + work + cool-down, where interval work = `Rounds × Hard + (Rounds − 1) × Easy`.
- `Text` examples (exact): `"3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)"`, `"3 min easy (L3) → 4 × 2 min @ L8 / 2 min @ L3 → 2 min easy (L3)"`, `"30 min @ 8 % incline, 5.9 km/h"`, `"12 min @ 5 % incline"` when speed is unknown.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void StairClimber_StartsAtSixMinutesLevelSix()
// Start(...) → StageIndex 0, Minutes 6, Intensity 6; block.TotalMinutes 11; block.Text == "3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)"

[Fact] public void StairClimber_NineStepUps_ReachFullFourByFour()
// apply StepUp 9 times; after step 6: Minutes 12 / TotalMinutes 17;
// step 7: StageIndex 1, Intensity null, SuggestedIntensity 8, IsSettingsPending true, Intervals (4,2,2), TotalMinutes 19
// Choose(state, 8, null); step 8: (4,3,3) TotalMinutes 26; step 9: StageIndex 2, (4,4,3), Intensity 8, TotalMinutes 30

[Fact] public void StairClimber_StageThree_StepUpRaisesHardLevel_CappedAtTwenty()
[Fact] public void StepBack_FromFirstIntervalFormat_ReturnsToTwelveMinutesContinuous()
[Fact] public void StepBack_WithNoHistory_IsUnchanged()
[Fact] public void ReenteringStageTwo_ReusesChosenHardLevel()  // StepUp→ choose 8 → StepBack → StepUp: Intensity 8, not pending

[Fact] public void InclineWalk_StartsPending_SuggestsFivePercent()   // Minutes 10, Intensity null, Suggested 5, Speed null
[Fact] public void InclineWalk_TenStepUps_ReachThirtyMinutes()       // with incline 5: step 10 → Minutes 30, StageIndex 0
[Fact] public void InclineWalk_SteeperThenFaster_ResetsInclineToEightAndAddsSpeed()
// incline 5, speed 5.6: step 11 → StageIndex 1, 30 min, 6 %; ... at 12 % next step → StageIndex 2, 8 %, 5.9;
// 4 more steps → 12 %; next → 8 %, 6.2 (RepeatsAtTop)
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/A2S.Domain.Tests --filter "FullyQualifiedName~Strategies.Cardio"`
Expected: build FAIL (types not defined).

- [ ] **Step 3: Implement the files above** (records + `CardioLadder` + `CardioLanes` presets with the Global Constraints values + `PlannedCardioBlock.From`).

- [ ] **Step 4: Run tests to verify they pass** — same command, expected PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(cardio): cardio stage ladder and planned cardio block"`

---

### Task 2: `CardioStageStrategy` — rules, performance, snapshot, settings

**Files:**
- Create: `src/A2S.Domain/Aggregates/Workout/Cardio/CardioLaneRules.cs`, `src/A2S.Domain/Aggregates/Workout/CardioStageStrategy.cs`, `src/A2S.Domain/ValueObjects/CardioResult.cs`
- Modify: `src/A2S.Domain/ValueObjects/ExercisePerformance.cs`, `ProgressionSnapshot.cs`, `Aggregates/Workout/ExerciseProgression.cs` (base members + `ProgressionData` fields + `CreateFromConfig`), `ProgressionConfig.cs`
- Test: `tests/A2S.Domain.Tests/Strategies/Cardio/CardioLaneRulesTests.cs`, `CardioStageStrategyTests.cs`, `CardioStageGoldenRunTests.cs`; extend `tests/A2S.Domain.Tests/ValueObjects/ExercisePerformanceTests.cs`, `ProgressionSnapshotTests.cs`, `tests/A2S.Domain.Tests/Strategies/StrategyPolymorphismTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  - `CardioResult` (ValueObject): `bool Finished, EffortRating Effort, PainFlag Pain, string? PainArea, int TotalSeconds, decimal? WorkMinutes, decimal? Intensity, decimal? Speed, int? Floors`; property `bool IsMiss => !Finished || Pain != PainFlag.None`.
  - `ExercisePerformance`: new optional ctor parameters `PlannedCardioBlock? plannedCardio = null, CardioResult? cardio = null`; properties `PlannedCardio`, `Cardio`. Rule: planned/completed sets are required **unless** `cardio != null`.
  - `static class CardioLaneRules { CardioStepDecision Decide(CardioLane lane, CardioResult result, int solidsBanked, int missStreak, out int newSolids, out int newMisses); }` with `enum CardioStepDecision { Hold, StepUp, StepBack }`.
  - `sealed class CardioStageStrategy : ExerciseProgression` (discriminator `"CardioStage"`): `CardioLane Lane`, `IReadOnlyList<CardioStageDefinition> Stages`, `CardioSegment? WarmUp`, `CardioSegment? CoolDown`, `bool RequiresSpeed`, `CardioStageState State`; `static CardioStageStrategy Create(CardioLanePreset preset)`; `PlannedCardioBlock CalculateCardioBlock()`; `CalculatePlannedSets` returns empty; `ApplyPerformanceResult` (no `Cardio` on the performance → no change); `internal CardioSettingsChange SetSettings(decimal? intensity, decimal? speed)` (applies `CardioLadder.Choose`; returns `record CardioSettingsChange(string OldText, string NewText)` from the block Text before and after, for the audit); `internal CardioStageState CaptureState()` / `internal void RestoreFromState(CardioStageState)`.
  - Base `ExerciseProgression` additions: `public virtual PlannedCardioBlock? CalculateCardioBlock() => null;`, `public virtual bool IsCardioSettingsPending => false;`.
  - `ProgressionSnapshot.FromState(ExerciseId, string, CardioStageState)` with type `"CardioStage"`, `GetCardioStageState()`.
  - `ProgressionData`: `CardioLane? CardioLane, int? CardioStageNumber, string? CardioStageName, string? CardioPrescription`.
  - `ProgressionConfig`: `CardioLane? CardioLane`; `CreateFromConfig` case `"cardiostage"` → `CardioStageStrategy.Create(lane == Hard ? CardioLanes.StairClimber() : CardioLanes.InclineWalk())`; error text lists `CardioStage`.
  - `GetProgressionChangeDescription` texts (exact): `"Step up: {next block title}"`, `"Banked 1 of 2 Solids"`, `"Same again"`, `"Step back: {block title}"`, `"Same again (1 of 2 before a step back)"`.
  - `GetCurrentWeight` / `GetTrainingMax` → null, `GetWeightIncrement` → 0 kg, `SupportsUnilateral` / `IsUnilateral` / `IsWeightPending` → false.

- [ ] **Step 1: Write the failing tests** — one `[Theory]` per lane covering every row of its rule table, for example:

```csharp
[Theory]
[InlineData(EffortRating.Easy,   0, CardioStepDecision.StepUp, 0)]
[InlineData(EffortRating.Solid,  0, CardioStepDecision.Hold,   1)]
[InlineData(EffortRating.Solid,  1, CardioStepDecision.StepUp, 0)]
[InlineData(EffortRating.Brutal, 1, CardioStepDecision.Hold,   1)] // banked Solid kept
public void HardLane_Effort(EffortRating effort, int banked, CardioStepDecision expected, int bankedAfter)

[Fact] public void HardLane_TwoMissesInARow_StepsBack()       // 1st miss Hold (misses 1), 2nd StepBack
[Fact] public void HardLane_MissKeepsBankedSolids()
[Theory] /* Easy → StepUp, Solid → Hold, Brutal → StepBack */ public void EasyLane_Effort(...)
[Fact] public void EasyLane_TwoMissesInARow_StepsBack()
[Fact] public void AnEffortAfterAMiss_ResetsTheMissStreak()
[Fact] public void Golden_StairClimber_ScriptedRun()   // scripted list of results → expected block Text after each
[Fact] public void Golden_InclineWalk_ScriptedRun()
[Fact] public void ApplyPerformanceResult_WithoutCardioResult_ChangesNothing()
[Fact] public void SnapshotRoundTrip_RestoresExactState()      // incl. History and ChosenIntensities
[Fact] public void ExercisePerformance_WithCardio_NeedsNoSets()
[Fact] public void ExercisePerformance_WithoutCardioOrSets_Throws()
[Fact] public void CreateFromConfig_CardioStage_Hard_IsStairClimber()
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Domain.Tests --filter "FullyQualifiedName~Cardio|FullyQualifiedName~ExercisePerformance|FullyQualifiedName~ProgressionSnapshot|FullyQualifiedName~StrategyPolymorphism"`; expected FAIL.
- [ ] **Step 3: Implement** the files and modifications listed.
- [ ] **Step 4: Run** the same filter, then the whole `A2S.Domain.Tests` project; expected all PASS (existing strategy tests unchanged).
- [ ] **Step 5: Commit** — `feat(cardio): CardioStageStrategy with hard and easy lane rules`

---

### Task 3: Cardio exercises on the workout (lane sharing, settings, pending)

**Files:**
- Modify: `src/A2S.Domain/Enums/ExerciseCategory.cs` (`Cardio = 4`), `Enums/AuditEntryType.cs` (append `CardioSettingsChanged`), `Aggregates/Workout/ProgressionAuditEntry.cs`, `Aggregates/Workout/Exercise.cs`, `Aggregates/Workout/Workout.cs`, `Services/SessionTimeEstimate.cs` (`RestDefaults.For` → 0 for Cardio)
- Modify: `tests/A2S.Tests.Shared/Builders/ExerciseBuilder.cs` (`AsCardioStage(CardioLane lane)`)
- Test: `tests/A2S.Domain.Tests/Aggregates/CardioExerciseTests.cs`

**Interfaces:**
- Consumes: Task 2 `CardioStageStrategy`, `CardioResult`.
- Produces:
  - `Exercise.CreateWithCardioStageProgression(string name, DayNumber assignedDay, int orderInDay, string externalTemplateId, CardioLane lane)` (category `Cardio`, equipment `Machine`).
  - `Exercise.IsCardioSettingsPending` (=> `Progression.IsCardioSettingsPending`); `ApplyProgression` checks `!IsCardioSettingsPending` with message `"Set the {Name} settings before finishing it."`.
  - `Workout.SetCardioSettings(ExerciseId exerciseId, decimal? intensity, decimal? speed)` → audit `ProgressionAuditEntry.CardioSettingsChanged(exerciseId, name, week, day, oldText, newText)`, then lane sync.
  - Lane sync (private in `Workout`): after `ApplyPerformancesToExercises`, after `SetCardioSettings`: every other `CardioStageStrategy` exercise with the same `Lane` gets `RestoreFromState(source.CaptureState())`.
  - `CapturePreProgressionSnapshots` also snapshots the lane siblings of the day's cardio exercises (undo already restores any snapshot by id).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void CompleteDay_EasyStairs_StepsUpBothUpperDays()   // Day1 & Day3 stairs: after Day1 Easy, Day3 block Text shows 7 min
[Fact] public void CompleteDay_WalkDoesNotTouchStairs()
[Fact] public void Undo_RestoresLaneSiblingToo()                    // Review Focus 1
[Fact] public void Complete_WhileSettingsPending_Throws()           // walk with blank speed → BusinessRuleValidationException, message above (Review Focus 4)
[Fact] public void SetCardioSettings_AddsAuditEntry_AndSyncsSibling()
[Fact] public void SetCardioSettings_OnNonCardioExercise_Throws()
[Fact] public void InsertExercise_Cardio_GoesLastOnTheDay()
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Domain.Tests --filter FullyQualifiedName~CardioExerciseTests`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the filter and the full domain test project; PASS.
- [ ] **Step 5: Commit** — `feat(cardio): cardio exercises share one progression per lane`

---

### Task 4: Persistence

**Files:**
- Create: `src/A2S.Infrastructure/Persistence/Configurations/CardioStageStrategyConfiguration.cs`
- Modify: `ExerciseProgressionConfiguration.cs` (`.HasValue<CardioStageStrategy>("CardioStage")`), `WorkoutConfiguration.cs` (map `ExercisePerformance.PlannedCardio` and `.Cardio` as owned JSON in **both** `CompletedActivities` and `ArchivedActivities`)
- Create: migration `AddCardioStageProgression` (`dotnet ef migrations add AddCardioStageProgression -p src/A2S.Infrastructure -s src/A2S.Api`)
- Test: extend `tests/A2S.Infrastructure.Tests/Persistence/ConfigurationTests.cs`; create `tests/A2S.Infrastructure.Tests/Persistence/CardioStagePersistenceTests.cs`

Columns (all nullable, prefix `CardioStage_`): `Lane` (string), `Stages` (jsonb), `WarmUp` (jsonb), `CoolDown` (jsonb), `RequiresSpeed` (bool), `State` (jsonb). Store the records with `HasConversion` to JSON (System.Text.Json), as `CardioTrackConfiguration` does for its jsonb columns.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void CardioStage_HasDiscriminatorValue()    // "CardioStage"
[Fact] public async Task CardioStageWorkout_RoundTrips()  // save stair + walk exercises, reload, State/Stages/History equal
[Fact] public async Task CompletedCardioActivity_RoundTrips() // performance with PlannedCardio + Cardio survives reload, also after ArchiveActivities
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Infrastructure.Tests --filter "FullyQualifiedName~CardioStage|FullyQualifiedName~ConfigurationTests"` (Docker running); FAIL.
- [ ] **Step 3: Implement the configuration and generate the migration.** Check the generated `Down` drops exactly the added columns.
- [ ] **Step 4: Run** the filter; PASS. Run `dotnet build`.
- [ ] **Step 5: Commit** — `feat(cardio): persist cardio stage progression`

---

### Task 5: Read side — week plan, workout, upcoming, budgets, history

**Files:**
- Modify: `src/A2S.Application/Queries/GetWeekPlan/WeekPlanDtos.cs` (`PlannedExerciseDto.CardioBlock: PlannedCardioBlockDto?`), `GetWeekPlanQueryHandler.cs` (fill it; metadata `IsCardioSettingsPending`), `DTOs/WorkoutDto.cs` (`[JsonDerivedType(typeof(CardioStageProgressionDto), "CardioStage")]`), `Queries/GetWorkout/GetCurrentWorkoutQueryHandler.cs`, `Queries/Plans/GetUpcomingDaysQuery.cs` + `DTOs/PlanDtos.cs` (`UpcomingExerciseDto.Cardio: string?` = block `Text`; keep exercises that have a block), `Services/DayBudgetCalculator.cs` (add `TotalMinutes`), `Services/ConfiguredExerciseFactory.cs` (case `"CardioStage"`), `Commands/AddExercise/AddExerciseCommandValidator.cs`, `Commands/CreateWorkout/CreateWorkoutCommand.cs` (`CardioLane? CardioLane`), `Queries/GetWorkoutHistory/GetWorkoutHistoryQueryHandler.cs` and `Queries/GetExerciseHistory/GetExerciseHistoryQueryHandler.cs` (keep performances with `Cardio`; history DTO gets `CardioSummary: string?`, e.g. `"7 min @ level 6 · Easy"`), `Hevy/ExerciseNoteBuilder.cs` (note = block Text)
- Test: extend `GetWeekPlanQueryHandlerTests`, `GetWorkoutHistoryQueryHandlerTests`, `AddExerciseCommandHandlerTests`; add `tests/A2S.Application.Tests/Queries/UpcomingDaysCardioTests.cs`

**Interfaces:**
- Consumes: Task 3 `Exercise.CalculateCardioBlock()` via `Progression`.
- Produces: `PlannedCardioBlockDto(string Lane, string StageName, int StageNumber, int StageCount, TrackSegmentDto? WarmUp, TrackSegmentDto? CoolDown, string Kind, int WorkMinutes, IntervalFormatDto? Intervals, decimal? RecoveryLevel, string IntensityUnit, decimal? Intensity, decimal? SuggestedIntensity, decimal? Speed, bool IsSettingsPending, int TotalMinutes, string Text)`; `IntervalFormatDto(int Rounds, int HardMinutes, int EasyMinutes)`; `CardioStageProgressionDto : ExerciseProgressionDto { Lane, StageNumber, StageName, Prescription, IsSettingsPending }`. `TrackSegmentDto` is the existing type (reuse).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task WeekPlan_CardioExercise_HasBlockAndNoSets()   // Day1 stairs: PlannedSets empty, CardioBlock.Text "3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)", RestSeconds 0
[Fact] public async Task Upcoming_KeepsCardioExercise_WithText()      // contract: existing fields unchanged
[Fact] public async Task History_ShowsCardioPerformance()             // CardioSummary "7 min @ level 6 · Easy"
[Fact] public async Task AddExercise_CardioStage_IsAccepted()
[Fact] public void DayBudget_CountsCardioMinutes()
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Application.Tests --filter "FullyQualifiedName~Cardio|FullyQualifiedName~GetWeekPlan|FullyQualifiedName~GetWorkoutHistory|FullyQualifiedName~AddExercise"`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the filter and full `A2S.Application.Tests`; PASS.
- [ ] **Step 5: Commit** — `feat(cardio): cardio blocks in week plan, upcoming days and history`

---

### Task 6: Write side — complete day, gym finish, cardio settings endpoint

**Files:**
- Modify: `src/A2S.Application/Commands/CompleteDay/ExercisePerformanceRequest.cs` (`CardioResultRequest? Cardio`; `CompletedSets` no longer `required`, default empty), `CompleteDayCommandValidator.cs` (sets required unless `Cardio` set; Cardio: `TotalSeconds` 1 s–4 h, `WorkMinutes` 0–240, `Intensity` 0–100, `Speed` 0–25, `Floors` 0–2000), `CompleteDayCommandHandler.cs` (cardio request → `CardioResult` + `exercise.Progression.CalculateCardioBlock()` into the performance; skip the cardio exercise in `CalculateNextSessionPlan`; pending settings → `Result` validation error, not an exception), `Commands/GymSessions/FinishGymSessionCommand.cs` validator (same cardio bounds), `Commands/SimulateAndCompleteDay/*` (cardio → finished, Easy)
- Create: `src/A2S.Application/Commands/CardioSettings/SetCardioSettingsCommand.cs` (`record SetCardioSettingsCommand(Guid WorkoutId, Guid ExerciseId, decimal? Intensity, decimal? Speed) : IRequest<Result<ExerciseDto>>`, `[ChangesSyncArea(SyncArea.Workout)]`, validator: at least one value, Intensity 0–100, Speed 0.5–25, handler + validator in the file's usual layout)
- Modify: `src/A2S.Api/Controllers/ExercisesController.cs` — `PUT /api/v1/workouts/{id}/exercises/{exerciseId}/cardio-settings` body `{ intensity?, speed? }`
- Test: extend `CompleteDayCommandHandlerTests`, `tests/A2S.Application.Tests/Hevy/GymSessionTests.cs`; create `SetCardioSettingsCommandHandlerTests.cs`; API `tests/A2S.Api.Tests/Integration/CardioExerciseIntegrationTests.cs`

**Interfaces:**
- Produces: `record CardioResultRequest(bool Finished, Effort Effort, PainFlag Pain = PainFlag.None, string? PainArea = null, int TotalSeconds = 0, decimal? WorkMinutes = null, decimal? Intensity = null, decimal? Speed = null, int? Floors = null)` (reuse `Plan/Effort.cs` and its `EffortRating` mapping used by `FinisherCardioLog`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task CompleteDay_CardioEasy_StepsUpTheLane()
[Fact] public async Task CompleteDay_WithoutCardioPerformance_KeepsCardioProgression()  // Review Focus 2
[Fact] public async Task CompleteDay_PendingCardioSettings_ReturnsValidationError()     // Review Focus 4
[Fact] public void Validator_CardioWithoutSets_IsValid()
[Fact] public void Validator_NoSetsAndNoCardio_IsInvalid()
[Fact] public async Task FinishGymSession_WithCardioPerformance_CompletesAndStagesPost()
[Fact] public async Task SetCardioSettings_SetsSpeedAndIncline_AndBumpsWorkoutArea()
[Fact] public async Task Api_PutCardioSettings_ThenCompleteDay_Returns200()   // integration
[Fact] public async Task Api_CardioSettings_WithPersonalToken_Is403()          // deny-by-default holds
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Application.Tests --filter "FullyQualifiedName~CompleteDay|FullyQualifiedName~CardioSettings|FullyQualifiedName~GymSession"` and `dotnet test tests/A2S.Api.Tests --filter FullyQualifiedName~CardioExercise`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** both filters, then `ChangeVersionBehaviorTests` and `ApiTokenAllowListTests`; PASS.
- [ ] **Step 5: Commit** — `feat(cardio): complete cardio exercises and edit their settings`

---

### Task 7: Hevy — post, routine, pull

**Files:**
- Modify: `src/A2S.Application/Hevy/HevyWorkoutPoster.cs` (a performance with `Cardio` → one duration exercise in the same workout: stairs `4377A52B` with `CustomMetric = Floors` when logged, walk `243710DE`; `warmup` set for the warm-up, `normal` set for the work, `warmup`-typed set for the cool-down only when the block has one; RPE from effort via the existing allowed-RPE helper; note = block Text + `" · {Effort}"`), `RoutineBuilder.cs` (`Lift(...)`: a cardio exercise becomes the same shape `TrackExercise` produces — warm-up set, one set per hard round or one work set, cool-down set — with block Text in the notes; no rest), `HevySessionCompleter.cs` `BuildPerformances` (cardio exercise matched by template id → `CardioResult` only when the Hevy set has an RPE: `Finished = true`, effort `EffortRatings.FromRpe`, `TotalSeconds` = summed durations; else omit), `HevyCardioTrackLogger.cs` (skip a Hevy exercise whose template id belongs to a `Cardio` exercise of the matched lift day)
- Test: extend `tests/A2S.Application.Tests/Hevy/{GymSessionTests, RoutineBuilderTests, CardioTrackHevyPullTests}.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Post_StairCardioExercise_IsOneDurationExerciseInTheWorkout()  // template 4377A52B, no separate cardio post
[Fact] public async Task Post_WalkCardioExercise_UsesTreadmill()                       // 243710DE, duration = TotalSeconds
[Fact] public void Routine_StairIntervals_HaveOneSetPerHardRound()                     // 4 × 2/2 → warm-up + 4 work sets + cool-down
[Fact] public async Task Pull_CardioWithRpe_CompletesTheLane()
[Fact] public async Task Pull_CardioWithoutRpe_LeavesProgressionUnchanged()
[Fact] public async Task Pull_StairExerciseOnLiftDay_IsNotLoggedToTheOldTrack()
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Application.Tests --filter FullyQualifiedName~Hevy`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the filter; PASS.
- [ ] **Step 5: Commit** — `feat(cardio): send cardio exercises to Hevy inside the lift workout`

---

### Task 8: Plan v3 and moving the running plan over

**Files:**
- Modify: `src/A2S.Application/SuggestedPlans/PlanV3BaseBuildTransition.cs`: add `Cardio(string name, string templateId, DayNumber day, int order, CardioLane lane)` helper producing a `CreateExerciseRequest` with `ProgressionType = "CardioStage"`, `Category = Cardio`; stairs on Day1/Day3 and `Treadmill` (Easy lane) on Day2/Day4, each last on its day; remove the `Stairs` and `CoolDown` finishers from `Schedule()`; `CardioTracks` keeps only `WalkToRun`; rewrite phase notes — Base: stairs and incline walk progress on their own each workout; Build: the old "Stair finisher: move on to … intervals when the track suggests it" line becomes "Stair climber moves to 4 × 4 intervals on its own once continuous work reaches 12 min". `PlanTemplatePreview.cs` shows cardio exercises with their starting block Text.
- Create: `src/A2S.Infrastructure/SeedData/CardioExerciseBackfill.cs` — `static Task RunAsync(A2SDbContext db, ILogger logger)`, called from `src/A2S.Api/Program.cs` after `MuscleGroupBackfill`. For each active plan whose template key is `plan-v3` (or a former name via `PlanTemplates.IsSamePlan`) and whose linked workout has no `Cardio` exercise: insert the four cardio exercises with `Workout.InsertExercise` (order past the end → last); seed the hard lane by replaying the user's `stair-finisher` track sessions (oldest first, from the start state) through `CardioStageStrategy.ApplyPerformanceResult` using each session's effort, pain and completed flag, then sync the sibling; clear `Finisher` on lift slots whose finisher is the stair track or a fixed cool-down; pause the stair track. One `SaveChanges` per user.
- Test: extend `tests/A2S.Application.Tests/GymPlanFit/PlanV3TemplateTests.cs`; create `tests/A2S.Infrastructure.Tests/Repositories/CardioExerciseBackfillTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void PlanV3_EachLiftDay_EndsWithItsCardioExercise()      // Day1/3 Stair Machine (Floors) Hard, Day2/4 Treadmill Easy
[Fact] public void PlanV3_LiftSlots_HaveNoFinishers()
[Fact] public void PlanV3_BuildPhaseNote_SaysIntervalsAreAutomatic()
[Fact] public async Task Backfill_OneEasySixMinuteSession_SeedsSevenMinutesLevelSix()  // Global Constraints seed
[Fact] public async Task Backfill_ClearsFinishers_AndPausesStairTrack()
[Fact] public async Task Backfill_IsIdempotent()                         // Review Focus 5: second run adds nothing, keeps progressed state
[Fact] public async Task Backfill_IgnoresOtherPlans()
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test tests/A2S.Application.Tests --filter FullyQualifiedName~PlanV3` and `dotnet test tests/A2S.Infrastructure.Tests --filter FullyQualifiedName~CardioExerciseBackfill`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** both filters and the full backend suite `dotnet test`; PASS.
- [ ] **Step 5: Commit** — `feat(plans): Plan v3 cardio exercises and backfill for the running plan`

---

### Task 9: Gym mode

**Files:**
- Modify: `src/A2S.Web/src/types/workoutDtos.ts` (`'CardioStage'` in the progression unions, `CardioResultRequest`, `ExercisePerformanceRequest.cardio?`, `completedSets` optional), `types/workout.ts` (`ExerciseCategory.Cardio = 4`), `api/workouts.ts` (`PlannedExerciseDto.cardioBlock?: PlannedCardioBlockDto | null`, `setCardioSettings(workoutId, exerciseId, body)`), `hooks/useWorkouts.ts` (`useSetCardioSettings`)
- Modify: `features/gym/gymSession.ts`: state `version: 2`; new `cardio: GymCardio[]` (`{exerciseId, name, lane, block, done, finished, effort, pain?, totalSeconds?, workMinutes?, intensity?, speed?, floors?, skipped?}`); `createGymSession` builds it from exercises with a `cardioBlock` (they no longer disappear in the `plannedSets.length > 0` filter); `toFinishRequest` adds a performance with `cardio` for each done item; `migrateGymSession` upgrades v1 → v2 (`cardio: []`); `restSecondsFor` → 0 for `Cardio`
- Modify: `features/gym/finisher/finisherPlan.ts` (`FinisherIntervals.restAfterLastRound?: boolean`, default true; false skips the last easy phase), `gymFinisherPlan.ts` (`cardioPhases(block)` from a `PlannedCardioBlockDto`)
- Create: `features/gym/GymCardioCard.tsx` (block Text, Start (stairs → `FinisherWizard`, reusing the phase-push schedule), a tick for the walk, Easy/Solid/Brutal buttons, "Stopped early" and pain toggles; Finish prefills minutes from the wizard summary), `features/gym/CardioSettingsDialog.tsx` (opens when the block `isSettingsPending`: walk asks speed in km/h — "a brisk walk where you can still talk in full sentences" — and incline with 5 % prefilled; stairs at Stage 2 asks hard level prefilled with `suggestedIntensity` — "a level you could hold for 2 hard minutes, 4 times"; saves through `setCardioSettings`, then refetches the plan)
- Modify: `features/gym/GymChecklist.tsx` (cardio cards after the last lift block), `GymModePage.tsx` (red readiness `finisherSkipped` marks the Hard-lane item `skipped` unless kept)
- Test: `features/gym/gymSession.test.ts`, `GymChecklist.test.tsx`, new `GymCardioCard.test.tsx`, new `CardioSettingsDialog.test.tsx`, `finisher/finisherPlan.test.ts`; fixtures in `gymFixtures.ts`

- [ ] **Step 1: Write the failing tests**

```ts
it('builds a cardio item from a cardio exercise with no planned sets')
it('sends a cardio performance only when the item is done')          // pairs with Review Focus 2
it('migrateGymSession upgrades a v1 draft')                          // Review Focus 3: v1 sets kept, cardio []
it('skips the last easy phase when restAfterLastRound is false')    // 4 × 4/3 → 3 easy phases, 30 min total with warm-up/cool-down
it('asks for walk speed and incline before starting when settings are pending')
it('asks for the hard level at stage 2 with the suggestion prefilled')
it('red readiness skips the stair climber but keeps the walk')
```

- [ ] **Step 2: Run to verify they fail** — `cd src/A2S.Web && npx vitest run src/features/gym`; FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npx vitest run src/features/gym` then `npm run lint` and `npx tsc -b`; PASS.
- [ ] **Step 5: Commit** — `feat(gym): cardio exercises in gym mode`

---

### Task 10: Plan and history views, docs

**Files:**
- Modify: `features/workout/{DayCard.tsx, ExerciseCard.tsx, WorkoutDashboard.tsx, WeekOverview.tsx, ExerciseProgressionModal.tsx, ExerciseProgressionTables.tsx}` (a `CardioStage` exercise shows its stage and block Text instead of sets; the progression modal lists cardio history: date, prescription, effort, and the audit's settings changes), `utils/workoutCalculations.ts` (no volume for cardio), `features/history/WorkoutHistoryComponents.tsx` (cardio rows show `cardioSummary`, not weight × reps), `features/current/{CycleList.tsx, CurrentSessionCard.tsx}` (cardio exercise listed with the day), `lib/templateConversion.ts`, `features/exercises/AddToPlanPanel.tsx` (unchanged behaviour; just type-checks)
- Modify: `AGENTS.md` (Domain Model: the fourth strategy, `PlannedCardioBlock`, `CardioResult`, `ExerciseCategory.Cardio`, `AuditEntryType.CardioSettingsChanged`; Progression Strategies: a "Cardio stage" section with both lanes' stages and rules and the lane-sharing rule; Commands: `SetCardioSettings`; Plan v3 notes; Gym mode: cardio items replace the stair/fixed finisher on Plan v3), `src/A2S.Web/AGENTS.md` if it lists gym state
- Test: `features/workout/*.test.tsx` where present, `features/history/*.test.tsx`, `features/current/cycle.test.ts`

- [ ] **Step 1: Write the failing tests**

```ts
it('Full plan and history shows the stair climber on Upper A with its prescription')
it('history lists a cardio performance as "7 min @ level 6 · Easy"')
it('progression modal for a cardio exercise lists sessions and settings changes')
```

- [ ] **Step 2: Run to verify they fail** — `npx vitest run src/features/workout src/features/history src/features/current`; FAIL.
- [ ] **Step 3: Implement**, then update the two AGENTS.md files.
- [ ] **Step 4: Run** `npm test`, `npm run build` (note any pre-existing build failure separately), and backend `dotnet test`; PASS.
- [ ] **Step 5: Commit** — `feat(cardio): cardio exercises in plan and history views; docs`
