using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Unit tests for MinimalSetsStrategy covering bodyweight/assisted exercise progression.
/// Tests verify set efficiency progression and failure handling.
///
/// Based on spreadsheet data:
/// - Assisted Dips: -32kg assistance, 3 sets, 40 total reps
/// - Assisted Pullups: -32kg assistance, 6 sets, 40 total reps
/// </summary>
public class MinimalSetsStrategyTests
{
    private readonly ExerciseId _testExerciseId = new(Guid.NewGuid());
    private readonly Weight _assistedWeight = Weight.Create(32m, WeightUnit.Kilograms); // Assistance weight

    #region Creation Tests

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Act
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        // Assert
        strategy.Should().NotBeNull();
        strategy.CurrentWeight.Value.Should().Be(32m);
        strategy.TargetTotalReps.Should().Be(40);
        strategy.CurrentSetCount.Should().Be(3);
        strategy.StartingSets.Should().Be(3);
        strategy.MinimumSets.Should().Be(2);
        strategy.MaximumSets.Should().Be(8);
    }

    [Fact]
    public void Create_WithDefaultBounds_ShouldUseDefaultValues()
    {
        // Act
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine);

        // Assert
        strategy.MinimumSets.Should().Be(2);
        strategy.MaximumSets.Should().Be(10);
    }

    [Theory]
    [InlineData(9)]   // Below minimum (10)
    [InlineData(201)] // Above maximum (200)
    public void Create_InvalidTargetReps_ShouldThrowException(int targetReps)
    {
        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            MinimalSetsStrategy.Create(
                _assistedWeight,
                targetTotalReps: targetReps,
                startingSets: 3,
                equipment: EquipmentType.Machine));
    }

    [Fact]
    public void Create_MinimumSetsGreaterThanStarting_ShouldThrowException()
    {
        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            MinimalSetsStrategy.Create(
                _assistedWeight,
                targetTotalReps: 40,
                startingSets: 3,
                equipment: EquipmentType.Machine,
                minimumSets: 4, // Greater than startingSets
                maximumSets: 8));
    }

    #endregion

    #region Planned Sets Tests

    [Fact]
    public void CalculatePlannedSets_ShouldDistributeRepsEvenly()
    {
        // Arrange - 40 reps / 3 sets = 13, 13, 14
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Assert
        plannedSets.Should().HaveCount(3);

        // Total should equal 40
        var totalReps = plannedSets.Sum(s => s.TargetReps);
        totalReps.Should().Be(40);

        // Earlier sets get extra reps (40/3 = 13 remainder 1)
        plannedSets[0].TargetReps.Should().Be(14); // Gets the remainder
        plannedSets[1].TargetReps.Should().Be(13);
        plannedSets[2].TargetReps.Should().Be(13);
    }

    [Fact]
    public void CalculatePlannedSets_EvenDistribution_ShouldBeEqual()
    {
        // Arrange - 40 reps / 4 sets = 10 each
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Assert
        plannedSets.Should().HaveCount(4);
        plannedSets.All(s => s.TargetReps == 10).Should().BeTrue();
    }

    [Fact]
    public void CalculatePlannedSets_ShouldNeverBeAmrap()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Assert
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue("MinimalSets should never have AMRAP");
    }

    #endregion

    #region Success Progression Tests

    [Fact]
    public void ApplyPerformanceResult_CompletedInFewerSets_ShouldReduceSetCount()
    {
        // Arrange - Start with 4 sets
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Complete 40 reps in only 3 sets (fewer than planned 4)
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 15, false),
            new(2, plannedSets[1].Weight, 15, false),
            new(3, plannedSets[2].Weight, 10, false)
            // Only 3 sets used, total = 40
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(3, "Should reduce sets when completed in fewer");
    }

    [Fact]
    public void ApplyPerformanceResult_AtMinimumSets_ShouldNotReduceFurther()
    {
        // Arrange - Already at minimum (2 sets)
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 2,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Complete 40 reps in only 1 set (amazing performance!)
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 40, false)
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(2, "Cannot go below minimum sets");
    }

    #endregion

    #region Maintained Tests

    [Fact]
    public void ApplyPerformanceResult_CompletedInExactSets_ShouldMaintain()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Complete exactly 40 reps in exactly 4 sets
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 10, false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(4, "Sets should not change when hitting target exactly");
    }

    [Fact]
    public void ApplyPerformanceResult_ExceededRepsInSameSets_ShouldMaintain()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Complete MORE than 40 reps but still using 4 sets
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 12, false)).ToList(); // 12 * 4 = 48 > 40

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - Still used all 4 sets, so maintained (not success)
        strategy.CurrentSetCount.Should().Be(4);
    }

    #endregion

    #region Failure Tests

    [Fact]
    public void ApplyPerformanceResult_DidNotCompleteTargetReps_ShouldAddSet()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Only complete 35 reps (below 40 target)
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 12, false),
            new(2, plannedSets[1].Weight, 12, false),
            new(3, plannedSets[2].Weight, 11, false) // Total = 35
        };

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(4, "Should add a set when target not met");
    }

    [Fact]
    public void ApplyPerformanceResult_AtMaximumSets_ShouldNotAddMore()
    {
        // Arrange - Already at maximum (8 sets)
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 8,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        // Only complete 30 reps (below target)
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1, s.Weight, 3, false)).ToList(); // 3 * 8 = 24 < 40

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.CurrentSetCount.Should().Be(8, "Cannot exceed maximum sets");
    }

    #endregion

    #region Full 21-Week Progression Simulation

    [Fact]
    public void Full21WeekCycle_AssistedDips_ShouldProgressCorrectly()
    {
        // Arrange - Assisted Dips from spreadsheet: -32kg, 3 sets, 40 reps
        var strategy = MinimalSetsStrategy.Create(
            Weight.Create(32m, WeightUnit.Kilograms),
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 6);

        var setHistory = new List<(int Week, int Sets)>
        {
            (0, 3) // Starting point
        };

        // Simulate 21 weeks with improving efficiency
        // Every 3rd week: success (fewer sets)
        // Every 5th week: failure (more sets needed)
        // Otherwise: maintained
        for (int week = 1; week <= 21; week++)
        {
            var plannedSets = strategy.CalculatePlannedSets(week, (week - 1) / 7 + 1).ToList();
            var currentSets = strategy.CurrentSetCount;

            List<CompletedSet> completedSets;
            if (week % 3 == 0 && currentSets > 2) // Success every 3rd week
            {
                // Complete target in fewer sets
                completedSets = new List<CompletedSet>();
                int repsRemaining = 40;
                for (int i = 1; i < currentSets && repsRemaining > 0; i++)
                {
                    int reps = Math.Min(repsRemaining, 20);
                    completedSets.Add(new CompletedSet(i, plannedSets[0].Weight, reps, false));
                    repsRemaining -= reps;
                }
            }
            else if (week % 5 == 0) // Failure every 5th week
            {
                // Don't complete target reps
                completedSets = plannedSets.Select((s, i) => new CompletedSet(
                    i + 1, s.Weight, 8, false)).ToList(); // Only ~24-32 reps
            }
            else // Maintained
            {
                // Complete target in exact number of sets
                completedSets = plannedSets.Select((s, i) => new CompletedSet(
                    i + 1, s.Weight, s.TargetReps, false)).ToList();
            }

            var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
            strategy.ApplyPerformanceResult(performance);

            setHistory.Add((week, strategy.CurrentSetCount));
        }

        // Assert
        strategy.CurrentSetCount.Should().BeGreaterThanOrEqualTo(2);
        strategy.CurrentSetCount.Should().BeLessThanOrEqualTo(6);

        // Should have some variation over 21 weeks
        var uniqueSetCounts = setHistory.Select(h => h.Sets).Distinct().Count();
        uniqueSetCounts.Should().BeGreaterThan(1, "Set count should vary over time");
    }

    [Fact]
    public void Full21WeekCycle_ConsistentImprovement_ShouldReachMinimum()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 5,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        // Act - Always complete in fewer sets than required (consistent improvement)
        for (int week = 1; week <= 21; week++)
        {
            var plannedSets = strategy.CalculatePlannedSets(week, 1).ToList();
            var currentSets = strategy.CurrentSetCount;

            // Always complete in 1 fewer set
            var fewerSets = Math.Max(1, currentSets - 1);
            var completedSets = new List<CompletedSet>();
            int repsRemaining = 40;
            for (int i = 1; i <= fewerSets; i++)
            {
                int reps = (int)Math.Ceiling((double)repsRemaining / (fewerSets - i + 1));
                completedSets.Add(new CompletedSet(i, plannedSets[0].Weight, reps, false));
                repsRemaining -= reps;
            }

            var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
            strategy.ApplyPerformanceResult(performance);
        }

        // Assert - Should reach minimum sets with consistent improvement
        strategy.CurrentSetCount.Should().Be(2, "Should reach minimum with consistent improvement");
    }

    #endregion

    #region Update Methods Tests

    [Fact]
    public void UpdateWeight_ShouldChangeCurrentWeight()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        var newWeight = Weight.Create(28m, WeightUnit.Kilograms); // Reduced assistance

        // Act
        strategy.UpdateWeight(newWeight);

        // Assert
        strategy.CurrentWeight.Value.Should().Be(28m);
    }

    [Fact]
    public void UpdateWeight_DifferentUnit_ShouldThrowException()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        var newWeight = Weight.Create(60m, WeightUnit.Pounds);

        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() => strategy.UpdateWeight(newWeight));
    }

    [Fact]
    public void UpdateTargetTotalReps_ShouldChangeTarget()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        // Act
        strategy.UpdateTargetTotalReps(50);

        // Assert
        strategy.TargetTotalReps.Should().Be(50);
    }

    [Fact]
    public void UpdateTargetTotalReps_InvalidValue_ShouldThrowException()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() => strategy.UpdateTargetTotalReps(5)); // Below 10
        Assert.Throws<ArgumentException>(() => strategy.UpdateTargetTotalReps(250)); // Above 200
    }

    [Fact]
    public void ResetSetCount_ShouldReturnToStartingSets()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 4,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        // Progress to a different set count
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = new List<CompletedSet>
        {
            new(1, plannedSets[0].Weight, 20, false),
            new(2, plannedSets[1].Weight, 20, false)
        };
        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
        strategy.ApplyPerformanceResult(performance);

        strategy.CurrentSetCount.Should().Be(3, "Should have reduced after success");

        // Act
        strategy.ResetSetCount();

        // Assert
        strategy.CurrentSetCount.Should().Be(4, "Should reset to starting sets");
    }

    #endregion

    #region Summary Tests

    [Fact]
    public void GetSummary_ShouldReturnCorrectDetails()
    {
        // Arrange
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine,
            minimumSets: 2,
            maximumSets: 8);

        // Act
        var summary = strategy.GetSummary();

        // Assert
        summary.Type.Should().Be("Minimal Sets");
        summary.Details["Target Total Reps"].Should().Be("40");
        summary.Details["Current Sets"].Should().Be("3");
        summary.Details["Set Range"].Should().Be("2-8");
        summary.Details["Current Weight"].Should().Contain("32");
    }

    #endregion

    #region Block-Specific Tests (Per Spreadsheet)

    [Theory]
    [InlineData(1, 3)]  // Block 1: Starting
    [InlineData(7, 3)]  // Block 1: Deload (no change for MinimalSets)
    [InlineData(8, 3)]  // Block 2: Start
    [InlineData(14, 3)] // Block 2: Deload
    [InlineData(15, 3)] // Block 3: Start
    [InlineData(21, 3)] // Block 3: Final deload
    public void CalculatePlannedSets_AllBlocks_ShouldUseCurrentState(int week, int expectedSets)
    {
        // Arrange - MinimalSets doesn't vary by week (unlike Linear)
        var strategy = MinimalSetsStrategy.Create(
            _assistedWeight,
            targetTotalReps: 40,
            startingSets: 3,
            equipment: EquipmentType.Machine);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, (week - 1) / 7 + 1).ToList();

        // Assert - MinimalSets uses current state, not week-based periodization
        plannedSets.Should().HaveCount(expectedSets);
    }

    #endregion
}
