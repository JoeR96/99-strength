using A2S.Domain.Common;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Linear progression strategy for main and auxiliary lifts.
/// Uses Training Max (TM) with percentage-based loading.
/// Implements RTF (Reps To Failure) progression algorithm from A2S 2.0.
/// </summary>
/// <remarks>
/// Reference: research/business-rules.md Section 3.1 "RTF Progression"
/// AMRAP delta table at lines 80-91 defines TM adjustment percentages.
/// </remarks>
public sealed class LinearProgressionStrategy : ExerciseProgression
{
    public TrainingMax TrainingMax { get; private set; }
    public bool UseAmrap { get; private set; }
    public int BaseSetsPerExercise { get; private set; }

    // EF Core constructor
    private LinearProgressionStrategy()
    {
        TrainingMax = null!;
    }

    private LinearProgressionStrategy(
        ExerciseProgressionId id,
        TrainingMax trainingMax,
        bool useAmrap,
        int baseSetsPerExercise)
        : base(id, "Linear")
    {
        CheckRule(baseSetsPerExercise >= 3 && baseSetsPerExercise <= 8,
            "Base sets must be between 3 and 8");

        TrainingMax = trainingMax;
        UseAmrap = useAmrap;
        BaseSetsPerExercise = baseSetsPerExercise;
    }

    public static LinearProgressionStrategy Create(
        TrainingMax trainingMax,
        bool useAmrap = true,
        int baseSetsPerExercise = 4)
    {
        return new LinearProgressionStrategy(
            new ExerciseProgressionId(Guid.NewGuid()),
            trainingMax,
            useAmrap,
            baseSetsPerExercise);
    }

    /// <summary>
    /// Calculates planned sets for a given week and block.
    /// Intensity, sets, and reps are taken directly from the A2S Hypertrophy spreadsheet.
    /// Normal sets use RepsPerSet; the last (AMRAP) set uses the Rep-out Target
    /// so that delta = actualReps - repOutTarget is calculated correctly.
    /// </summary>
    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        CheckRule(weekNumber >= 1 && weekNumber <= 21, "Week number must be between 1 and 21");
        CheckRule(blockNumber >= 1 && blockNumber <= 3, "Block number must be between 1 and 3");

        var weekData = WeeklyProgram[weekNumber];
        var workingWeight = TrainingMax.CalculateWorkingWeight(weekData.Intensity);
        var setsForWeek = weekData.Sets;

        var sets = new List<PlannedSet>();
        for (int i = 1; i <= setsForWeek; i++)
        {
            bool isAmrap = UseAmrap && i == setsForWeek && !IsDeloadWeek(weekNumber);
            // AMRAP set uses RepOutTarget for correct delta calculation;
            // normal sets and deload sets use RepsPerSet.
            var reps = (isAmrap && weekData.RepOutTarget.HasValue)
                ? weekData.RepOutTarget.Value
                : weekData.RepsPerSet;
            sets.Add(new PlannedSet(i, workingWeight, reps, isAmrap));
        }

