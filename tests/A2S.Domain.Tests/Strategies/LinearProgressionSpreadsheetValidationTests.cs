using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Strategies;

/// <summary>
/// Validates LinearProgressionStrategy against the actual A2S2 Hypertrophy spreadsheet.
/// Data source: src/A2S.Api/A2S2_Validation_Data.md
///
/// These tests use two real exercises from the spreadsheet:
///   - Overhead Press (Smith Machine): TM=65kg, rounding=2.5kg
///   - Smith Squat: TM=110kg, rounding=5kg
///
/// The spreadsheet defines for each week:
///   - Intensity%: used to calculate working weight = MROUND(TM × intensity%, rounding)
///   - Reps/Set: reps for each normal set
///   - Rep-out Target: the AMRAP baseline for delta calculation (differs from Reps/Set!)
///   - Set Goal: number of sets (always 4 in hypertrophy)
///
/// Key differences from the current (incorrect) code:
///   1. Intensities are 0.60–0.79 (NOT 0.65–1.05 which is the strength variant)
///   2. Sets are always 4 (NOT varying 1–5)
///   3. Rep-out Target ≠ Reps/Set (Rep-out Target = Reps/Set + 2, except Week 1 = +3)
///   4. Deload weeks use reps=5 (NOT reps=10)
/// </summary>
public class LinearProgressionSpreadsheetValidationTests
{
    private readonly ExerciseId _exerciseId = new(Guid.NewGuid());

    #region Spreadsheet Reference Data

    /// <summary>
    /// The correct A2S2 Hypertrophy weekly program from the spreadsheet.
    /// (Intensity, Sets, RepsPerSet, RepOutTarget)
    /// RepOutTarget is null for deload weeks (no AMRAP).
    /// </summary>
    private static readonly (decimal Intensity, int Sets, int RepsPerSet, int? RepOutTarget)[] HypertrophyProgram =
    {
        // Week 0 placeholder
        (0m, 0, 0, null),

        // Block 1: Weeks 1-7
        (0.65m, 4, 12, 15),   // Week 1
        (0.68m, 4, 11, 13),   // Week 2
        (0.70m, 4, 10, 12),   // Week 3
        (0.68m, 4, 11, 13),   // Week 4
        (0.70m, 4, 10, 12),   // Week 5
        (0.73m, 4, 9, 11),    // Week 6
        (0.60m, 4, 5, null),  // Week 7 - DELOAD

        // Block 2: Weeks 8-14
        (0.68m, 4, 11, 13),   // Week 8
        (0.70m, 4, 10, 12),   // Week 9
        (0.73m, 4, 9, 11),    // Week 10
        (0.70m, 4, 10, 12),   // Week 11
        (0.73m, 4, 9, 11),    // Week 12
        (0.76m, 4, 8, 10),    // Week 13
        (0.60m, 4, 5, null),  // Week 14 - DELOAD

        // Block 3: Weeks 15-21
        (0.70m, 4, 10, 12),   // Week 15
        (0.73m, 4, 9, 11),    // Week 16
        (0.76m, 4, 8, 10),    // Week 17
        (0.73m, 4, 9, 11),    // Week 18
        (0.76m, 4, 8, 10),    // Week 19
        (0.79m, 4, 7, 9),     // Week 20
        (0.60m, 4, 5, null),  // Week 21 - DELOAD
    };

    /// <summary>
    /// Rounds a weight to the nearest increment using MROUND (spreadsheet rounding).
    /// </summary>
    private static decimal MRound(decimal value, decimal increment)
        => Math.Round(value / increment) * increment;

    #endregion

    #region OHP Smith Machine — Full 6-Week Progression (TM=65kg, rounding=2.5kg)

