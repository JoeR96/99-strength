using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Phase 3 audit: edge case and boundary tests for all three progression strategies.
/// Covers: creation validation boundaries, deferred weight progression, block boundary transitions,
/// capture/restore snapshot round-trips, and equipment-specific edge cases.
/// </summary>
public class ProgressionStrategyEdgeCaseTests
{
    private readonly ExerciseId _exerciseId = new(Guid.Parse("eee88888-8888-8888-8888-888888888888"));
    private readonly TrainingMax _tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
    private readonly Weight _weight = Weight.Create(20m, WeightUnit.Kilograms);
    private readonly RepRange _repRange = RepRange.Create(8, 15);

    #region Linear — Creation Boundary Tests

    [Theory]
    [InlineData(2)]  // Below minimum (3)
    [InlineData(9)]  // Above maximum (8)
    public void LinearCreate_InvalidBaseSets_ShouldThrow(int baseSets)
    {
        var act = () => LinearProgressionStrategy.Create(
            _tm, useAmrap: true, baseSetsPerExercise: baseSets, tier: ProgramTier.Primary);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void LinearCreate_ValidBaseSets_ShouldSucceed(int baseSets)
    {
        var strategy = LinearProgressionStrategy.Create(
            _tm, useAmrap: true, baseSetsPerExercise: baseSets, tier: ProgramTier.Primary);

        strategy.BaseSetsPerExercise.Should().Be(baseSets);
    }

    [Fact]
    public void LinearCreate_InvalidBlockNumber_ShouldThrow()
    {
        var strategy = LinearProgressionStrategy.Create(
            _tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var act0 = () => strategy.CalculatePlannedSets(1, 0).ToList();
        var act4 = () => strategy.CalculatePlannedSets(1, 4).ToList();

        act0.Should().Throw<BusinessRuleViolationException>();
        act4.Should().Throw<BusinessRuleViolationException>();
    }

    #endregion

    #region Linear — Deload Week TM Preservation

    [Fact]
    public void Linear_DeloadWeek_ApplyPerformance_ShouldNotChangeTm()
    {
        var strategy = LinearProgressionStrategy.Create(
            _tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var plannedSets = strategy.CalculatePlannedSets(7, 1).ToList(); // Deload week

        // Complete all sets (no AMRAP on deload)
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, s.TargetReps, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_exerciseId, plannedSets, completedSets);
        strategy.ApplyPerformanceResult(performance);

        strategy.TrainingMax.Value.Should().Be(100m, "Deload week should not change TM");
    }

    #endregion

    #region Linear — No AMRAP Mode

    [Fact]
    public void Linear_NoAmrap_AllSetsShouldBeNonAmrap()
    {
        var strategy = LinearProgressionStrategy.Create(
            _tm, useAmrap: false, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        plannedSets.Should().AllSatisfy(s => s.IsAmrap.Should().BeFalse());
    }

    #endregion

    #region RPS — Deferred Starting Weight

    [Fact]
    public void RpsCreate_WithoutStartingWeight_IsWeightPending()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable);

        strategy.IsWeightPending.Should().BeTrue();
        strategy.CurrentWeight.Should().BeNull();
    }

    [Fact]
    public void RpsWithPendingWeight_CalculatePlannedSets_UsesZeroWeight()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        plannedSets.Should().AllSatisfy(s => s.Weight.Value.Should().Be(0m));
    }

