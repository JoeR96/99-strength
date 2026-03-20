using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Unit tests for LinearProgressionStrategy covering all 21 weeks of the A2S Hypertrophy program.
/// Tests verify intensity, sets, reps, and TM adjustments match the spreadsheet exactly.
///
/// Hypertrophy table:
///   6 intensity levels: a=0.65, b=0.68, c=0.70, d=0.73, e=0.76, f=0.79
///   Always 4 sets. Deload at 0.60, 5 reps, no AMRAP.
///   Rep-out target = RepsPerSet + 2 (week 1 = +3).
///   TM stored at 2dp precision (NOT rounded to gym increments).
/// </summary>
public class LinearProgressionStrategyTests
{
    private readonly TrainingMax _testTm = TrainingMax.Create(100m, WeightUnit.Kilograms);
    private readonly ExerciseId _testExerciseId = new(Guid.NewGuid());

    #region Block 1: Weeks 1-7

    [Theory]
    [InlineData(1, 0.65, 4, 12, 15)]  // Week 1: 65%, 4 sets, 12 reps, rep-out 15
    [InlineData(2, 0.68, 4, 11, 13)]  // Week 2: 68%, 4 sets, 11 reps, rep-out 13
    [InlineData(3, 0.70, 4, 10, 12)]  // Week 3: 70%, 4 sets, 10 reps, rep-out 12
    [InlineData(4, 0.68, 4, 11, 13)]  // Week 4: 68%, 4 sets, 11 reps, rep-out 13
    [InlineData(5, 0.70, 4, 10, 12)]  // Week 5: 70%, 4 sets, 10 reps, rep-out 12
    [InlineData(6, 0.73, 4,  9, 11)]  // Week 6: 73%, 4 sets,  9 reps, rep-out 11
    [InlineData(7, 0.60, 4,  5,  0)]  // Week 7: DELOAD - 60%, 4 sets, 5 reps, no AMRAP
    public void Block1_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedRepsPerSet, int expectedRepOutTarget)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 1).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);

        if (week == 7) // Deload - no AMRAP, all sets use RepsPerSet
        {
            foreach (var set in plannedSets)
            {
                set.Weight.Value.Should().Be(expectedWeight.Value);
                set.TargetReps.Should().Be(expectedRepsPerSet);
                set.IsAmrap.Should().BeFalse($"Deload week {week} should have no AMRAP");
            }
        }
        else
        {
            // Normal sets (1-3): RepsPerSet
            for (int i = 0; i < plannedSets.Count - 1; i++)
            {
                plannedSets[i].Weight.Value.Should().Be(expectedWeight.Value);
                plannedSets[i].TargetReps.Should().Be(expectedRepsPerSet,
                    $"Week {week} normal set {i + 1} should target {expectedRepsPerSet} reps");
                plannedSets[i].IsAmrap.Should().BeFalse();
            }
            // Last set (AMRAP): RepOutTarget
            var lastSet = plannedSets.Last();
            lastSet.Weight.Value.Should().Be(expectedWeight.Value);
            lastSet.TargetReps.Should().Be(expectedRepOutTarget,
                $"Week {week} AMRAP set should target rep-out {expectedRepOutTarget}");
            lastSet.IsAmrap.Should().BeTrue($"Week {week} last set should be AMRAP");
        }
    }

    [Fact]
    public void Block1_Week7_DeloadWeek_ShouldHaveReducedIntensity()
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var plannedSets = strategy.CalculatePlannedSets(7, blockNumber: 1).ToList();

        var expectedWeight = _testTm.CalculateWorkingWeight(0.60m);
        plannedSets.First().Weight.Value.Should().Be(expectedWeight.Value);
        plannedSets.Should().HaveCount(4, "Deload week should have 4 sets");
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue("Deload sets should not be AMRAP");
        plannedSets.All(s => s.TargetReps == 5).Should().BeTrue("Deload reps should be 5");
    }

    #endregion

    #region Block 2: Weeks 8-14

    [Theory]
    [InlineData(8,  0.68, 4, 11, 13)]
    [InlineData(9,  0.70, 4, 10, 12)]
    [InlineData(10, 0.73, 4,  9, 11)]
    [InlineData(11, 0.70, 4, 10, 12)]
    [InlineData(12, 0.73, 4,  9, 11)]
    [InlineData(13, 0.76, 4,  8, 10)]
    [InlineData(14, 0.60, 4,  5,  0)]  // DELOAD
    public void Block2_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedRepsPerSet, int expectedRepOutTarget)
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 2).ToList();

        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);
        var isDeload = week == 14;

        if (isDeload)
        {
            foreach (var set in plannedSets)
            {
                set.Weight.Value.Should().Be(expectedWeight.Value);
                set.TargetReps.Should().Be(expectedRepsPerSet);
                set.IsAmrap.Should().BeFalse();
            }
        }
        else
        {
            for (int i = 0; i < plannedSets.Count - 1; i++)
            {
                plannedSets[i].TargetReps.Should().Be(expectedRepsPerSet);
                plannedSets[i].IsAmrap.Should().BeFalse();
            }
            plannedSets.Last().TargetReps.Should().Be(expectedRepOutTarget);
            plannedSets.Last().IsAmrap.Should().BeTrue();
        }
    }

    #endregion

    #region Block 3: Weeks 15-21

    [Theory]
    [InlineData(15, 0.70, 4, 10, 12)]
    [InlineData(16, 0.73, 4,  9, 11)]
    [InlineData(17, 0.76, 4,  8, 10)]
    [InlineData(18, 0.73, 4,  9, 11)]
    [InlineData(19, 0.76, 4,  8, 10)]
    [InlineData(20, 0.79, 4,  7,  9)]
    [InlineData(21, 0.60, 4,  5,  0)]  // DELOAD
    public void Block3_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, decimal expectedIntensity, int expectedSets, int expectedRepsPerSet, int expectedRepOutTarget)
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: 3).ToList();

        plannedSets.Should().HaveCount(expectedSets, $"Week {week} should have {expectedSets} sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(expectedIntensity);
        var isDeload = week == 21;

        if (isDeload)
        {
            foreach (var set in plannedSets)
            {
                set.Weight.Value.Should().Be(expectedWeight.Value);
                set.TargetReps.Should().Be(expectedRepsPerSet);
                set.IsAmrap.Should().BeFalse();
            }
        }
        else
        {
            for (int i = 0; i < plannedSets.Count - 1; i++)
            {
                plannedSets[i].TargetReps.Should().Be(expectedRepsPerSet);
                plannedSets[i].IsAmrap.Should().BeFalse();
            }
            plannedSets.Last().TargetReps.Should().Be(expectedRepOutTarget);
            plannedSets.Last().IsAmrap.Should().BeTrue();
        }
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

        // Week 1: RepsPerSet=12, RepOutTarget=15. AMRAP set targets 15.
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var repOutTarget = 15; // Week 1 rep-out target
        var actualReps = repOutTarget + amrapDelta;

        // Normal sets complete at RepsPerSet (12)
        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1,
                plannedSets[i].Weight,
                12, // RepsPerSet for normal sets
                wasAmrap: false));
        }
        // Last set is AMRAP with the delta applied to rep-out target
        completedSets.Add(new CompletedSet(
            plannedSets.Count,
            plannedSets.Last().Weight,
            Math.Max(1, actualReps),
            wasAmrap: true));

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert - TM stored at 2dp precision, NOT rounded to 2.5kg
        var expectedTm = Math.Round(100m * (1 + expectedAdjustmentPercent), 2);
        strategy.TrainingMax.Value.Should().Be(expectedTm,
            $"AMRAP delta {amrapDelta} should result in {expectedAdjustmentPercent * 100}% TM adjustment");
    }

    [Fact]
    public void ApplyPerformanceResult_WithoutAmrap_ShouldNotChangeTm()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: false);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completedSets = plannedSets.Select((s, i) => new CompletedSet(
            i + 1,
            s.Weight,
            s.TargetReps,
            wasAmrap: false)).ToList();

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        strategy.ApplyPerformanceResult(performance);

        strategy.TrainingMax.Value.Should().Be(100m, "TM should not change without AMRAP");
    }

    #endregion

    #region GetSetsForWeek Static Method Tests

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 4)]   // Deload
    [InlineData(8, 4)]
    [InlineData(9, 4)]
    [InlineData(10, 4)]
    [InlineData(11, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 4)]
    [InlineData(14, 4)]  // Deload
    [InlineData(15, 4)]
    [InlineData(16, 4)]
    [InlineData(17, 4)]
    [InlineData(18, 4)]
    [InlineData(19, 4)]
    [InlineData(20, 4)]
    [InlineData(21, 4)]  // Final Deload
    public void GetSetsForWeek_AllWeeks_ShouldMatchSpreadsheet(int week, int expectedSets)
    {
        var sets = LinearProgressionStrategy.GetSetsForWeek(week);
        sets.Should().Be(expectedSets, $"Week {week} should have {expectedSets} sets (hypertrophy always 4)");
    }

    [Fact]
    public void GetSetsForWeek_InvalidWeek_ShouldThrowException()
    {
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
            (0, 100m)
        };

        // Act - Simulate 21 weeks with +5 AMRAP delta (+3%) on non-deload weeks
        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

            if (!isDeload)
            {
                var repsPerSet = plannedSets.First().TargetReps; // Normal set reps
                var repOutTarget = plannedSets.Last().TargetReps; // AMRAP set rep-out target

                var completedSets = new List<CompletedSet>();
                for (int i = 0; i < plannedSets.Count - 1; i++)
                {
                    completedSets.Add(new CompletedSet(
                        i + 1, plannedSets[i].Weight, repsPerSet, wasAmrap: false));
                }
                completedSets.Add(new CompletedSet(
                    plannedSets.Count,
                    plannedSets.Last().Weight,
                    repOutTarget + 5, // +5 AMRAP delta = +3% TM
                    wasAmrap: true));

                var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
                strategy.ApplyPerformanceResult(performance);
            }

            tmHistory.Add((week, strategy.TrainingMax.Value));
        }

        // Assert
        var finalTm = strategy.TrainingMax.Value;
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

        // Deltas for each week (deload weeks have 0 but won't be applied)
        var weeklyDeltas = new[]
        {
            // Block 1: Strong start, plateau mid-block, failure, recovery
            +5, +4, +3, 0, -1, +5, 0, // Week 7 is deload
            // Block 2: Good recovery, big failure, strong finish
            +4, +3, +5, -2, +4, +5, 0, // Week 14 is deload
            // Block 3: Moderate gains, plateau, failure, recovery
            +3, +4, 0, +5, -1, +4, 0   // Week 21 is deload
        };

        // Pre-calculated expected TM after each week with 2dp precision (no gym-increment rounding):
        // TM_new = Math.Round(TM_old * (1 + adjustment%), 2)
        var expectedTmPerWeek = new[]
        {
            0m,       // placeholder (1-indexed)
            103.00m,  // Week 1:  100 * 1.03 = 103.00
            105.06m,  // Week 2:  103.00 * 1.02 = 105.06
            106.64m,  // Week 3:  105.06 * 1.015 = 106.6359 -> 106.64
            106.64m,  // Week 4:  0 delta -> no change
            104.51m,  // Week 5:  106.64 * 0.98 = 104.5072 -> 104.51
            107.65m,  // Week 6:  104.51 * 1.03 = 107.6453 -> 107.65
            107.65m,  // Week 7:  DELOAD -> no change
            109.80m,  // Week 8:  107.65 * 1.02 = 109.803 -> 109.80
            111.45m,  // Week 9:  109.80 * 1.015 = 111.447 -> 111.45
            114.79m,  // Week 10: 111.45 * 1.03 = 114.7935 -> 114.79
            109.05m,  // Week 11: 114.79 * 0.95 = 109.0505 -> 109.05
            111.23m,  // Week 12: 109.05 * 1.02 = 111.231 -> 111.23
            114.57m,  // Week 13: 111.23 * 1.03 = 114.5669 -> 114.57
            114.57m,  // Week 14: DELOAD -> no change
            116.29m,  // Week 15: 114.57 * 1.015 = 116.2886 -> 116.29
            118.62m,  // Week 16: 116.29 * 1.02 = 118.6158 -> 118.62
            118.62m,  // Week 17: 0 delta -> no change
            122.18m,  // Week 18: 118.62 * 1.03 = 122.1786 -> 122.18
            119.74m,  // Week 19: 122.18 * 0.98 = 119.7364 -> 119.74
            122.13m,  // Week 20: 119.74 * 1.02 = 122.1348 -> 122.13
            122.13m   // Week 21: DELOAD -> no change
        };

        // Act & Assert - verify TM after every single week
        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;
            var delta = weeklyDeltas[week - 1];

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

            if (!isDeload && delta != 0)
            {
                var repsPerSet = plannedSets.First().TargetReps;
                var repOutTarget = plannedSets.Last().TargetReps; // AMRAP set has rep-out target

                var completedSets = new List<CompletedSet>();
                for (int i = 0; i < plannedSets.Count - 1; i++)
                {
                    completedSets.Add(new CompletedSet(
                        i + 1, plannedSets[i].Weight, repsPerSet, wasAmrap: false));
                }
                completedSets.Add(new CompletedSet(
                    plannedSets.Count,
                    plannedSets.Last().Weight,
                    Math.Max(1, repOutTarget + delta),
                    wasAmrap: true));

                var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
                strategy.ApplyPerformanceResult(performance);
            }

            strategy.TrainingMax.Value.Should().Be(expectedTmPerWeek[week],
                $"Week {week} (Block {blockNumber}): delta={delta}, deload={isDeload} -> TM should be {expectedTmPerWeek[week]}kg");
        }

        strategy.TrainingMax.Value.Should().Be(122.13m, "Final TM after 21 weeks");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculatePlannedSets_InvalidWeek_ShouldThrowException()
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        Assert.Throws<ArgumentException>(() => strategy.CalculatePlannedSets(0, 1));
        Assert.Throws<ArgumentException>(() => strategy.CalculatePlannedSets(22, 1));
    }

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true, baseSetsPerExercise: 5);

        strategy.Should().NotBeNull();
        strategy.TrainingMax.Should().Be(_testTm);
        strategy.UseAmrap.Should().BeTrue();
        strategy.BaseSetsPerExercise.Should().Be(5);
    }

    [Fact]
    public void UpdateTrainingMax_ShouldChangeTrainingMax()
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var newTm = TrainingMax.Create(110m, WeightUnit.Kilograms);

        strategy.UpdateTrainingMax(newTm, "Manual adjustment");

        strategy.TrainingMax.Value.Should().Be(110m);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void ApplyPerformanceResult_SmallTmIncrease_StoredAt2dp()
    {
        // +2 delta gives +1% adjustment: 100 * 1.01 = 101.00
        // TM stored at 2dp (NOT rounded to 2.5kg gym increments)
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var repOutTarget = 15; // Week 1 rep-out target

        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1, plannedSets[i].Weight, 12, wasAmrap: false));
        }
        // +2 delta: actual = 15 + 2 = 17
        completedSets.Add(new CompletedSet(
            plannedSets.Count,
            plannedSets.Last().Weight,
            repOutTarget + 2,
            wasAmrap: true));

        var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);

        strategy.ApplyPerformanceResult(performance);

        // 100 * 1.01 = 101.00 stored at 2dp
        strategy.TrainingMax.Value.Should().Be(101.00m,
            "101kg stored at 2dp precision, NOT rounded to gym increments");
    }

    [Fact]
    public void ApplyPerformanceResult_ConsecutiveFailures_TmDecreasesCumulatively()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        // First failure: -2 delta = -5%
        var plannedSets1 = strategy.CalculatePlannedSets(1, 1).ToList();
        var repOutTarget1 = 15; // Week 1

        var completedSets1 = new List<CompletedSet>();
        for (int i = 0; i < plannedSets1.Count - 1; i++)
        {
            completedSets1.Add(new CompletedSet(i + 1, plannedSets1[i].Weight, 12, wasAmrap: false));
        }
        completedSets1.Add(new CompletedSet(
            plannedSets1.Count, plannedSets1.Last().Weight,
            repOutTarget1 - 2, // -2 delta = -5%
            wasAmrap: true));

        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, plannedSets1, completedSets1));

        // After first failure: 100 * 0.95 = 95.00
        strategy.TrainingMax.Value.Should().Be(95m, "100kg * 0.95 = 95kg");

        // Second failure: -2 delta = -5% of 95
        var plannedSets2 = strategy.CalculatePlannedSets(2, 1).ToList();
        var repOutTarget2 = 13; // Week 2

        var completedSets2 = new List<CompletedSet>();
        for (int i = 0; i < plannedSets2.Count - 1; i++)
        {
            completedSets2.Add(new CompletedSet(i + 1, plannedSets2[i].Weight, 11, wasAmrap: false));
        }
        completedSets2.Add(new CompletedSet(
            plannedSets2.Count, plannedSets2.Last().Weight,
            repOutTarget2 - 2, // -2 delta = -5%
            wasAmrap: true));

        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, plannedSets2, completedSets2));

        // After second failure: 95 * 0.95 = 90.25
        strategy.TrainingMax.Value.Should().Be(90.25m,
            "95kg * 0.95 = 90.25kg (stored at 2dp)");
    }

    [Fact]
    public void ApplyPerformanceResult_WithDifferentStartingTm_AdjustsCorrectly()
    {
        // TM = 150kg, +5 delta = +3%: 150 * 1.03 = 154.50
        var tm = TrainingMax.Create(150m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var repOutTarget = 15;

        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(i + 1, plannedSets[i].Weight, 12, wasAmrap: false));
        }
        completedSets.Add(new CompletedSet(
            plannedSets.Count, plannedSets.Last().Weight,
            repOutTarget + 5,
            wasAmrap: true));

        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, plannedSets, completedSets));

        // 150 * 1.03 = 154.50
        strategy.TrainingMax.Value.Should().Be(154.50m,
            "150kg * 1.03 = 154.50kg (stored at 2dp)");
    }

    [Theory]
    [InlineData(7)]   // Block 1 deload
    [InlineData(14)]  // Block 2 deload
    [InlineData(21)]  // Block 3 deload
    public void CalculatePlannedSets_DeloadWeek_HasReducedIntensityAndVolume(int deloadWeek)
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);
        var blockNumber = (deloadWeek - 1) / 7 + 1;

        var plannedSets = strategy.CalculatePlannedSets(deloadWeek, blockNumber).ToList();

        // Deload: 60% intensity, 4 sets, 5 reps, no AMRAP
        plannedSets.Should().HaveCount(4, $"Week {deloadWeek} (deload) should have 4 sets");

        var expectedWeight = _testTm.CalculateWorkingWeight(0.60m);
        plannedSets.First().Weight.Value.Should().Be(expectedWeight.Value,
            $"Week {deloadWeek} (deload) should be at 60% intensity");
        plannedSets.First().TargetReps.Should().Be(5,
            $"Week {deloadWeek} (deload) should target 5 reps");
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue(
            $"Week {deloadWeek} (deload) should have no AMRAP sets");
    }

    #endregion

    #region Planned Sets for Hevy Sync Assertions

    [Fact]
    public void PlannedSets_ForLinearExercise_LastSetIsAmrapAllOthersNormal()
    {
        var strategy = LinearProgressionStrategy.Create(_testTm, useAmrap: true);

        // All non-deload weeks have 4 sets, AMRAP on last
        foreach (var (week, block) in new[] { (1, 1), (2, 1), (6, 1), (10, 2), (13, 2), (20, 3) })
        {
            var plannedSets = strategy.CalculatePlannedSets(week, block).ToList();

            plannedSets.Should().HaveCount(4, $"Week {week} should have 4 sets");

            // Last set must be AMRAP
            plannedSets.Last().IsAmrap.Should().BeTrue($"Week {week}: last set should be AMRAP");

            // First 3 sets must NOT be AMRAP
            for (int i = 0; i < 3; i++)
            {
                plannedSets[i].IsAmrap.Should().BeFalse($"Week {week}: set {i + 1} should NOT be AMRAP");
            }
        }
    }

    [Fact]
    public void PlannedSets_ForLinearExercise_WeightsMatchIntensityPercentOfTM()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        var testCases = new[]
        {
            (Week: 1,  Intensity: 0.65m, ExpectedWeight: 65.0m),   // 100 * 0.65 = 65
            (Week: 3,  Intensity: 0.70m, ExpectedWeight: 70.0m),   // 100 * 0.70 = 70
            (Week: 7,  Intensity: 0.60m, ExpectedWeight: 60.0m),   // Deload
            (Week: 10, Intensity: 0.73m, ExpectedWeight: 72.5m),   // 100 * 0.73 = 73 -> round to 72.5
            (Week: 13, Intensity: 0.76m, ExpectedWeight: 75.0m),   // 100 * 0.76 = 76 -> round to 75
            (Week: 20, Intensity: 0.79m, ExpectedWeight: 80.0m),   // 100 * 0.79 = 79 -> round to 80
        };

        foreach (var tc in testCases)
        {
            var block = (tc.Week - 1) / 7 + 1;
            var plannedSets = strategy.CalculatePlannedSets(tc.Week, block).ToList();

            foreach (var set in plannedSets)
            {
                set.Weight.Value.Should().Be(tc.ExpectedWeight,
                    $"Week {tc.Week}: weight should be {tc.Intensity * 100}% of 100kg TM = {tc.ExpectedWeight}kg");
            }
        }
    }

    [Fact]
    public void PlannedSets_AfterProgression_ReflectUpdatedTM()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);

        // Get Week 1 planned sets (65% of 100 = 65kg)
        var week1Sets = strategy.CalculatePlannedSets(1, 1).ToList();
        week1Sets.First().Weight.Value.Should().Be(65.0m, "Week 1: 65% of 100kg");

        // Apply a +5 AMRAP delta (+3%) -> TM goes from 100 to 103.00
        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < week1Sets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(i + 1, week1Sets[i].Weight, 12, wasAmrap: false));
        }
        completedSets.Add(new CompletedSet(week1Sets.Count, week1Sets.Last().Weight, 20, wasAmrap: true)); // 15 + 5 = 20
        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, week1Sets, completedSets));

        strategy.TrainingMax.Value.Should().Be(103.00m, "TM should be 103.00 after +3% on 100");

        // Get Week 2 planned sets with the NEW TM
        var week2Sets = strategy.CalculatePlannedSets(2, 1).ToList();

        // Week 2 is 68% intensity: 103.00 * 0.68 = 70.04 -> round to 70.0
        var expectedWeight = Math.Round(103.00m * 0.68m / 2.5m) * 2.5m; // = 70.0
        week2Sets.First().Weight.Value.Should().Be(expectedWeight,
            $"Week 2: 68% of new TM 103.00kg = {expectedWeight}kg");

        // AMRAP flag still on last set
        week2Sets.Last().IsAmrap.Should().BeTrue("AMRAP should still be on last set after TM adjustment");
    }

    #endregion
}
