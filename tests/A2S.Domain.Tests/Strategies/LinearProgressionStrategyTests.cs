using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Unit tests for LinearProgressionStrategy covering all 21 weeks of the A2S2 program.
/// Tests verify sets, reps/set, and rep-out targets match the spreadsheet exactly.
///
/// A2S2 program structure:
///   21 weeks = 3 blocks × (6 working weeks + 1 deload)
///   Each block has 2 mini-cycles (MC) of 3 weeks.
///   Working weeks: 5 sets. Deload weeks: 4 sets.
///
/// Primary (T1, e.g., Back Squat): reps range 5 → 1
/// Auxiliary (T2, e.g., Front Squat): reps range 7 → 2
///
/// Rep-out target:
///   MC1 (weeks 1-3 of each block): reps × 2
///   MC2 (weeks 4-6 of blocks 1-2): reps × 2 - 1
///   MC2 (weeks 4-6 of block 3): reps × 2
///
/// TM stored at 2dp precision (NOT rounded to gym increments).
/// </summary>
public class LinearProgressionStrategyTests
{
    private readonly TrainingMax _primaryTm = TrainingMax.Create(120m, WeightUnit.Kilograms);
    private readonly TrainingMax _auxiliaryTm = TrainingMax.Create(67.5m, WeightUnit.Kilograms);
    private readonly ExerciseId _testExerciseId = new(Guid.Parse("eee22222-2222-2222-2222-222222222222"));

    #region Primary (T1) — Back Squat — All 21 Weeks