    [Fact]
    public void RpsWithPendingWeight_ApplyPerformance_IsNoOp()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingSets: 3, targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));

        strategy.CurrentSetCount.Should().Be(3, "Sets should not change while weight is pending");
    }

    [Fact]
    public void RpsConfirmStartingWeight_ThenApplyPerformance_Works()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingSets: 3, targetSets: 5);

        strategy.ConfirmStartingWeight(Weight.Create(30m, WeightUnit.Kilograms));
        strategy.IsWeightPending.Should().BeFalse();
        strategy.CurrentWeight!.Value.Should().Be(30m);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));

        strategy.CurrentSetCount.Should().Be(4, "Should progress after confirming weight");
    }

    [Fact]
    public void RpsConfirmStartingWeight_AlreadyConfirmed_ShouldThrow()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable, startingWeight: _weight);

        var act = () => strategy.ConfirmStartingWeight(Weight.Create(30m, WeightUnit.Kilograms));

        act.Should().Throw<BusinessRuleViolationException>();
    }

    #endregion

    #region RPS — Creation Validation Boundaries

    [Theory]
    [InlineData(0)]   // Below minimum (1)
    [InlineData(11)]  // Above maximum (10)
    public void RpsCreate_InvalidStartingSets_ShouldThrow(int startingSets)
    {
        var act = () => RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: startingSets, targetSets: 5, startingWeight: _weight);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void RpsCreate_TargetSetsLessThanStartingSets_ShouldThrow()
    {
        var act = () => RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 5, targetSets: 3, startingWeight: _weight);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    #endregion

    #region RPS — Weight Increment Boundary (Dumbbell < 10kg vs >= 10kg)

    [Fact]
    public void Rps_DumbbellBelow10kg_Increment1kg()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Dumbbell,
            startingSets: 5, targetSets: 5,
            startingWeight: Weight.Create(8m, WeightUnit.Kilograms));

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));

        strategy.CurrentWeight!.Value.Should().Be(9m, "Dumbbell < 10kg: +1kg");
    }

    [Fact]
    public void Rps_DumbbellAtExactly10kg_Increment2kg()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Dumbbell,
            startingSets: 5, targetSets: 5,
            startingWeight: Weight.Create(10m, WeightUnit.Kilograms));

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));

        strategy.CurrentWeight!.Value.Should().Be(12m, "Dumbbell >= 10kg: +2kg");
    }

    #endregion

    #region MinimalSets — Creation Validation Boundaries

    [Theory]
    [InlineData(9)]   // Below minimum (10)
    [InlineData(201)] // Above maximum (200)
    public void MinimalSetsCreate_InvalidTargetReps_ShouldThrow(int targetReps)
    {
        var act = () => MinimalSetsStrategy.Create(
            _weight, targetTotalReps: targetReps, startingSets: 3, equipment: EquipmentType.Machine);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData(0)]   // Below minimum (1)
    [InlineData(21)]  // Above maximum (20)
    public void MinimalSetsCreate_InvalidStartingSets_ShouldThrow(int startingSets)
    {
        var act = () => MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 40, startingSets: startingSets, equipment: EquipmentType.Machine);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void MinimalSetsCreate_MaxLessThanStarting_ShouldThrow()
    {
        var act = () => MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 40, startingSets: 5, equipment: EquipmentType.Machine,
            minimumSets: 2, maximumSets: 3);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    #endregion

    #region MinimalSets — Exact Boundary Behavior

    [Fact]
    public void MinimalSets_AtBoundaryReps10_ShouldCreate()
    {
        var strategy = MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 10, startingSets: 2, equipment: EquipmentType.Machine);

        strategy.TargetTotalReps.Should().Be(10);
    }

    [Fact]
    public void MinimalSets_AtBoundaryReps200_ShouldCreate()
    {
        var strategy = MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 200, startingSets: 5, equipment: EquipmentType.Machine);

        strategy.TargetTotalReps.Should().Be(200);
    }

    [Fact]
    public void MinimalSets_OddDistribution_ShouldNotLoseReps()
    {
        var strategy = MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 41, startingSets: 4, equipment: EquipmentType.Machine);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        plannedSets.Sum(s => s.TargetReps).Should().Be(41, "Total reps must equal target exactly");
        plannedSets.Should().HaveCount(4);
        // 41 / 4 = 10 remainder 1 → first set gets +1
        plannedSets[0].TargetReps.Should().Be(11);
        plannedSets[1].TargetReps.Should().Be(10);
        plannedSets[2].TargetReps.Should().Be(10);
        plannedSets[3].TargetReps.Should().Be(10);
    }

    #endregion

    #region Snapshot Round-Trip — Linear

    [Fact]
    public void Linear_CaptureAndRestore_PreservesTmAndState()
    {
        var strategy = LinearProgressionStrategy.Create(
            TrainingMax.Create(105.5m, WeightUnit.Kilograms),
            useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var snapshot = strategy.CaptureSnapshot(_exerciseId, "Test Exercise");

        // Modify strategy
        strategy.UpdateTrainingMaxValue(TrainingMax.Create(200m, WeightUnit.Kilograms));
        strategy.TrainingMax.Value.Should().Be(200m);

        // Restore
        strategy.RestoreFromSnapshot(snapshot);

        strategy.TrainingMax.Value.Should().Be(105.5m);
    }

    #endregion

    #region Snapshot Round-Trip — RPS

    [Fact]
    public void Rps_CaptureAndRestore_PreservesAllState()
    {
        var strategy = RepsPerSetStrategy.Create(
            _repRange, EquipmentType.Cable,
            startingSets: 3, targetSets: 5,
            startingWeight: Weight.Create(25m, WeightUnit.Kilograms));

        // Progress once
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));

        strategy.CurrentSetCount.Should().Be(4);

        var snapshot = strategy.CaptureSnapshot(_exerciseId, "Test");

        // Modify
        var plannedSets2 = strategy.CalculatePlannedSets(2, 1).ToList();
        var completedSets2 = plannedSets2.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets2, completedSets2));
        strategy.CurrentSetCount.Should().Be(5);

        // Restore
        strategy.RestoreFromSnapshot(snapshot);

        strategy.CurrentSetCount.Should().Be(4);
        strategy.CurrentWeight!.Value.Should().Be(25m);
    }

    #endregion

    #region Snapshot Round-Trip — MinimalSets

    [Fact]
    public void MinimalSets_CaptureAndRestore_PreservesAllState()
    {
        var strategy = MinimalSetsStrategy.Create(
            _weight, targetTotalReps: 40, startingSets: 4,
            equipment: EquipmentType.Machine, minimumSets: 2, maximumSets: 8);

        // Progress once (success = reduce sets)
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 20, false),
            new(2, plannedSets[1].Weight, 20, false)
        };
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, plannedSets, completedSets));
        strategy.CurrentSetCount.Should().Be(3);

        var snapshot = strategy.CaptureSnapshot(_exerciseId, "Test");

        // Modify further
        strategy.ResetSetCount();
        strategy.CurrentSetCount.Should().Be(4);

        // Restore
        strategy.RestoreFromSnapshot(snapshot);

        strategy.CurrentSetCount.Should().Be(3);
    }

    #endregion

    #region Linear — Weight Calculation Precision

    [Fact]
    public void Linear_WorkingWeight_ShouldReflectTmPercentage()
    {
        var strategy = LinearProgressionStrategy.Create(
            TrainingMax.Create(100m, WeightUnit.Kilograms),
            useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // All sets should use the same weight (TM * intensity for week 1)
        var weight = plannedSets.First().Weight;
        plannedSets.Should().AllSatisfy(s => s.Weight.Should().Be(weight));
    }

    #endregion
}