    /// <summary>
    /// Validates the OHP progression across weeks 1-6 where AMRAP=19 every week.
    /// Spreadsheet data:
    ///   Week 1: TM=65.00 → WW=42.5, RepOut=15, AMRAP=19, delta=+4 → +2% → TM=66.30
    ///   Week 2: TM=66.30 → WW=45.0, RepOut=13, AMRAP=19, delta=+6 → +3% → TM=68.29
    ///   Week 3: TM=68.29 → WW=47.5, RepOut=12, AMRAP=19, delta=+7 → +3% → TM=70.34
    ///   Week 4: TM=70.34 → WW=47.5, RepOut=13, AMRAP=19, delta=+6 → +3% → TM=72.45
    ///   Week 5: TM=72.45 → WW=50.0, RepOut=12, AMRAP=19, delta=+7 → +3% → TM=74.62
    ///   Week 6: TM=74.62 → WW=55.0, RepOut=11, AMRAP=19, delta=+8 → +3% → TM=76.86
    /// </summary>
    [Fact]
    public void OHP_Weeks1To6_WithAmrap19_TmShouldMatchSpreadsheet()
    {
        // Arrange
        var tm = TrainingMax.Create(65m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 4);

        var expectedPerWeek = new[]
        {
            // (Week, ExpectedTmBefore, ExpectedWorkingWeight, RepOutTarget, AmrapResult, ExpectedTmAfter)
            (Week: 1, TmBefore: 65.00m,  WW: 42.5m, RepOut: 15, Amrap: 19, TmAfter: 66.30m),
            (Week: 2, TmBefore: 66.30m,  WW: 45.0m, RepOut: 13, Amrap: 19, TmAfter: 68.29m),
            (Week: 3, TmBefore: 68.29m,  WW: 47.5m, RepOut: 12, Amrap: 19, TmAfter: 70.34m),
            (Week: 4, TmBefore: 70.34m,  WW: 47.5m, RepOut: 13, Amrap: 19, TmAfter: 72.45m),
            (Week: 5, TmBefore: 72.45m,  WW: 50.0m, RepOut: 12, Amrap: 19, TmAfter: 74.62m),
            (Week: 6, TmBefore: 74.62m,  WW: 55.0m, RepOut: 11, Amrap: 19, TmAfter: 76.86m),
        };

        foreach (var expected in expectedPerWeek)
        {
            var blockNumber = (expected.Week - 1) / 7 + 1;

            // Assert TM before this week
            strategy.TrainingMax.Value.Should().BeApproximately(expected.TmBefore, 0.01m,
                $"Week {expected.Week}: TM before should be {expected.TmBefore}kg");

            // Act: get planned sets
            var plannedSets = strategy.CalculatePlannedSets(expected.Week, blockNumber).ToList();

            // Assert: working weight matches spreadsheet
            plannedSets.First().Weight.Value.Should().Be(expected.WW,
                $"Week {expected.Week}: Working weight should be {expected.WW}kg " +
                $"(MROUND({expected.TmBefore} × {HypertrophyProgram[expected.Week].Intensity}, 2.5))");

            // Assert: 4 sets always
            plannedSets.Should().HaveCount(4,
                $"Week {expected.Week}: Hypertrophy always has 4 sets");

            // Assert: last set is AMRAP
            plannedSets.Last().IsAmrap.Should().BeTrue(
                $"Week {expected.Week}: Last set should be AMRAP");

            // Assert: rep-out target (AMRAP baseline) matches spreadsheet
            // The AMRAP delta should be calculated against RepOutTarget, NOT RepsPerSet
            var amrapPlanned = plannedSets.Last();
            // The planned set's TargetReps should be the Rep-out Target for delta calculation
            // Spreadsheet: delta = AMRAP_Result - Rep_out_Target
            var spreadsheetDelta = expected.Amrap - expected.RepOut;

            // Build completed sets with AMRAP result
            var completedSets = CreateCompletedSets(
                plannedSets,
                HypertrophyProgram[expected.Week].RepsPerSet,
                expected.Amrap);

            var performance = new ExercisePerformance(_exerciseId, plannedSets, completedSets);

            // The delta calculation should match the spreadsheet
            var actualDelta = performance.GetAmrapDelta();
            actualDelta.Should().Be(spreadsheetDelta,
                $"Week {expected.Week}: AMRAP delta should be {expected.Amrap} - {expected.RepOut} = {spreadsheetDelta}");

            // Apply progression
            strategy.ApplyPerformanceResult(performance);

            // Assert TM after matches spreadsheet
            strategy.TrainingMax.Value.Should().BeApproximately(expected.TmAfter, 0.01m,
                $"Week {expected.Week}: TM after should be {expected.TmAfter}kg");
        }
    }

