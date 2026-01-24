using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Unit tests for LinearProgressionStrategy covering all 21 weeks of the A2S program.
/// Tests verify intensity, sets, reps, and TM adjustments match the spreadsheet exactly.
/// </summary>
public class LinearProgressionStrategyTests
{
    private readonly TrainingMax _testTm = TrainingMax.Create(100m, WeightUnit.Kilograms);
    private readonly ExerciseId _testExerciseId = new(Guid.NewGuid());

    #region Block 1: Volume Accumulation (Weeks 1-7)

    [Theory]
    [InlineData(1, 0.75, 5, 10)]  // Week 1: 75%, 5 sets, 10 reps
    [InlineData(2, 0.85, 4, 8)]   // Week 2: 85%, 4 sets, 8 reps
    [InlineData(3, 0.90, 3, 6)]   // Week 3: 90%, 3 sets, 6 reps
    [InlineData(4, 0.80, 5, 9)]   // Week 4: 80%, 5 sets, 9 reps
    [InlineData(5, 0.85, 4, 7)]   // Week 5: 85%, 4 sets, 7 reps
    [InlineData(6, 0.90, 3, 5)]   // Week 6: 90%, 3 sets, 5 reps
    [InlineData(7, 0.65, 5, 10)]  // Week 7: DELOAD - 65%, 5 sets, 10 reps
    public void Block1_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedReps)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 1).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);
        foreach (var set in plannedSets)
        {
            set.Weight.Value.Should().Be(expectedWeight.Value,
                $"Week {week} weight should be {expectedIntensity * 100}% of TM");
            set.TargetReps.Should().Be(expectedReps,
                $"Week {week} should target {expectedReps} reps");
        }

        // Last set should be AMRAP (except deload week 7)
        var lastSet = plannedSets.Last();
        if (week != 7)
        {
            lastSet.IsAmrap.Should().BeTrue($"Week {week} last set should be AMRAP");
        }
    }

    [Fact]
    public void Block1_Week7_DeloadWeek_ShouldHaveReducedIntensity()
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(7, blockNumber: 1).ToList();

        // Assert - Deload at 65%
        var expectedWeight = _testTm.CalculateWorkingWeight(0.65m);
        plannedSets.First().Weight.Value.Should().Be(expectedWeight.Value);
        plannedSets.Should().HaveCount(5, "Deload week should have 5 sets");
    }

    #endregion

    #region Block 2: Intensity Building (Weeks 8-14)

    [Theory]
    [InlineData(8, 0.85, 4, 8)]   // Week 8: 85%, 4 sets, 8 reps
    [InlineData(9, 0.90, 3, 6)]   // Week 9: 90%, 3 sets, 6 reps
    [InlineData(10, 0.95, 2, 4)]  // Week 10: 95%, 2 sets, 4 reps
    [InlineData(11, 0.85, 4, 7)]  // Week 11: 85%, 4 sets, 7 reps
    [InlineData(12, 0.90, 3, 5)]  // Week 12: 90%, 3 sets, 5 reps
    [InlineData(13, 0.95, 2, 3)]  // Week 13: 95%, 2 sets, 3 reps
    [InlineData(14, 0.65, 5, 10)] // Week 14: DELOAD - 65%, 5 sets, 10 reps
    public void Block2_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedReps)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 2).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);
        foreach (var set in plannedSets)
        {
            set.Weight.Value.Should().Be(expectedWeight.Value,
                $"Week {week} weight should be {expectedIntensity * 100}% of TM");
            set.TargetReps.Should().Be(expectedReps,
                $"Week {week} should target {expectedReps} reps");
        }
    }

    [Fact]
    public void Block2_Week10_HighIntensity_ShouldHaveOnly2Sets()
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(10, blockNumber: 2).ToList();

        // Assert - 95% intensity with only 2 sets
        plannedSets.Should().HaveCount(2);
        var expectedWeight = _testTm.CalculateWorkingWeight(0.95m);
        plannedSets.First().Weight.Value.Should().Be(expectedWeight.Value);
        plannedSets.First().TargetReps.Should().Be(4);
    }

    #endregion

    #region Block 3: Peak/Realization (Weeks 15-21)

    [Theory]
    [InlineData(15, 0.90, 3, 6)]  // Week 15: 90%, 3 sets, 6 reps
    [InlineData(16, 0.95, 2, 4)]  // Week 16: 95%, 2 sets, 4 reps
    [InlineData(17, 1.00, 1, 2)]  // Week 17: 100%, 1 set, 2 reps
    [InlineData(18, 0.95, 2, 4)]  // Week 18: 95%, 2 sets, 4 reps
    [InlineData(19, 1.00, 1, 2)]  // Week 19: 100%, 1 set, 2 reps
    [InlineData(20, 1.05, 1, 2)]  // Week 20: 105% PEAK, 1 set, 2 reps
    [InlineData(21, 0.65, 5, 10)] // Week 21: DELOAD - 65%, 5 sets, 10 reps
    public void Block3_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedReps)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 3).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);
        foreach (var set in plannedSets)
        {
            set.Weight.Value.Should().Be(expectedWeight.Value,
                $"Week {week} weight should be {expectedIntensity * 100}% of TM");
            set.TargetReps.Should().Be(expectedReps,
                $"Week {week} should target {expectedReps} reps");
        }
    }

    [Fact]
    public void Block3_Week20_PeakWeek_ShouldExceedTrainingMax()
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(20, blockNumber: 3).ToList();

        // Assert - 105% intensity (supramaximal)
        plannedSets.Should().HaveCount(1, "Peak week should have only 1 set");
        var expectedWeight = _testTm.CalculateWorkingWeight(1.05m);
        plannedSets.First().Weight.Value.Should().Be(expectedWeight.Value);
        plannedSets.First().TargetReps.Should().Be(2);
        plannedSets.First().IsAmrap.Should().BeTrue();
    }

    #endregion

    #region TM Adjustment Tests (RTF Progression)

    [Theory]
    [InlineData(5, 0.03)]   // +5 or more: +3.0%
    [InlineData(6, 0.03)]   // +6: still +3.0%
    [InlineData(10, 0.03)]  // +10: still +3.0%
    [InlineData(4, 0.02)]   // +4: +2.0%
    [InlineData(3, 0.015)]  // +3: +1.5%
    [InlineData(2, 0.01)]   // +2: +1.0%
    [InlineData(1, 0.005)]  // +1: +0.5%
    [InlineData(0, 0)]      // 0: no change
    [InlineData(-1, -0.02)] // -1: -2.0%
    [InlineData(-2, -0.05)] // -2: -5.0%
    [InlineData(-3, -0.05)] // -3 or worse: -5.0%
    [InlineData(-5, -0.05)] // -5: still -5.0%
    public void ApplyPerformanceResult_WithAmrapDelta_ShouldAdjustTmCorrectly(
        int amrapDelta, decimal expectedAdjustmentPercent)
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        // Get planned sets for week 1 (target = 10 reps)
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var targetReps = 10;
        var actualReps = targetReps + amrapDelta;

        // Create completed sets matching planned, with AMRAP result
        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1,
                plannedSets[i].Weight,
                targetReps,
                wasAmrap: false));
        }
        // Last set is AMRAP with the delta
        completedSets.Add(new CompletedSet(
            plannedSets.Count,
            plannedSets.Last().Weight,
            Math.Max(1, actualReps), // Ensure at least 1 rep
            wasAmrap: true));

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        var expectedTm = 100m * (1 + expectedAdjustmentPercent);
        // Account for rounding to nearest 2.5kg
        var roundedExpectedTm = Math.Round(expectedTm / 2.5m) * 2.5m;
        strategy.TrainingMax.Value.Should().Be(roundedExpectedTm,
            $"AMRAP delta {amrapDelta} should result in {expectedAdjustmentPercent * 100}% TM adjustment");
    }

    [Fact]
    public void ApplyPerformanceResult_WithoutAmrap_ShouldNotChangeTm()
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: false);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1,
            s.Weight,
            s.TargetReps,
            wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert
        strategy.TrainingMax.Value.Should().Be(100m, "TM should not change without AMRAP");
    }

    #endregion

    #region GetSetsForWeek Static Method Tests

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 4)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 4)]
    [InlineData(6, 3)]
    [InlineData(7, 5)]   // Deload
    [InlineData(8, 4)]
    [InlineData(9, 3)]
    [InlineData(10, 2)]
    [InlineData(11, 4)]
    [InlineData(12, 3)]
    [InlineData(13, 2)]
    [InlineData(14, 5)]  // Deload
    [InlineData(15, 3)]
    [InlineData(16, 2)]
    [InlineData(17, 1)]
    [InlineData(18, 2)]
    [InlineData(19, 1)]
    [InlineData(20, 1)]  // Peak
    [InlineData(21, 5)]  // Final Deload
    public void GetSetsForWeek_AllWeeks_ShouldMatchSpreadsheet(int week, int expectedSets)
    {
        // Act
        var sets = LinearProgressionStrategy.GetSetsForWeek(week);

        // Assert
        sets.Should().Be(expectedSets, $"Week {week} should have {expectedSets} sets");
    }

    [Fact]
    public void GetSetsForWeek_InvalidWeek_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => LinearProgressionStrategy.GetSetsForWeek(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LinearProgressionStrategy.GetSetsForWeek(22));
    }

    #endregion

    #region Full 21-Week Cycle Simulation

    [Fact]
    public void Full21WeekCycle_WithConsistentPerformance_ShouldProgressTm()
    {
        // Arrange
        var startingTm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(startingTm, useAmrap: true);

        var tmHistory = new List<(int Week, decimal TmValue)>
        {
            (0, 100m) // Starting point
        };

        // Act - Simulate 21 weeks with +5 AMRAP delta (except deload weeks)
        // Using +5 delta (+3%) to ensure rounding to nearest 2.5kg shows progression
        // Note: +2 delta (+1%) would round 101kg back to 100kg due to 2.5kg increments
        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();
            var targetReps = plannedSets.First().TargetReps;

            if (!isDeload)
            {
                // Create performance with +5 AMRAP delta (+3% TM adjustment)
                var completedSets = new List<CompletedSet>();
                for (int i = 0; i < plannedSets.Count - 1; i++)
                {
                    completedSets.Add(new CompletedSet(
                        i + 1, plannedSets[i].Weight, targetReps, wasAmrap: false));
                }
                completedSets.Add(new CompletedSet(
                    plannedSets.Count,
                    plannedSets.Last().Weight,
                    targetReps + 5, // +5 AMRAP delta = +3% TM
                    wasAmrap: true));

                var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
                strategy.ApplyPerformanceResult(performance);
            }

            tmHistory.Add((week, strategy.TrainingMax.Value));
        }

        // Assert
        var finalTm = strategy.TrainingMax.Value;

        // With +5 AMRAP delta (+3% each non-deload week), expect significant increase
        // 18 training weeks * ~3% = ~54% increase, but rounding reduces this
        // At minimum, we should see some progression
        finalTm.Should().BeGreaterThan(100m, "TM should increase over 21 weeks with consistent +5 AMRAP delta");

        // Verify deload weeks didn't change TM
        tmHistory[7].TmValue.Should().Be(tmHistory[6].TmValue, "Week 7 deload should not change TM");
        tmHistory[14].TmValue.Should().Be(tmHistory[13].TmValue, "Week 14 deload should not change TM");
        tmHistory[21].TmValue.Should().Be(tmHistory[20].TmValue, "Week 21 deload should not change TM");
    }

    [Fact]
    public void Full21WeekCycle_WithMixedPerformance_ShouldAdjustAppropriately()
    {
        // Arrange
        var startingTm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(startingTm, useAmrap: true);

        // Simulate realistic performance pattern with higher deltas to overcome 2.5kg rounding
        // Small deltas (+1, +2) get rounded away when TM is 100kg, so using larger deltas
        var weeklyDeltas = new[]
        {
            // Block 1: Learning phase - varied (using higher deltas)
            +5, +4, +3, +4, +5, +4, 0, // Week 7 is deload
            // Block 2: Building - mostly good
            +4, +4, +3, +4, +3, +2, 0, // Week 14 is deload
            // Block 3: Peak - challenging (lower but still positive)
            +3, +2, -1, +3, +2, +4, 0  // Week 21 is deload
        };

        // Act
        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;
            var delta = weeklyDeltas[week - 1];

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

            if (!isDeload && delta != 0) // Only apply performance for non-deload, non-zero delta
            {
                var targetReps = plannedSets.First().TargetReps;
                var completedSets = new List<CompletedSet>();
                for (int i = 0; i < plannedSets.Count - 1; i++)
                {
                    completedSets.Add(new CompletedSet(
                        i + 1, plannedSets[i].Weight, targetReps, wasAmrap: false));
                }
                completedSets.Add(new CompletedSet(
                    plannedSets.Count,
                    plannedSets.Last().Weight,
                    Math.Max(1, targetReps + delta),
                    wasAmrap: true));

                var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
                strategy.ApplyPerformanceResult(performance);
            }
        }

        // Assert
        var finalTm = strategy.TrainingMax.Value;

        // With mostly positive deltas and some negatives, expect net increase
        // Due to 2.5kg rounding, small increases may be lost, but larger ones should accumulate
        finalTm.Should().BeGreaterThan(100m, "TM should increase with mostly positive deltas");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculatePlannedSets_InvalidWeek_ShouldThrowException()
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act & Assert - CheckRule throws ArgumentException
        Assert.Throws<ArgumentException>(() => strategy.CalculatePlannedSets(0, 1));
        Assert.Throws<ArgumentException>(() => strategy.CalculatePlannedSets(22, 1));
    }

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Act
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true, baseSetsPerExercise: 5);

        // Assert
        strategy.Should().NotBeNull();
        strategy.TrainingMax.Should().Be(_testTm);
        strategy.UseAmrap.Should().BeTrue();
        strategy.BaseSetsPerExercise.Should().Be(5);
    }

    [Fact]
    public void UpdateTrainingMax_ShouldChangeTrainingMax()
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var newTm = TrainingMax.Create(110m, WeightUnit.Kilograms);

        // Act
        strategy.UpdateTrainingMax(newTm, "Manual adjustment");

        // Assert
        strategy.TrainingMax.Value.Should().Be(110m);
    }

    #endregion
}
