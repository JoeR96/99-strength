using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Validates LinearProgressionStrategy against the actual A2S2 spreadsheet.
///
/// A2S2 program structure:
///   21 weeks = 3 blocks × (6 working weeks + 1 deload)
///   Working weeks: 5 sets. Deload weeks: 4 sets, 5 reps, no AMRAP.
///
/// Primary (T1): reps 5→1. Auxiliary (T2): reps 7→2.
///
/// Rep-out target formula:
///   MC1 (weeks 1-3 of each block): reps × 2
///   MC2 (weeks 4-6 of blocks 1-2): reps × 2 - 1
///   MC2 (weeks 4-6 of block 3): reps × 2
/// </summary>
public class LinearProgressionSpreadsheetValidationTests
{
    private readonly ExerciseId _exerciseId = new(Guid.Parse("eee77777-7777-7777-7777-777777777777"));

    #region Program Table Validation — Primary (T1)

    /// <summary>
    /// Verifies the program table for Primary tier matches the A2S2 spreadsheet
    /// for all 21 weeks: sets, reps/set, and rep-out target.
    /// </summary>
    [Theory]
    // Block 1                (week, sets, reps, repOut)  — repOut=0 means deload
    [InlineData( 1, 5, 5, 10)]
    [InlineData( 2, 5, 4,  8)]
    [InlineData( 3, 5, 3,  6)]
    [InlineData( 4, 5, 5,  9)]
    [InlineData( 5, 5, 4,  7)]
    [InlineData( 6, 5, 3,  5)]
    [InlineData( 7, 4, 5,  0)]  // Deload
    // Block 2
    [InlineData( 8, 5, 4,  8)]
    [InlineData( 9, 5, 3,  6)]
    [InlineData(10, 5, 2,  4)]
    [InlineData(11, 5, 4,  7)]
    [InlineData(12, 5, 3,  5)]
    [InlineData(13, 5, 2,  3)]
    [InlineData(14, 4, 5,  0)]  // Deload
    // Block 3
    [InlineData(15, 5, 3,  6)]
    [InlineData(16, 5, 2,  4)]
    [InlineData(17, 5, 1,  2)]
    [InlineData(18, 5, 2,  4)]
    [InlineData(19, 5, 1,  2)]
    [InlineData(20, 5, 1,  2)]
    [InlineData(21, 4, 5,  0)]  // Deload
    public void PrimaryProgramTable_ShouldMatchSpreadsheet(
        int week, int expectedSets, int expectedReps, int expectedRepOut)
    {
        var isDeload = week == 7 || week == 14 || week == 21;

        var tm = TrainingMax.Create(120m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);
        var blockNumber = (week - 1) / 7 + 1;
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

        plannedSets.Should().HaveCount(expectedSets,
            $"Week {week}: should have {expectedSets} sets");

        plannedSets.First().TargetReps.Should().Be(expectedReps,
            $"Week {week}: normal set reps should be {expectedReps}");

        if (!isDeload)
        {
            plannedSets.Last().IsAmrap.Should().BeTrue(
                $"Week {week}: last set should be AMRAP");
            plannedSets.Last().TargetReps.Should().Be(expectedRepOut,
                $"Week {week}: AMRAP set should have rep-out target {expectedRepOut}");
        }
        else
        {
            plannedSets.Last().IsAmrap.Should().BeFalse(
                $"Week {week}: deload should not have AMRAP");
        }
    }

    #endregion

    #region Program Table Validation — Auxiliary (T2)

