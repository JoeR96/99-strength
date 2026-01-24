using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Unit tests for RepsPerSetStrategy covering accessory exercise progression.
/// Tests verify set progression, weight increases, and failure handling.
///
/// Based on spreadsheet data:
/// - Lat Pulldown: 3 sets → 4 sets → 5 sets (then weight increase)
/// - Rep range: 8-12-15 (min-target-max)
/// </summary>
public class RepsPerSetStrategyTests
{
    private readonly ExerciseId _testExerciseId = new(Guid.NewGuid());
    private readonly Weight _startingWeight = Weight.Create(20m, WeightUnit.Kilograms);
    private readonly RepRange _standardRepRange = RepRange.Create(8, 12, 15);

    #region Creation Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Act
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5,
            isUnilateral: false);

        // Assert
        strategy.Should().NotBeNull();
        strategy.RepRange.Should().Be(_standardRepRange);
        strategy.CurrentWeight.Value.Should().Be(20m);
        strategy.CurrentSetCount.Should().Be(3);
        strategy.TargetSets.Should().Be(5);
        strategy.IsUnilateral.Should().BeFalse();
    }

    [Fact]
    public void Create_WithUnilateral_ShouldHaveLowerMaxSets()
    {
        // Act
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Dumbbell,
            startingSets: 2,
            targetSets: 4,
            isUnilateral: true);

        // Assert
        strategy.MaxSets.Should().Be(3, "Unilateral exercises max at 3 sets per side");
        strategy.IsUnilateral.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBilateral_ShouldHaveHigherMaxSets()
    {
        // Act
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 2,
            targetSets: 5,
            isUnilateral: false);

        // Assert
        strategy.MaxSets.Should().Be(5, "Bilateral exercises max at 5 sets");
    }

    #endregion

    #region Planned Sets Tests

    [Fact]
    public void CalculatePlannedSets_ShouldReturnCurrentSetCount()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Assert
        plannedSets.Should().HaveCount(3);
        plannedSets.All(s => s.TargetReps == 12).Should().BeTrue("All sets should target rep range target");
        plannedSets.All(s => s.Weight.Value == 20m).Should().BeTrue("All sets should use current weight");
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue("RepsPerSet exercises should never have AMRAP");
    }

    [Fact]
    public void CalculatePlannedSets_ShouldNotVaryByWeek()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 4,
            targetSets: 5);

        // Act - Compare week 1 vs week 15
        var week1Sets = strategy.CalculatePlannedSets(1, 1).ToList();
        var week15Sets = strategy.CalculatePlannedSets(15, 3).ToList();

        // Assert - Should be identical (RepsPerSet doesn't use week-based periodization)
        week1Sets.Should().HaveCount(week15Sets.Count);
    }

    #endregion

    #region Success Progression Tests

    [Fact]
    public void ApplyPerformanceResult_AllSetsHitMax_ShouldAddOneSet()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // All sets hit max reps (15)
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(4, "Should add one set when all sets hit max reps");
        strategy.CurrentWeight.Value.Should().Be(20m, "Weight should not change yet");
    }

    [Fact]
    public void ApplyPerformanceResult_AtTargetSets_AllHitMax_ShouldIncreaseWeight()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 5, // Already at target
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - Cable uses 2.5kg increments
        strategy.CurrentWeight.Value.Should().Be(22.5m, "Weight should increase by 2.5kg for cable");
        strategy.CurrentSetCount.Should().Be(5, "Sets should reset to starting (which is already 5)");
    }

    [Fact]
    public void ApplyPerformanceResult_UnilateralAtMax_ShouldIncreaseWeight()
    {
        // Arrange - Unilateral with max 3 sets
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Dumbbell,
            startingSets: 3, // At max for unilateral
            targetSets: 5,   // Target is higher but capped at 3
            isUnilateral: true);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - Dumbbell 20kg uses 2kg increment (>10kg threshold)
        strategy.CurrentWeight.Value.Should().Be(22m, "Weight should increase by 2kg for dumbbell >10kg");
    }

    #endregion

    #region Maintained Tests

    [Fact]
    public void ApplyPerformanceResult_AllSetsHitTarget_ShouldMaintain()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // All sets hit target reps (12) but not max (15)
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 12, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(3, "Sets should not change when hitting target");
        strategy.CurrentWeight.Value.Should().Be(20m, "Weight should not change");
    }

    [Fact]
    public void ApplyPerformanceResult_MixedRepsAboveMin_ShouldMaintain()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Mixed reps: 14, 12, 10 - all above min (8), some hit max
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 14, false),
            new(2, plannedSets[1].Weight, 12, false),
            new(3, plannedSets[2].Weight, 10, false)
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - Not all hit max, so should maintain
        strategy.CurrentSetCount.Should().Be(3, "Sets should not change with mixed reps");
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void ApplyPerformanceResult_AnySetBelowMin_ShouldRemoveOneSet()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 4,
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // One set below min (7 < 8)
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 12, false),
            new(2, plannedSets[1].Weight, 10, false),
            new(3, plannedSets[2].Weight, 7, false),  // Below min
            new(4, plannedSets[3].Weight, 9, false)
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(3, "Should remove one set when any set is below min");
        strategy.CurrentWeight.Value.Should().Be(20m, "Weight should not change");
    }

    [Fact]
    public void ApplyPerformanceResult_AtMinimumSets_BelowMin_ShouldDecreaseWeight()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 1, // Already at minimum
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 6, false) // Below min
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - Cable uses 2.5kg decrement
        strategy.CurrentSetCount.Should().Be(1, "Already at minimum sets");
        strategy.CurrentWeight.Value.Should().Be(17.5m, "Weight should decrease by 2.5kg");
    }

    #endregion

    #region Weight Increment Tests (Equipment-Based)

    [Theory]
    [InlineData(EquipmentType.Dumbbell, 8, 1)]    // Dumbbell < 10kg: +1kg
    [InlineData(EquipmentType.Dumbbell, 15, 2)]   // Dumbbell >= 10kg: +2kg
    [InlineData(EquipmentType.Cable, 20, 2.5)]    // Cable: +2.5kg
    [InlineData(EquipmentType.Machine, 30, 2.5)]  // Machine: +2.5kg
    [InlineData(EquipmentType.Barbell, 40, 2.5)]  // Barbell: +2.5kg
    public void WeightIncrement_ShouldVaryByEquipment(
        EquipmentType equipment, decimal startingWeight, decimal expectedIncrement)
    {
        // Arrange
        var weight = Weight.Create(startingWeight, WeightUnit.Kilograms);
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            weight,
            equipment,
            startingSets: 5, // At target
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentWeight.Value.Should().Be(startingWeight + expectedIncrement);
    }

    [Fact]
    public void WeightIncrement_Bodyweight_ShouldNotChangeWeight()
    {
        // Arrange
        var weight = Weight.Create(0m, WeightUnit.Kilograms);
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            weight,
            EquipmentType.Bodyweight,
            startingSets: 5,
            targetSets: 5);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 15, wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentWeight.Value.Should().Be(0m, "Bodyweight exercises don't add weight");
    }

    #endregion

    #region Full 21-Week Progression Simulation

    [Fact]
    public void Full21WeekCycle_LatPulldown_ShouldProgressCorrectly()
    {
        // Arrange - Lat Pulldown starts at 3 sets, targets 5 sets
        // Based on spreadsheet: 3 → 4 → 5 → weight increase → reset
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            Weight.Create(40m, WeightUnit.Kilograms),
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        var progressionHistory = new List<(int Week, int Sets, decimal Weight)>
        {
            (0, 3, 40m) // Starting point
        };

        // Simulate 21 weeks with mostly successful performance
        for (int week = 1; week <= 21; week++)
        {
            var plannedSets = strategy.CalculatePlannedSets(week, (week - 1) / 7 + 1).ToList();

            // Simulate mostly hitting max reps (success pattern)
            var hitMax = week % 4 != 0; // Fail every 4th week
            var reps = hitMax ? 15 : 10; // Max or maintained

            var completedSets = plannedSets.Select((s, i) => new CompletedSet(
                i + 1, s.Weight, reps, wasAmrap: false)).ToList();

            var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
            strategy.ApplyPerformanceResult(performance);

            progressionHistory.Add((week, strategy.CurrentSetCount, strategy.CurrentWeight.Value));
        }

        // Assert - Should have progressed weight at least once
        var finalWeight = strategy.CurrentWeight.Value;
        finalWeight.Should().BeGreaterThan(40m, "Weight should have increased over 21 weeks");

        // Verify the progression pattern (sets should fluctuate between starting and target)
        var weightIncreases = progressionHistory.Where((h, i) =>
            i > 0 && h.Weight > progressionHistory[i - 1].Weight).Count();
        weightIncreases.Should().BeGreaterThan(0, "Should have had at least one weight increase");
    }

    [Fact]
    public void Full21WeekCycle_WithVariedPerformance_ShouldHandleAllScenarios()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            Weight.Create(30m, WeightUnit.Kilograms),
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        // Define performance pattern: Success, Success, Maintained, Failed, repeat
        var performancePattern = new[] { 15, 15, 12, 6 }; // Max, Max, Target, Below min

        // Act - 21 weeks
        for (int week = 1; week <= 21; week++)
        {
            var reps = performancePattern[(week - 1) % 4];
            var plannedSets = strategy.CalculatePlannedSets(week, (week - 1) / 7 + 1).ToList();
            var completedSets = plannedSets.Select((s, i) => new CompletedSet(
                i + 1, s.Weight, reps, wasAmrap: false)).ToList();

            var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
            strategy.ApplyPerformanceResult(performance);
        }

        // Assert - Should have handled all scenarios without exception
        strategy.CurrentSetCount.Should().BeGreaterThanOrEqualTo(1);
        strategy.CurrentSetCount.Should().BeLessThanOrEqualTo(5);
    }

    #endregion

    #region Update Methods Tests

    [Fact]
    public void UpdateWeight_ShouldChangeCurrentWeight()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable);

        var newWeight = Weight.Create(25m, WeightUnit.Kilograms);

        // Act
        strategy.UpdateWeight(newWeight);

        // Assert
        strategy.CurrentWeight.Value.Should().Be(25m);
    }

    [Fact]
    public void UpdateWeight_DifferentUnit_ShouldThrowException()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable);

        var newWeight = Weight.Create(50m, WeightUnit.Pounds);

        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() => strategy.UpdateWeight(newWeight));
    }

    [Fact]
    public void UpdateRepRange_ShouldChangeRepRange()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable);

        var newRepRange = RepRange.Create(10, 15, 20);

        // Act
        strategy.UpdateRepRange(newRepRange);

        // Assert
        strategy.RepRange.Should().Be(newRepRange);
    }

    #endregion

    #region Summary Tests

    [Fact]
    public void GetSummary_ShouldReturnCorrectDetails()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Cable,
            startingSets: 3,
            targetSets: 5);

        // Act
        var summary = strategy.GetSummary();

        // Assert
        summary.Type.Should().Be("Reps Per Set");
        summary.Details["Rep Range"].Should().Contain("8");
        summary.Details["Rep Range"].Should().Contain("15");
        summary.Details["Current Sets"].Should().Contain("3");
        summary.Details["Current Weight"].Should().Contain("20");
    }

    [Fact]
    public void GetSummary_Unilateral_ShouldIndicateType()
    {
        // Arrange
        var strategy = RepsPerSetStrategy.Create(
            _standardRepRange,
            _startingWeight,
            EquipmentType.Dumbbell,
            startingSets: 2,
            targetSets: 4,
            isUnilateral: true);

        // Act
        var summary = strategy.GetSummary();

        // Assert
        summary.Details.Should().ContainKey("Type");
        summary.Details["Type"].Should().Contain("Unilateral");
    }

    #endregion
}