    /// <summary>
    /// After week 6, OHP enters deload (week 7) and then weeks 8-21 with no AMRAP entered.
    /// TM should remain at 76.86kg for all remaining weeks.
    /// </summary>
    [Fact]
    public void OHP_Week7Deload_TmShouldNotChange()
    {
        // Arrange: simulate weeks 1-6 to get TM to 76.86
        var strategy = SimulateOhpWeeks1Through6();
        strategy.TrainingMax.Value.Should().BeApproximately(76.86m, 0.01m);

        var blockNumber = 1; // Week 7 is in block 1

        // Act
        var plannedSets = strategy.CalculatePlannedSets(7, blockNumber).ToList();

        // Assert: deload parameters
        plannedSets.Should().HaveCount(4, "Deload should still have 4 sets in hypertrophy");

        var expectedWW = MRound(76.86m * 0.60m, 2.5m); // 46.116 → 45.0
        plannedSets.First().Weight.Value.Should().Be(45.0m,
            "Week 7 deload: MROUND(76.86 × 0.60, 2.5) = 45.0kg");

        plannedSets.First().TargetReps.Should().Be(5,
            "Week 7 deload: 5 reps per set");

        // Deload: no AMRAP, TM stays unchanged
        // (In production, no AMRAP result is submitted for deload weeks)
    }

    /// <summary>
    /// Validates weeks 8-13 working weights when TM is frozen at 76.86kg.
    /// No AMRAP results entered, so TM doesn't change.
    /// </summary>
    [Theory]
    [InlineData(8, 0.68, 52.5, 11, 13)]
    [InlineData(9, 0.70, 55.0, 10, 12)]
    [InlineData(10, 0.73, 55.0, 9, 11)]  // 76.86×0.73=56.1078 → MROUND(56.1, 2.5) = 55.0
    [InlineData(11, 0.70, 55.0, 10, 12)]
    [InlineData(12, 0.73, 55.0, 9, 11)]
    [InlineData(13, 0.76, 57.5, 8, 10)]
    public void OHP_Weeks8To13_NoAmrap_WorkingWeightsShouldMatchSpreadsheet(
        int week, decimal intensity, decimal expectedWW, int expectedReps, int expectedRepOut)
    {
        // Arrange
        var strategy = SimulateOhpWeeks1Through6();
        var tm = strategy.TrainingMax.Value; // 76.86

        var blockNumber = (week - 1) / 7 + 1;

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

        // Assert
        plannedSets.First().Weight.Value.Should().Be(expectedWW,
            $"Week {week}: MROUND({tm} × {intensity}, 2.5) should be {expectedWW}kg");

        plannedSets.Should().HaveCount(4,
            $"Week {week}: Hypertrophy always has 4 sets");

        plannedSets.First().TargetReps.Should().Be(expectedReps,
            $"Week {week}: Reps/Set should be {expectedReps}");
    }

    /// <summary>
    /// Validates weeks 15-21 (Block 3) working weights with TM frozen at 76.86kg.
    /// </summary>
    [Theory]
    [InlineData(15, 0.70, 55.0, 10, 12)]
    [InlineData(16, 0.73, 55.0, 9, 11)]
    [InlineData(17, 0.76, 57.5, 8, 10)]
    [InlineData(18, 0.73, 55.0, 9, 11)]
    [InlineData(19, 0.76, 57.5, 8, 10)]
    [InlineData(20, 0.79, 60.0, 7, 9)]
    [InlineData(21, 0.60, 45.0, 5, null)]  // Deload
    public void OHP_Weeks15To21_NoAmrap_WorkingWeightsShouldMatchSpreadsheet(
        int week, decimal intensity, decimal expectedWW, int expectedReps, int? expectedRepOut)
    {
        // Arrange
        var strategy = SimulateOhpWeeks1Through6();
        var tm = strategy.TrainingMax.Value; // 76.86

        var blockNumber = (week - 1) / 7 + 1;

        // Act
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

        // Assert
        plannedSets.First().Weight.Value.Should().Be(expectedWW,
            $"Week {week}: MROUND({tm} × {intensity}, 2.5) should be {expectedWW}kg");

        plannedSets.Should().HaveCount(4,
            $"Week {week}: Hypertrophy always has 4 sets");

        plannedSets.First().TargetReps.Should().Be(expectedReps,
            $"Week {week}: Reps/Set should be {expectedReps}");
    }