    [Theory]
    // Block 1                (week, sets, reps, repOut)
    [InlineData( 1, 5, 7, 14)]
    [InlineData( 2, 5, 6, 12)]
    [InlineData( 3, 5, 5, 10)]
    [InlineData( 4, 5, 7, 13)]
    [InlineData( 5, 5, 6, 11)]
    [InlineData( 6, 5, 5,  9)]
    [InlineData( 7, 4, 5,  0)]  // Deload
    // Block 2
    [InlineData( 8, 5, 6, 12)]
    [InlineData( 9, 5, 5, 10)]
    [InlineData(10, 5, 4,  8)]
    [InlineData(11, 5, 6, 11)]
    [InlineData(12, 5, 5,  9)]
    [InlineData(13, 5, 4,  7)]
    [InlineData(14, 4, 5,  0)]  // Deload
    // Block 3
    [InlineData(15, 5, 5, 10)]
    [InlineData(16, 5, 4,  8)]
    [InlineData(17, 5, 3,  6)]
    [InlineData(18, 5, 4,  8)]
    [InlineData(19, 5, 3,  6)]
    [InlineData(20, 5, 2,  4)]
    [InlineData(21, 4, 5,  0)]  // Deload
    public void AuxiliaryProgramTable_ShouldMatchSpreadsheet(
        int week, int expectedSets, int expectedReps, int expectedRepOut)
    {
        var isDeload = week == 7 || week == 14 || week == 21;

        var tm = TrainingMax.Create(67.5m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Auxiliary);
        var blockNumber = (week - 1) / 7 + 1;
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

        plannedSets.Should().HaveCount(expectedSets,
            $"Week {week}: should have {expectedSets} sets");

        plannedSets.First().TargetReps.Should().Be(expectedReps,
            $"Week {week}: normal set reps should be {expectedReps}");

        if (!isDeload)
        {
            plannedSets.Last().IsAmrap.Should().BeTrue(
                $"Week {week}: last set should be AMRAP");
            plannedSets.Last().TargetReps.Should().Be(expectedRepOut,
                $"Week {week}: AMRAP set should have rep-out target {expectedRepOut}");
        }
        else
        {
            plannedSets.Last().IsAmrap.Should().BeFalse(
                $"Week {week}: deload should not have AMRAP");
        }
    }

    #endregion

    #region Rep-out Target Formula Validation

    /// <summary>
    /// Primary rep-out target:
    ///   MC1 (weeks 1-3 of block): reps × 2
    ///   MC2 (weeks 4-6 of blocks 1-2): reps × 2 - 1
    ///   MC2 (weeks 4-6 of block 3): reps × 2
    /// </summary>
    [Theory]
    // MC1: repOut = reps × 2
    [InlineData( 1, 5, 10, 2)]  // 5×2 = 10
    [InlineData( 2, 4,  8, 2)]  // 4×2 = 8
    [InlineData( 3, 3,  6, 2)]  // 3×2 = 6
    [InlineData( 8, 4,  8, 2)]
    [InlineData( 9, 3,  6, 2)]
    [InlineData(10, 2,  4, 2)]
    [InlineData(15, 3,  6, 2)]
    [InlineData(16, 2,  4, 2)]
    [InlineData(17, 1,  2, 2)]
    // MC2 blocks 1-2: repOut = reps × 2 - 1
    [InlineData( 4, 5,  9, -1)]  // 5×2 = 10, -1 = 9
    [InlineData( 5, 4,  7, -1)]  // 4×2 = 8, -1 = 7
    [InlineData( 6, 3,  5, -1)]  // 3×2 = 6, -1 = 5
    [InlineData(11, 4,  7, -1)]
    [InlineData(12, 3,  5, -1)]
    [InlineData(13, 2,  3, -1)]
    // MC2 block 3: repOut = reps × 2 (same as MC1)
    [InlineData(18, 2,  4, 2)]
    [InlineData(19, 1,  2, 2)]
    [InlineData(20, 1,  2, 2)]
    public void Primary_RepOutTarget_ShouldFollowFormula(
        int week, int expectedReps, int expectedRepOut, int formulaMultiplier)
    {
        // formulaMultiplier: 2 means repOut = reps × 2, -1 means repOut = reps × 2 - 1
        if (formulaMultiplier == 2)
        {
            expectedRepOut.Should().Be(expectedReps * 2,
                $"Week {week}: repOut should be reps×2 = {expectedReps * 2}");
        }
        else
        {
            expectedRepOut.Should().Be(expectedReps * 2 - 1,
                $"Week {week}: repOut should be reps×2-1 = {expectedReps * 2 - 1}");
        }
    }

    #endregion

    #region TM Adjustment Delta Table — Exhaustive Validation