        return sets;
    }

    private static bool IsDeloadWeek(int weekNumber)
        => weekNumber == 7 || weekNumber == 14 || weekNumber == 21;

    /// <summary>
    /// Applies performance results and adjusts Training Max based on AMRAP performance.
    /// Implements the RTF progression algorithm from business-rules.md lines 80-91.
    /// </summary>
    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        if (!UseAmrap)
        {
            // No TM adjustment for non-AMRAP exercises
            return;
        }

        // Validate AMRAP set was completed
        var amrapPlanned = performance.PlannedSets.LastOrDefault(s => s.IsAmrap);
        if (amrapPlanned == null)
        {
            throw new InvalidOperationException(
                "Cannot apply progression without an AMRAP set in planned sets");
        }

        var amrapCompleted = performance.CompletedSets.LastOrDefault(s => s.WasAmrap);
        if (amrapCompleted == null)
        {
            throw new InvalidOperationException(
                "Cannot apply progression without an AMRAP set in completed sets");
        }

        // Calculate delta and adjustment
        var delta = performance.GetAmrapDelta();
        var adjustment = CalculateAdjustmentFromDelta(delta);
        var previousTm = TrainingMax;

        TrainingMax = TrainingMax.ApplyAdjustment(adjustment);

        // Note: Domain events are raised by the Workout aggregate root, not by child entities
        // The Exercise/Workout will handle raising the TrainingMaxAdjusted event
    }

    /// <summary>
    /// Gets a summary of current progression state for UI display.
    /// </summary>
    public override ProgressionSummary GetSummary()
    {
        return new ProgressionSummary
        {
            Type = "Linear (RTF)",
            Details = new Dictionary<string, string>
            {
                ["Training Max"] = TrainingMax.ToString(),
                ["Uses AMRAP"] = UseAmrap ? "Yes" : "No",
                ["Sets per Exercise"] = BaseSetsPerExercise.ToString()
            }
        };
    }

    /// <summary>
    /// Calculates TM adjustment based on AMRAP delta.
    /// Reference: business-rules.md lines 80-91 (AMRAP delta table).
    /// </summary>
    /// <remarks>
    /// Progression Table:
    /// +5 or more: +3.0%
    /// +4: +2.0%
    /// +3: +1.5%
    /// +2: +1.0%
    /// +1: +0.5%
    /// 0: 0% (no change)
    /// -1: -2.0%
    /// -2 or worse: -5.0%
    /// </remarks>
    private static TrainingMaxAdjustment CalculateAdjustmentFromDelta(int delta)
    {
        return delta switch
        {
            >= 5 => TrainingMaxAdjustment.Percentage(0.03m),     // +3.0%
            4 => TrainingMaxAdjustment.Percentage(0.02m),        // +2.0%
            3 => TrainingMaxAdjustment.Percentage(0.015m),       // +1.5%
            2 => TrainingMaxAdjustment.Percentage(0.01m),        // +1.0%
            1 => TrainingMaxAdjustment.Percentage(0.005m),       // +0.5%
            0 => TrainingMaxAdjustment.None,                     // 0%
            -1 => TrainingMaxAdjustment.Percentage(-0.02m),      // -2.0%
            _ => TrainingMaxAdjustment.Percentage(-0.05m)        // -5.0% for -2 or worse
        };
    }

    /// <summary>
    /// Week-by-week programming data matching the A2S2 Hypertrophy spreadsheet exactly.
    /// Format: (Intensity%, Sets, RepsPerSet, RepOutTarget)
    /// </summary>
    /// <remarks>
    /// Source: A2S2 Hypertrophy spreadsheet (validated against A2S2_Validation_Data.md)
    ///
    /// Hypertrophy uses 6 intensity levels: a=0.65, b=0.68, c=0.70, d=0.73, e=0.76, f=0.79
    /// mapped to 6 rep levels: 12, 11, 10, 9, 8, 7.
    /// Rep-out target = RepsPerSet + 2 (except week 1 which is +3).
    /// Always 4 sets. Deload weeks at 60% intensity, 5 reps, no AMRAP.
    ///
    /// Block structure (3-week mini-cycles, overlapping):
    ///   Block 1: MC1(a,b,c) MC2(b,c,d) Deload
    ///   Block 2: MC1(b,c,d) MC2(c,d,e) Deload
    ///   Block 3: MC1(c,d,e) MC2(d,e,f) Deload
    /// </remarks>
    private record struct WeekData(decimal Intensity, int Sets, int RepsPerSet, int? RepOutTarget);

    private static readonly WeekData[] WeeklyProgram = new[]
    {
        // Week 0 placeholder (1-indexed access)
        new WeekData(0.00m, 0, 0, null),

        // BLOCK 1: Weeks 1-7
        new WeekData(0.65m, 4, 12, 15),  // Week 1
        new WeekData(0.68m, 4, 11, 13),  // Week 2
        new WeekData(0.70m, 4, 10, 12),  // Week 3
        new WeekData(0.68m, 4, 11, 13),  // Week 4
        new WeekData(0.70m, 4, 10, 12),  // Week 5
        new WeekData(0.73m, 4,  9, 11),  // Week 6
        new WeekData(0.60m, 4,  5, null), // Week 7 - DELOAD (no AMRAP)

        // BLOCK 2: Weeks 8-14
        new WeekData(0.68m, 4, 11, 13),  // Week 8
        new WeekData(0.70m, 4, 10, 12),  // Week 9
        new WeekData(0.73m, 4,  9, 11),  // Week 10
        new WeekData(0.70m, 4, 10, 12),  // Week 11
        new WeekData(0.73m, 4,  9, 11),  // Week 12
        new WeekData(0.76m, 4,  8, 10),  // Week 13
        new WeekData(0.60m, 4,  5, null), // Week 14 - DELOAD

        // BLOCK 3: Weeks 15-21
        new WeekData(0.70m, 4, 10, 12),  // Week 15
        new WeekData(0.73m, 4,  9, 11),  // Week 16
        new WeekData(0.76m, 4,  8, 10),  // Week 17
        new WeekData(0.73m, 4,  9, 11),  // Week 18
        new WeekData(0.76m, 4,  8, 10),  // Week 19
        new WeekData(0.79m, 4,  7,  9),  // Week 20
        new WeekData(0.60m, 4,  5, null), // Week 21 - DELOAD (final week)
    };

    /// <summary>
    /// Gets the number of sets for a given week.
    /// In hypertrophy, always 4.
    /// </summary>
    public static int GetSetsForWeek(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].Sets;
    }

    /// <summary>
    /// Gets the rep-out target (AMRAP baseline) for a given week.
    /// Returns null for deload weeks.
    /// </summary>
    public static int? GetRepOutTarget(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].RepOutTarget;
    }

    /// <summary>
    /// Gets the reps per set (for normal sets) for a given week.
    /// </summary>
    public static int GetRepsPerSet(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].RepsPerSet;
    }

    /// <summary>
    /// Gets the intensity percentage for a given week.
    /// </summary>
    public static decimal GetIntensity(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].Intensity;
    }

    /// <summary>
    /// Updates the Training Max to a new value.
    /// Used for manual TM adjustments by the user.
    /// </summary>
    public void UpdateTrainingMax(TrainingMax newTrainingMax, string? reason = null)
    {
        var previousTm = TrainingMax;
        TrainingMax = newTrainingMax;

        // Note: Domain events are raised by the Workout aggregate root, not by child entities
        // The Exercise/Workout will handle raising the TrainingMaxAdjusted event
    }

    internal void RestoreState(TrainingMax trainingMax)
    {
        TrainingMax = trainingMax;
    }
}