    #endregion

    #region TM Progression With All Delta Scenarios (TM=110kg)

    /// <summary>
    /// Tests all TM adjustment scenarios using TM=110kg with the hypertrophy program.
    /// Covers every delta multiplier: +5(+3%), +2(+1%), +1(+0.5%), +3(+1.5%), +4(+2%), 0, -1(-2%), -3(-5%)
    /// The AMRAP target comes from the hypertrophy rep-out targets.
    /// </summary>
    [Fact]
    public void HighTm_Weeks1To6_AllPositiveDeltas_TmShouldProgress()
    {
        // Arrange: TM=110kg with hypertrophy table
        var tm = TrainingMax.Create(110m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 4);

        // Week 1: RepOut=15, AMRAP=20 → delta=+5 → +3%
        // TM = 110 * 1.03 = 113.30
        var week1Sets = strategy.CalculatePlannedSets(1, 1).ToList();
        week1Sets.Last().TargetReps.Should().Be(15, "Week 1 AMRAP target should be 15 (rep-out)");
        var completed1 = CreateCompletedSets(week1Sets, 12, 20);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week1Sets, completed1));
        strategy.TrainingMax.Value.Should().BeApproximately(113.30m, 0.01m, "Week 1: +5 delta → +3%");

        // Week 2: RepOut=13, AMRAP=17 → delta=+4 → +2%
        // TM = 113.30 * 1.02 = 115.57
        var week2Sets = strategy.CalculatePlannedSets(2, 1).ToList();
        week2Sets.Last().TargetReps.Should().Be(13, "Week 2 AMRAP target should be 13");
        var completed2 = CreateCompletedSets(week2Sets, 11, 17);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week2Sets, completed2));
        strategy.TrainingMax.Value.Should().BeApproximately(115.57m, 0.01m, "Week 2: +4 delta → +2%");

        // Week 3: RepOut=12, AMRAP=15 → delta=+3 → +1.5%
        // TM = 115.57 * 1.015 = 117.30
        var week3Sets = strategy.CalculatePlannedSets(3, 1).ToList();
        week3Sets.Last().TargetReps.Should().Be(12, "Week 3 AMRAP target should be 12");
        var completed3 = CreateCompletedSets(week3Sets, 10, 15);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week3Sets, completed3));
        strategy.TrainingMax.Value.Should().BeApproximately(117.30m, 0.01m, "Week 3: +3 delta → +1.5%");

        // Week 4: RepOut=13, AMRAP=15 → delta=+2 → +1%
        // TM = 117.30 * 1.01 = 118.47
        var week4Sets = strategy.CalculatePlannedSets(4, 1).ToList();
        week4Sets.Last().TargetReps.Should().Be(13, "Week 4 AMRAP target should be 13");
        var completed4 = CreateCompletedSets(week4Sets, 11, 15);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week4Sets, completed4));
        strategy.TrainingMax.Value.Should().BeApproximately(118.47m, 0.01m, "Week 4: +2 delta → +1%");

        // Week 5: RepOut=12, AMRAP=13 → delta=+1 → +0.5%
        // TM = 118.47 * 1.005 = 119.06
        var week5Sets = strategy.CalculatePlannedSets(5, 1).ToList();
        week5Sets.Last().TargetReps.Should().Be(12, "Week 5 AMRAP target should be 12");
        var completed5 = CreateCompletedSets(week5Sets, 10, 13);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week5Sets, completed5));
        strategy.TrainingMax.Value.Should().BeApproximately(119.06m, 0.01m, "Week 5: +1 delta → +0.5%");

        // Week 6: RepOut=11, AMRAP=11 → delta=0 → 0%
        // TM stays 119.06
        var week6Sets = strategy.CalculatePlannedSets(6, 1).ToList();
        week6Sets.Last().TargetReps.Should().Be(11, "Week 6 AMRAP target should be 11");
        var completed6 = CreateCompletedSets(week6Sets, 9, 11);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week6Sets, completed6));
        strategy.TrainingMax.Value.Should().BeApproximately(119.06m, 0.01m, "Week 6: 0 delta → no change");
    }

    /// <summary>
    /// Tests negative delta scenarios: -1 and -2+ (which triggers -5%).
    /// </summary>
    [Fact]
    public void NegativeDeltas_TmShouldDecrease()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 4);

        // Week 1: RepOut=15, AMRAP=14 → delta=-1 → -2%
        // TM = 100 * 0.98 = 98.00
        var week1Sets = strategy.CalculatePlannedSets(1, 1).ToList();
        var completed1 = CreateCompletedSets(week1Sets, 12, 14);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week1Sets, completed1));
        strategy.TrainingMax.Value.Should().Be(98.00m, "delta=-1 → -2%");

        // Week 2: RepOut=13, AMRAP=10 → delta=-3 (2+ under) → -5%
        // TM = 98 * 0.95 = 93.10
        var week2Sets = strategy.CalculatePlannedSets(2, 1).ToList();
        var completed2 = CreateCompletedSets(week2Sets, 11, 10);
        strategy.ApplyPerformanceResult(new ExercisePerformance(_exerciseId, week2Sets, completed2));
        strategy.TrainingMax.Value.Should().Be(93.10m, "delta=-3 → -5%");
    }

    /// <summary>
    /// Week 7 deload: no AMRAP, TM unchanged, 60% intensity, 5 reps, 4 sets.
    /// </summary>
    [Fact]
    public void Deload_Week7_NoAmrap_CorrectParameters()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 4);

        var plannedSets = strategy.CalculatePlannedSets(7, 1).ToList();

        plannedSets.Should().HaveCount(4, "Deload has 4 sets");
        plannedSets.All(s => !s.IsAmrap).Should().BeTrue("Deload has no AMRAP sets");
        plannedSets.First().TargetReps.Should().Be(5, "Deload reps = 5");
        var expectedWW = MRound(100m * 0.60m, 2.5m); // = 60
        plannedSets.First().Weight.Value.Should().Be(expectedWW, "Deload intensity = 60%");
    }

    #endregion

    #region TM Adjustment Delta Table — Exhaustive Validation From Spreadsheet

    /// <summary>
    /// Every single TM adjustment multiplier from the spreadsheet scenario summary.
    /// This validates the delta → percentage mapping is correct.
    /// </summary>
    [Theory]
    [InlineData(110.00, 10, 15, 5,  1.03,  113.30)]  // +5 over → ×1.03
    [InlineData(113.30, 8,  10, 2,  1.01,  114.43)]  // +2 over → ×1.01
    [InlineData(117.90, 5,  6,  1,  1.005, 118.49)]  // +1 over → ×1.005
    [InlineData(118.49, 8,  11, 3,  1.015, 120.27)]  // +3 over → ×1.015
    [InlineData(120.27, 6,  10, 4,  1.02,  122.67)]  // +4 over → ×1.02
    [InlineData(122.67, 4,  4,  0,  1.0,   122.67)]  // same    → ×1.0
    [InlineData(122.67, 7,  6,  -1, 0.98,  120.22)]  // -1 under → ×0.98
    [InlineData(120.22, 5,  2,  -3, 0.95,  114.21)]  // 2+ under → ×0.95
    public void TmAdjustment_AllMultipliers_ShouldMatchSpreadsheetScenarios(
        double tmBeforeD, int repOutTarget, int amrapResult, int expectedDelta,
        double multiplierD, double expectedTmAfterD)
    {
        // Arrange
        var tmBefore = (decimal)tmBeforeD;
        var multiplier = (decimal)multiplierD;
        var expectedTmAfter = (decimal)expectedTmAfterD;

        var tm = TrainingMax.Create(tmBefore, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 5);

        // Use week 1 as a vehicle - we just need planned sets with the right target reps
        var plannedSets = strategy.CalculatePlannedSets(1, 1).ToList();
        var targetRepsFromCode = plannedSets.Last().TargetReps;

        // Override: we want the AMRAP delta to be calculated against repOutTarget
        // Create planned sets manually with the correct rep-out target
        var manualPlannedSets = new List<PlannedSet>();
        for (int i = 1; i <= plannedSets.Count; i++)
        {
            bool isAmrap = i == plannedSets.Count;
            manualPlannedSets.Add(new PlannedSet(i, plannedSets[0].Weight, repOutTarget, isAmrap));
        }

        var completedSets = CreateCompletedSets(manualPlannedSets, repOutTarget, amrapResult);
        var performance = new ExercisePerformance(_exerciseId, manualPlannedSets, completedSets);

        // Verify delta
        performance.GetAmrapDelta().Should().Be(expectedDelta,
            $"Delta should be {amrapResult} - {repOutTarget} = {expectedDelta}");

        // Act
        strategy.ApplyPerformanceResult(performance);

        // Assert: TM = tmBefore × multiplier, rounded to 2 decimal places
        var manualCalc = Math.Round(tmBefore * multiplier, 2);
        strategy.TrainingMax.Value.Should().Be(manualCalc,
            $"TM should be {tmBefore} × {multiplier} = {manualCalc}");

        strategy.TrainingMax.Value.Should().BeApproximately(expectedTmAfter, 0.01m,
            $"TM should match spreadsheet value {expectedTmAfter}");
    }

    #endregion

    #region Working Weight Calculation — Rounding Validation

    /// <summary>
    /// Validates that working weight calculation with 2.5kg rounding matches
    /// expectations. TrainingMax.CalculateWorkingWeight always uses 2.5kg for kg.
    ///
    /// The spreadsheet uses exercise-specific rounding (OHP=2.5kg, Squat=5kg),
    /// but the code uses a fixed 2.5kg. This test documents the discrepancy.
    /// </summary>
    [Theory]
    [InlineData(65.00,  0.65, 42.5)]   // OHP Week 1: 65×0.65=42.25 → 42.5 (2.5kg rounding)
    [InlineData(66.30,  0.68, 45.0)]   // OHP Week 2: 66.3×0.68=45.084 → 45.0
    [InlineData(76.86,  0.60, 47.5)]   // OHP Deload: 76.86×0.60=46.116 → 47.5 (2.5) vs 45.0 (5kg)
    [InlineData(76.86,  0.68, 52.5)]   // OHP Week 8: 76.86×0.68=52.2648 → 52.5
    [InlineData(76.86,  0.70, 55.0)]   // OHP Week 9: 76.86×0.70=53.802 → 55.0 (2.5) vs 55.0 (5kg)
    [InlineData(76.86,  0.73, 55.0)]   // OHP Week 10: 76.86×0.73=56.1078 → 57.5 (2.5) vs 55.0 (5kg)!
    [InlineData(76.86,  0.76, 57.5)]   // OHP Week 13: 76.86×0.76=58.4136 → 57.5 (2.5) vs 60.0 (5kg)!
    [InlineData(76.86,  0.79, 60.0)]   // OHP Week 20: 76.86×0.79=60.7194 → 60.0 (2.5) vs 60.0 (5kg)
    public void WorkingWeight_With2_5kgRounding_ShouldMatchMROUND(
        double tmD, double intensityD, double expectedWwD)
    {
        var tmValue = (decimal)tmD;
        var intensity = (decimal)intensityD;
        var expectedWW = (decimal)expectedWwD;

        var tm = TrainingMax.Create(tmValue, WeightUnit.Kilograms);
        var weight = tm.CalculateWorkingWeight(intensity);

        var manual = MRound(tmValue * intensity, 2.5m);
        weight.Value.Should().Be(manual,
            $"CalculateWorkingWeight({tmValue} × {intensity}) should be MROUND({tmValue * intensity}, 2.5) = {manual}");
    }

    /// <summary>
    /// Documents which OHP weeks produce DIFFERENT results with 2.5kg vs 5kg rounding.
    /// The spreadsheet uses 2.5kg for OHP. If the code is correct for OHP (2.5kg),
    /// it will be wrong for Smith Squat (5kg rounding).
    /// </summary>
    [Theory]
    [InlineData(76.86, 0.60, 47.5, 45.0)]   // Deload: 2.5→47.5 vs 5→45.0 ← DIFFERENT
    [InlineData(76.86, 0.73, 57.5, 55.0)]   // Week 10: 2.5→57.5 vs 5→55.0 ← DIFFERENT
    [InlineData(76.86, 0.76, 57.5, 60.0)]   // Week 13: 2.5→57.5 vs 5→60.0 ← DIFFERENT
    public void WorkingWeight_RoundingDiscrepancy_2_5kg_vs_5kg(
        double tmD, double intensityD, double ww2_5D, double ww5D)
    {
        var tmValue = (decimal)tmD;
        var intensity = (decimal)intensityD;

        var rounded2_5 = MRound(tmValue * intensity, 2.5m);
        var rounded5 = MRound(tmValue * intensity, 5m);

        rounded2_5.Should().Be((decimal)ww2_5D, "2.5kg rounding");
        rounded5.Should().Be((decimal)ww5D, "5kg rounding");
        rounded2_5.Should().NotBe(rounded5,
            "This documents cases where rounding increment matters");
    }

    #endregion

    #region Intensity Table Validation — Code Matches Spreadsheet

    /// <summary>
    /// Verifies the code's WeeklyProgram now matches the A2S2 Hypertrophy spreadsheet
    /// for all 21 weeks: intensity, sets, reps/set, and rep-out target.
    /// </summary>
    [Theory]
    // Block 1
    [InlineData(1,  0.65, 4, 12, 15)]
    [InlineData(2,  0.68, 4, 11, 13)]
    [InlineData(3,  0.70, 4, 10, 12)]
    [InlineData(4,  0.68, 4, 11, 13)]
    [InlineData(5,  0.70, 4, 10, 12)]
    [InlineData(6,  0.73, 4, 9,  11)]
    [InlineData(7,  0.60, 4, 5,  0)]  // Deload (0 = no rep-out target)
    // Block 2
    [InlineData(8,  0.68, 4, 11, 13)]
    [InlineData(9,  0.70, 4, 10, 12)]
    [InlineData(10, 0.73, 4, 9,  11)]
    [InlineData(11, 0.70, 4, 10, 12)]
    [InlineData(12, 0.73, 4, 9,  11)]
    [InlineData(13, 0.76, 4, 8,  10)]
    [InlineData(14, 0.60, 4, 5,  0)]  // Deload
    // Block 3
    [InlineData(15, 0.70, 4, 10, 12)]
    [InlineData(16, 0.73, 4, 9,  11)]
    [InlineData(17, 0.76, 4, 8,  10)]
    [InlineData(18, 0.73, 4, 9,  11)]
    [InlineData(19, 0.76, 4, 8,  10)]
    [InlineData(20, 0.79, 4, 7,  9)]
    [InlineData(21, 0.60, 4, 5,  0)]  // Deload
    public void IntensityTable_CodeMatchesSpreadsheet(
        int week,
        double expectedIntensityD, int expectedSets, int expectedReps, int expectedRepOut)
    {
        var expectedIntensity = (decimal)expectedIntensityD;
        var isDeload = week == 7 || week == 14 || week == 21;

        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true);
        var blockNumber = (week - 1) / 7 + 1;
        var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();

        // Verify sets
        plannedSets.Should().HaveCount(expectedSets,
            $"Week {week}: should have {expectedSets} sets");

        // Verify intensity via working weight
        plannedSets.First().Weight.Value.Should().Be(MRound(100m * expectedIntensity, 2.5m),
            $"Week {week}: weight should reflect {expectedIntensity * 100}% intensity");

        // Verify normal set reps
        plannedSets.First().TargetReps.Should().Be(expectedReps,
            $"Week {week}: normal set reps should be {expectedReps}");

        if (!isDeload)
        {
            // AMRAP set should use rep-out target
            plannedSets.Last().IsAmrap.Should().BeTrue(
                $"Week {week}: last set should be AMRAP");
            plannedSets.Last().TargetReps.Should().Be(expectedRepOut,
                $"Week {week}: AMRAP set should have rep-out target {expectedRepOut}");
        }
        else
        {
            // Deload: no AMRAP
            plannedSets.Last().IsAmrap.Should().BeFalse(
                $"Week {week}: deload should not have AMRAP");
        }

        // Cross-validate with our reference data
        HypertrophyProgram[week].Intensity.Should().Be(expectedIntensity);
        HypertrophyProgram[week].Sets.Should().Be(expectedSets);
        HypertrophyProgram[week].RepsPerSet.Should().Be(expectedReps);
    }

    #endregion

    #region Rep-out Target vs Reps/Set — The AMRAP Baseline Bug

    /// <summary>
    /// The spreadsheet has TWO distinct rep columns:
    ///   - Reps/Set: what you do for each normal set
    ///   - Rep-out Target: the baseline for AMRAP delta calculation
    ///
    /// The current code uses a single TargetReps for both, which is wrong
    /// for the hypertrophy variant where Rep-out Target = Reps/Set + 2 (or +3 for week 1).
    ///
    /// This test validates the relationship between the two.
    /// </summary>
    [Theory]
    [InlineData(1,  12, 15, 3)]   // Week 1: +3 offset (only week with +3)
    [InlineData(2,  11, 13, 2)]
    [InlineData(3,  10, 12, 2)]
    [InlineData(4,  11, 13, 2)]
    [InlineData(5,  10, 12, 2)]
    [InlineData(6,  9,  11, 2)]
    [InlineData(8,  11, 13, 2)]
    [InlineData(9,  10, 12, 2)]
    [InlineData(10, 9,  11, 2)]
    [InlineData(11, 10, 12, 2)]
    [InlineData(12, 9,  11, 2)]
    [InlineData(13, 8,  10, 2)]
    [InlineData(15, 10, 12, 2)]
    [InlineData(16, 9,  11, 2)]
    [InlineData(17, 8,  10, 2)]
    [InlineData(18, 9,  11, 2)]
    [InlineData(19, 8,  10, 2)]
    [InlineData(20, 7,  9,  2)]
    public void HypertrophyRepOutTarget_ShouldBeRepsPerSetPlus2Or3(
        int week, int expectedRepsPerSet, int expectedRepOutTarget, int expectedOffset)
    {
        var program = HypertrophyProgram[week];

        program.RepsPerSet.Should().Be(expectedRepsPerSet,
            $"Week {week}: Reps/Set");
        program.RepOutTarget.Should().Be(expectedRepOutTarget,
            $"Week {week}: Rep-out Target");

        var actualOffset = program.RepOutTarget!.Value - program.RepsPerSet;
        actualOffset.Should().Be(expectedOffset,
            $"Week {week}: Rep-out Target should be Reps/Set + {expectedOffset}");
    }

    /// <summary>
    /// Deload weeks have no rep-out target (no AMRAP).
    /// </summary>
    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void HypertrophyDeloadWeeks_ShouldHaveNoRepOutTarget(int week)
    {
        HypertrophyProgram[week].RepOutTarget.Should().BeNull(
            $"Week {week} is a deload: no AMRAP, no rep-out target");
        HypertrophyProgram[week].RepsPerSet.Should().Be(5,
            $"Week {week} deload: 5 reps per set");
        HypertrophyProgram[week].Intensity.Should().Be(0.60m,
            $"Week {week} deload: 60% intensity");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates completed sets for a performance, with normal sets at reps/set
    /// and the last set as AMRAP with the specified result.
    /// </summary>
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

    /// <summary>
    /// Simulates OHP weeks 1-6 (all AMRAP=19) to get TM to ~76.86kg.
    /// </summary>
    private LinearProgressionStrategy SimulateOhpWeeks1Through6()
    {
        var tm = TrainingMax.Create(65m, WeightUnit.Kilograms);
        var strategy = LinearProgressionStrategy.Create(tm, useAmrap: true, baseSetsPerExercise: 4);

        var amrapResults = new[] { 19, 19, 19, 19, 19, 19 };
        // Rep-out targets from the hypertrophy table
        var repOutTargets = new[] { 15, 13, 12, 13, 12, 11 };

        for (int week = 1; week <= 6; week++)
        {
            var blockNumber = 1;
            var plannedSets = strategy.CalculatePlannedSets(week, blockNumber).ToList();
            var completedSets = CreateCompletedSets(
                plannedSets,
                HypertrophyProgram[week].RepsPerSet,
                amrapResults[week - 1]);
            var performance = new ExercisePerformance(_exerciseId, plannedSets, completedSets);
            strategy.ApplyPerformanceResult(performance);
        }

        return strategy;
    }


    #endregion
}