    /// <summary>
    /// Every single TM adjustment multiplier from the A2S2 spreadsheet.
    /// Delta → percentage mapping is independent of program tier.
    /// </summary>
    [Theory]
    [InlineData(120.00, 5, 10, 5,  1.03,  123.60)]  // +5 over → ×1.03
    [InlineData(123.60, 4, 10, 6,  1.03,  127.31)]  // +6 over → ×1.03 (capped)
    [InlineData(100.00, 2,  4, 2,  1.01,  101.00)]  // +2 over → ×1.01
    [InlineData(100.00, 1,  2, 1,  1.005, 100.50)]  // +1 over → ×1.005
    [InlineData(100.00, 3,  6, 3,  1.015, 101.50)]  // +3 over → ×1.015
    [InlineData(100.00, 4,  8, 4,  1.02,  102.00)]  // +4 over → ×1.02
    [InlineData(100.00, 4,  4, 0,  1.0,   100.00)]  // same    → ×1.0
    [InlineData(100.00, 3,  2, -1, 0.98,   98.00)]  // -1 under → ×0.98
    [InlineData(100.00, 5,  2, -3, 0.95,   95.00)]  // 2+ under → ×0.95
    public void TmAdjustment_AllMultipliers_ShouldMatchSpreadsheet(
        double tmBeforeD, int repOutTarget, int amrapResult, int expectedDelta,
        double multiplierD, double expectedTmAfterD)
    {
        var tmBefore = (decimal)tmBeforeD;
        var expectedTmAfter = (decimal)expectedTmAfterD;

        var tm = TrainingMax.Create(tmBefore, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Create planned sets manually with the correct rep-out target
        var weight = Weight.Create(80m, WeightUnit.Kilograms); // arbitrary weight
        var manualPlannedSets = new List<PlannedSet>();
        for (int i = 1; i <= 5; i++)
        {
            bool isAmrap = i == 5;
            manualPlannedSets.Add(new PlannedSet(i, weight, repOutTarget, isAmrap));
        }

        var completedSets = CreateCompletedSets(manualPlannedSets, repOutTarget, amrapResult);
        var performance = new ExercisePerformance(_exerciseId, manualPlannedSets, completedSets);

        // Verify delta
        performance.GetAmrapDelta().Should().Be(expectedDelta,
            $"Delta should be {amrapResult} - {repOutTarget} = {expectedDelta}");

        strategy.ApplyPerformanceResult(performance);

        var manualCalc = Math.Round(tmBefore * (decimal)multiplierD, 2);
        strategy.TrainingMax.Value.Should().Be(manualCalc,
            $"TM should be {tmBefore} × {multiplierD} = {manualCalc}");

        strategy.TrainingMax.Value.Should().BeApproximately(expectedTmAfter, 0.01m,
            $"TM should match expected value {expectedTmAfter}");
    }

    #endregion

    #region Negative Delta Scenarios

    [Fact]
    public void NegativeDeltas_TmShouldDecrease()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        // Week 1 Primary: repOut=10, actual=9 → delta=-1 → -2%
        var week1Sets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completed1 = CreateCompletedSets(week1Sets, 5, 9);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week1Sets, completed1));
        strategy.TrainingMax.Value.Should().Be(98.00m, "delta=-1 → -2%");

        // Week 2 Primary: repOut=8, actual=5 → delta=-3 → -5%
        var week2Sets = strategy.CalculatePlannedSets(2, 1).ToList();
        var completed2 = CreateCompletedSets(week2Sets, 4, 5);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week2Sets, completed2));
        strategy.TrainingMax.Value.Should().Be(93.10m, "delta=-3 → -5%");
    }

    #endregion

    #region Deload Validation

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void Deload_NoAmrap_CorrectParameters(int deloadWeek)
    {
        var tm = TrainingMax.Create(120m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(
            tm, useAmrap: true, baseSetsPerExercise: 5, tier: ProgramTier.Primary);

        var blockNumber = (deloadWeek - 1) / 7 + 1;
        var plannedSets = strategy.CalculatePlannedSets(deloadWeek, blockNumber).ToList();

        plannedSets.Should().HaveCount(4, "Deload has 4 sets");
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue("Deload has no AMRAP sets");
        plannedSets.First().TargetReps.Should().Be(5, "Deload reps = 5");
    }

    #endregion

    #region Helpers

    private static List<CompletedSet> CreateCompletedSets(
        List<PlannedSet> plannedSets, int normalReps, int amrapReps)
    {
        var completedSets = new List<CompletedSet>();
        for (int i = 0; i < plannedSets.Count - 1; i++)
        {
            completedSets.Add(new CompletedSet(
                i + 1,
                plannedSets[i].Weight,
                normalReps,
                wasAmrap: false));
        }
        completedSets.Add(new CompletedSet(
            plannedSets.Count,
            plannedSets.Last().Weight,
            amrapReps,
            wasAmrap: true));
        return completedSets;
    }

    #endregion
}