    [Theory]
    // Block 1: Weeks 1-7
    //         (week, block, repsPerSet, sets, repOutTarget)  — repOutTarget=0 means deload
    [InlineData( 1, 1, 5, 5, 10)]  // MC1-W1: 5 reps, repOut = 5×2 = 10
    [InlineData( 2, 1, 4, 5,  8)]  // MC1-W2: 4 reps, repOut = 4×2 = 8
    [InlineData( 3, 1, 3, 5,  6)]  // MC1-W3: 3 reps, repOut = 3×2 = 6
    [InlineData( 4, 1, 5, 5,  9)]  // MC2-W1: 5 reps, repOut = 5×2-1 = 9
    [InlineData( 5, 1, 4, 5,  7)]  // MC2-W2: 4 reps, repOut = 4×2-1 = 7
    [InlineData( 6, 1, 3, 5,  5)]  // MC2-W3: 3 reps, repOut = 3×2-1 = 5
    [InlineData( 7, 1, 5, 4,  0)]  // DELOAD: 5 reps, 4 sets, no AMRAP
    // Block 2: Weeks 8-14
    [InlineData( 8, 2, 4, 5,  8)]  // MC1-W1: 4 reps, repOut = 4×2 = 8
    [InlineData( 9, 2, 3, 5,  6)]  // MC1-W2: 3 reps, repOut = 3×2 = 6
    [InlineData(10, 2, 2, 5,  4)]  // MC1-W3: 2 reps, repOut = 2×2 = 4
    [InlineData(11, 2, 4, 5,  7)]  // MC2-W1: 4 reps, repOut = 4×2-1 = 7
    [InlineData(12, 2, 3, 5,  5)]  // MC2-W2: 3 reps, repOut = 3×2-1 = 5
    [InlineData(13, 2, 2, 5,  3)]  // MC2-W3: 2 reps, repOut = 2×2-1 = 3
    [InlineData(14, 2, 5, 4,  0)]  // DELOAD
    // Block 3: Weeks 15-21
    [InlineData(15, 3, 3, 5,  6)]  // MC1-W1: 3 reps, repOut = 3×2 = 6
    [InlineData(16, 3, 2, 5,  4)]  // MC1-W2: 2 reps, repOut = 2×2 = 4
    [InlineData(17, 3, 1, 5,  2)]  // MC1-W3: 1 rep,  repOut = 1×2 = 2
    [InlineData(18, 3, 2, 5,  4)]  // MC2-W1: 2 reps, repOut = 2×2 = 4
    [InlineData(19, 3, 1, 5,  2)]  // MC2-W2: 1 rep,  repOut = 1×2 = 2
    [InlineData(20, 3, 1, 5,  2)]  // MC2-W3: 1 rep,  repOut = 1×2 = 2
    [InlineData(21, 3, 5, 4,  0)]  // DELOAD
    public void Primary_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, int block, int expectedRepsPerSet, int expectedSets, int expectedRepOutTarget)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: block).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets,
            $"Week {week}: should have {expectedSets} sets");

        if (expectedRepOutTarget == 0) // Deload — no AMRAP, all sets same reps
        {
            foreach (var set in plannedSets)
            {
                set.TargetReps.Should().Be(expectedRepsPerSet,
                    $"Week {week} (deload): all sets should target {expectedRepsPerSet} reps");
                set.IsAmrap.Should().BeFalse(
                    $"Week {week} (deload): no AMRAP on deload");
            }
        }
        else // Working week — normal sets + AMRAP last set
        {
            // Normal sets (all except last)
            for (int i = 0; i < plannedSets.Count - 1; i++)
            {
                plannedSets[i].TargetReps.Should().Be(expectedRepsPerSet,
                    $"Week {week}: normal set {i + 1} should target {expectedRepsPerSet} reps");
                plannedSets[i].IsAmrap.Should().BeFalse(
                    $"Week {week}: set {i + 1} should NOT be AMRAP");
            }

            // Last set is AMRAP with rep-out target
            var lastSet = plannedSets.Last();
            lastSet.TargetReps.Should().Be(expectedRepOutTarget,
                $"Week {week}: AMRAP set should target rep-out {expectedRepOutTarget}");
            lastSet.IsAmrap.Should().BeTrue(
                $"Week {week}: last set should be AMRAP");
        }
    }

    #endregion

    #region Auxiliary (T2) — Front Squat — All 21 Weeks

    [Theory]
    // Block 1: Weeks 1-7
    //         (week, block, repsPerSet, sets, repOutTarget)
    [InlineData( 1, 1, 7, 5, 14)]  // MC1-W1: 7 reps, repOut = 7×2 = 14
    [InlineData( 2, 1, 6, 5, 12)]  // MC1-W2: 6 reps, repOut = 6×2 = 12
    [InlineData( 3, 1, 5, 5, 10)]  // MC1-W3: 5 reps, repOut = 5×2 = 10
    [InlineData( 4, 1, 7, 5, 13)]  // MC2-W1: 7 reps, repOut = 7×2-1 = 13
    [InlineData( 5, 1, 6, 5, 11)]  // MC2-W2: 6 reps, repOut = 6×2-1 = 11
    [InlineData( 6, 1, 5, 5,  9)]  // MC2-W3: 5 reps, repOut = 5×2-1 = 9
    [InlineData( 7, 1, 5, 4,  0)]  // DELOAD
    // Block 2: Weeks 8-14
    [InlineData( 8, 2, 6, 5, 12)]  // MC1-W1: 6 reps, repOut = 6×2 = 12
    [InlineData( 9, 2, 5, 5, 10)]  // MC1-W2: 5 reps, repOut = 5×2 = 10
    [InlineData(10, 2, 4, 5,  8)]  // MC1-W3: 4 reps, repOut = 4×2 = 8
    [InlineData(11, 2, 6, 5, 11)]  // MC2-W1: 6 reps, repOut = 6×2-1 = 11
    [InlineData(12, 2, 5, 5,  9)]  // MC2-W2: 5 reps, repOut = 5×2-1 = 9
    [InlineData(13, 2, 4, 5,  7)]  // MC2-W3: 4 reps, repOut = 4×2-1 = 7
    [InlineData(14, 2, 5, 4,  0)]  // DELOAD
    // Block 3: Weeks 15-21
    [InlineData(15, 3, 5, 5, 10)]  // MC1-W1: 5 reps, repOut = 5×2 = 10
    [InlineData(16, 3, 4, 5,  8)]  // MC1-W2: 4 reps, repOut = 4×2 = 8
    [InlineData(17, 3, 3, 5,  6)]  // MC1-W3: 3 reps, repOut = 3×2 = 6
    [InlineData(18, 3, 4, 5,  8)]  // MC2-W1: 4 reps, repOut = 4×2 = 8
    [InlineData(19, 3, 3, 5,  6)]  // MC2-W2: 3 reps, repOut = 3×2 = 6
    [InlineData(20, 3, 2, 5,  4)]  // MC2-W3: 2 reps, repOut = 2×2 = 4
    [InlineData(21, 3, 5, 4,  0)]  // DELOAD
    public void Auxiliary_CalculatePlannedSets_ShouldMatchSpreadsheet(
        int week, int block, int expectedRepsPerSet, int expectedSets, int expectedRepOutTarget)
    {
        // Arrange
        var strategy = LinearProgressionStrategy.Create(
            _auxiliaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber: block).ToList();

        // Assert
        plannedSets.Should().HaveCount(expectedSets,
            $"Week {week}: should have {expectedSets} sets");

        if (expectedRepOutTarget == 0) // Deload
        {
            foreach (var set in plannedSets)
            {
                set.TargetReps.Should().Be(expectedRepsPerSet,
                    $"Week {week} (deload): all sets should target {expectedRepsPerSet} reps");
                set.IsAmrap.Should().BeFalse(
                    $"Week {week} (deload): no AMRAP on deload");
            }
        }
        else // Working week
        {
            for (int i = 0; i < plannedSets.Count - 1; i++)
            {
                plannedSets[i].TargetReps.Should().Be(expectedRepsPerSet,
                    $"Week {week}: normal set {i + 1} should target {expectedRepsPerSet} reps");
                plannedSets[i].IsAmrap.Should().BeFalse(
                    $"Week {week}: set {i + 1} should NOT be AMRAP");
            }

            var lastSet = plannedSets.Last();
            lastSet.TargetReps.Should().Be(expectedRepOutTarget,
                $"Week {week}: AMRAP set should target rep-out {expectedRepOutTarget}");
            lastSet.IsAmrap.Should().BeTrue(
                $"Week {week}: last set should be AMRAP");
        }
    }

    #endregion

    #region Deload Week Structure

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void DeloadWeeks_ShouldHave4Sets_5Reps_NoAmrap(int deloadWeek)
    {
        var block = (deloadWeek - 1) / 7 + 1;

        var primaryStrategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);
        var auxiliaryStrategy = LinearProgressionStrategy.Create(
            _auxiliaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        foreach (var (label, strategy) in new[] { ("Primary", primaryStrategy), ("Auxiliary", auxiliaryStrategy) })
        {
            var plannedSets = strategy.CalculatePlannedSets(deloadWeek, block).ToList();

            plannedSets.Should().HaveCount(4,
                $"{label} Week {deloadWeek} (deload): should have 4 sets");
            plannedSets.All(s => s.TargetReps == 5).Should().BeTrue(
                $"{label} Week {deloadWeek} (deload): all sets should target 5 reps");
            plannedSets.All(s => !s.IsAmrap).Should().BeTrue(
                $"{label} Week {deloadWeek} (deload): no AMRAP on deload");
        }
    }

    #endregion

    #region TM Adjustment Tests (RTF Progression — AMRAP Delta Table)

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
        // Arrange — TM=100kg for easy math
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Week 1 Primary: repsPerSet=5, repOutTarget=10. AMRAP set targets 10.
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var repOutTarget = 10; // Primary Week 1 rep-out target
        var actualReps = repOutTarget + amrapDelta;

        // Normal sets complete at repsPerSet (5)
        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1,
                plannedSets[i].Weight,
                5, // repsPerSet for normal sets
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

        // Assert — TM stored at 2dp precision, NOT rounded to 2.5kg
        var expectedTm = Math.Round(100m * (1 + expectedAdjustmentPercent), 2);
        strategy.TrainingMax.Value.Should().Be(expectedTm,
            $"AMRAP delta {amrapDelta} should result in {expectedAdjustmentPercent * 100}% TM adjustment");
    }

    [Fact]
    public void ApplyPerformanceResult_WithoutAmrap_ShouldNotChangeTm()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: false, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

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
    [InlineData(1, 5)]
    [InlineData(2, 5)]
    [InlineData(3, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(7, 4)]    // Deload
    [InlineData(8, 5)]
    [InlineData(9, 5)]
    [InlineData(10, 5)]
    [InlineData(11, 5)]
    [InlineData(12, 5)]
    [InlineData(13, 5)]
    [InlineData(14, 4)]   // Deload
    [InlineData(15, 5)]
    [InlineData(16, 5)]
    [InlineData(17, 5)]
    [InlineData(18, 5)]
    [InlineData(19, 5)]
    [InlineData(20, 5)]
    [InlineData(21, 4)]   // Deload
    public void GetSetsForWeek_AllWeeks_ShouldMatchSpreadsheet(int week, int expectedSets)
    {
        var sets = A2SHypertrophyProgram.GetSetsForWeek(week);
        sets.Should().Be(expectedSets,
            $"Week {week} should have {expectedSets} sets (5 working, 4 deload)");
    }

    [Fact]
    public void GetSetsForWeek_InvalidWeek_ShouldThrowException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => A2SHypertrophyProgram.GetSetsForWeek(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => A2SHypertrophyProgram.GetSetsForWeek(22));
    }

    #endregion

    #region Full 21-Week Cycle Simulation — Primary

    [Fact]
    public void Full21WeekCycle_Primary_WithConsistentPerformance_ShouldProgressTm()
    {
        // Arrange
        var startingTm = TrainingMax.Create(120m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            startingTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var tmHistory = new List<(int Week, decimal TmValue)>
        {
            (0, 120m)
        };

        // Act — Simulate 21 weeks with +5 AMRAP delta (+3%) on non-deload weeks
        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

            if (!isDeload)
            {
                var repsPerSet = plannedSets.First().TargetReps;
                var repOutTarget = plannedSets.Last().TargetReps;

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
        finalTm.Should().BeGreaterThan(120m,
            "TM should increase over 21 weeks with consistent +5 AMRAP delta");

        // Verify deload weeks didn't change TM
        tmHistory[7].TmValue.Should().Be(tmHistory[6].TmValue,
            "Week 7 deload should not change TM");
        tmHistory[14].TmValue.Should().Be(tmHistory[13].TmValue,
            "Week 14 deload should not change TM");
        tmHistory[21].TmValue.Should().Be(tmHistory[20].TmValue,
            "Week 21 deload should not change TM");
    }

    #endregion

    #region Full 21-Week Cycle Simulation — Auxiliary

    [Fact]
    public void Full21WeekCycle_Auxiliary_WithConsistentPerformance_ShouldProgressTm()
    {
        // Arrange
        var startingTm = TrainingMax.Create(67.5m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            startingTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        var tmHistory = new List<(int Week, decimal TmValue)>
        {
            (0, 67.5m)
        };

        for (int week = 1; week <= 21; week++)
        {
            var blockNumber = (week - 1) / 7 + 1;
            var isDeload = week == 7 || week == 14 || week == 21;

            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

            if (!isDeload)
            {
                var repsPerSet = plannedSets.First().TargetReps;
                var repOutTarget = plannedSets.Last().TargetReps;

                var completedSets = new List<CompletedSet>();
                for (int i = 0; i < plannedSets.Count - 1; i++)
                {
                    completedSets.Add(new CompletedSet(
                        i + 1, plannedSets[i].Weight, repsPerSet, wasAmrap: false));
                }
                completedSets.Add(new CompletedSet(
                    plannedSets.Count,
                    plannedSets.Last().Weight,
                    repOutTarget + 3, // +3 AMRAP delta = +1.5% TM
                    wasAmrap: true));

                var performance = new ExercisePerformance(_testExerciseId, plannedSets, completedSets);
                strategy.ApplyPerformanceResult(performance);
            }

            tmHistory.Add((week, strategy.TrainingMax.Value));
        }

        // Assert
        var finalTm = strategy.TrainingMax.Value;
        finalTm.Should().BeGreaterThan(67.5m,
            "TM should increase over 21 weeks with consistent +3 AMRAP delta");

        tmHistory[7].TmValue.Should().Be(tmHistory[6].TmValue,
            "Week 7 deload should not change TM");
        tmHistory[14].TmValue.Should().Be(tmHistory[13].TmValue,
            "Week 14 deload should not change TM");
        tmHistory[21].TmValue.Should().Be(tmHistory[20].TmValue,
            "Week 21 deload should not change TM");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculatePlannedSets_InvalidWeek_ShouldThrowException()
    {
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        Assert.Throws<BusinessRuleViolationException>(() => strategy.CalculatePlannedSets(0, 1));
        Assert.Throws<BusinessRuleViolationException>(() => strategy.CalculatePlannedSets(22, 1));
    }

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        strategy.Should().NotBeNull();
        strategy.TrainingMax.Should().Be(_primaryTm);
        strategy.UseAmrap.Should().BeTrue();
        strategy.BaseSetsPerExercise.Should().Be(5);
        strategy.Tier.Should().Be(ProgramTier.Primary);
    }

    [Fact]
    public void Create_AuxiliaryTier_ShouldStoreTier()
    {
        var strategy = LinearProgressionStrategy.Create(
            _auxiliaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        strategy.Tier.Should().Be(ProgramTier.Auxiliary);
    }

    [Fact]
    public void UpdateTrainingMax_ShouldChangeTrainingMax()
    {
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);
        var newTm = TrainingMax.Create(130m, WeightUnit.Kilograms);

        strategy.UpdateTrainingMaxValue(newTm, "Manual adjustment");

        strategy.TrainingMax.Value.Should().Be(130m);
    }

    #endregion

    #region Additional TM Precision Tests

    [Fact]
    public void ApplyPerformanceResult_SmallTmIncrease_StoredAt2dp()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Week 1 Primary: repOutTarget=10
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();

        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1, plannedSets[i].Weight, 5, wasAmrap: false));
        }
        // +2 delta: actual = 10 + 2 = 12
        completedSets.Add(new CompletedSet(
            plannedSets.Count,
            plannedSets.Last().Weight,
            12,
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
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // First failure: -2 delta = -5%
        var plannedSets1 = strategy.CalculatePlannedSets(1, 1).ToList();

        var completedSets1 = new List<CompletedSet>();
        for (int i = 0; i < plannedSets1.Count - 1; i++)
        {
            completedSets1.Add(new CompletedSet(i + 1, plannedSets1[i].Weight, 5, wasAmrap: false));
        }
        completedSets1.Add(new CompletedSet(
            plannedSets1.Count, plannedSets1.Last().Weight,
            8, // repOutTarget(10) - 2 = actual 8 → delta = -2
            wasAmrap: true));

        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, plannedSets1, completedSets1));
        strategy.TrainingMax.Value.Should().Be(95m, "100kg * 0.95 = 95kg");

        // Second failure: -2 delta = -5% of 95
        var plannedSets2 = strategy.CalculatePlannedSets(2, 1).ToList();

        var completedSets2 = new List<CompletedSet>();
        for (int i = 0; i < plannedSets2.Count - 1; i++)
        {
            completedSets2.Add(new CompletedSet(i + 1, plannedSets2[i].Weight, 4, wasAmrap: false));
        }
        // Week 2 Primary: repOutTarget=8, delta=-2 → actual=6
        completedSets2.Add(new CompletedSet(
            plannedSets2.Count, plannedSets2.Last().Weight,
            6,
            wasAmrap: true));

        strategy.ApplyPerformanceResult(new ExercisePerformance(_testExerciseId, plannedSets2, completedSets2));
        strategy.TrainingMax.Value.Should().Be(90.25m,
            "95kg * 0.95 = 90.25kg (stored at 2dp)");
    }

    #endregion

    #region Rep Progression Pattern Validation

    [Fact]
    public void Primary_RepProgression_ShouldDecrease5To1AcrossBlocks()
    {
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Block 1 starts at 5 reps
        strategy.CalculatePlannedSets(1, 1).First().TargetReps.Should().Be(5, "Block 1 starts at 5 reps");
        // Block 2 starts at 4 reps
        strategy.CalculatePlannedSets(8, 2).First().TargetReps.Should().Be(4, "Block 2 starts at 4 reps");
        // Block 3 starts at 3 reps
        strategy.CalculatePlannedSets(15, 3).First().TargetReps.Should().Be(3, "Block 3 starts at 3 reps");
        // Block 3 reaches 1 rep
        strategy.CalculatePlannedSets(17, 3).First().TargetReps.Should().Be(1, "Block 3 week 3 reaches 1 rep");
    }

    [Fact]
    public void Auxiliary_RepProgression_ShouldDecrease7To2AcrossBlocks()
    {
        var strategy = LinearProgressionStrategy.Create(
            _auxiliaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        // Block 1 starts at 7 reps
        strategy.CalculatePlannedSets(1, 1).First().TargetReps.Should().Be(7, "Block 1 starts at 7 reps");
        // Block 2 starts at 6 reps
        strategy.CalculatePlannedSets(8, 2).First().TargetReps.Should().Be(6, "Block 2 starts at 6 reps");
        // Block 3 starts at 5 reps
        strategy.CalculatePlannedSets(15, 3).First().TargetReps.Should().Be(5, "Block 3 starts at 5 reps");
        // Block 3 reaches 2 reps (floor for T2)
        strategy.CalculatePlannedSets(20, 3).First().TargetReps.Should().Be(2, "Block 3 week 6 reaches 2 reps");
    }

    #endregion

    #region AMRAP Structure Validation

    [Fact]
    public void Primary_WorkingWeeks_LastSetIsAmrapAllOthersNormal()
    {
        var strategy = LinearProgressionStrategy.Create(
            _primaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Test a selection of working weeks across all blocks
        foreach (var (week, block) in new[] { (1, 1), (5, 1), (10, 2), (13, 2), (17, 3), (20, 3) })
        {
            var plannedSets = strategy.CalculatePlannedSets(week, block).ToList();

            plannedSets.Should().HaveCount(5, $"Week {week} should have 5 sets");
            plannedSets.Last().IsAmrap.Should().BeTrue($"Week {week}: last set should be AMRAP");

            for (int i = 0; i < 4; i++)
            {
                plannedSets[i].IsAmrap.Should().BeFalse($"Week {week}: set {i + 1} should NOT be AMRAP");
            }
        }
    }

    [Fact]
    public void Auxiliary_WorkingWeeks_LastSetIsAmrapAllOthersNormal()
    {
        var strategy = LinearProgressionStrategy.Create(
            _auxiliaryTm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);

        foreach (var (week, block) in new[] { (1, 1), (6, 1), (9, 2), (13, 2), (16, 3), (20, 3) })
        {
            var plannedSets = strategy.CalculatePlannedSets(week, block).ToList();

            plannedSets.Should().HaveCount(5, $"Week {week} should have 5 sets");
            plannedSets.Last().IsAmrap.Should().BeTrue($"Week {week}: last set should be AMRAP");

            for (int i = 0; i < 4; i++)
            {
                plannedSets[i].IsAmrap.Should().BeFalse($"Week {week}: set {i + 1} should NOT be AMRAP");
            }
        }
    }

    #endregion
}
