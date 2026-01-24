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
    /// Intensity, sets, and reps are taken directly from the A2S spreadsheet.
    /// </summary>
    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        CheckRule(weekNumber >= 1 && weekNumber <= 21, "Week number must be between 1 and 21");
        CheckRule(blockNumber >= 1 && blockNumber <= 3, "Block number must be between 1 and 3");

        var intensity = GetIntensityPercentage(weekNumber, blockNumber);
        var targetReps = GetTargetReps(weekNumber, blockNumber);
        var setsForWeek = GetSetsForWeek(weekNumber);
        var workingWeight = TrainingMax.CalculateWorkingWeight(intensity);

        var sets = new List<PlannedSet>();
        for (int i = 1; i <= setsForWeek; i++)
        {
            bool isAmrap = UseAmrap && i == setsForWeek;
            sets.Add(new PlannedSet(i, workingWeight, targetReps, isAmrap));
        }

        return sets;
    }

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
    /// Week-by-week programming data matching the A2S 2024-2025 spreadsheet exactly.
    /// Format: (Intensity%, Sets, TargetReps)
    /// </summary>
    /// <remarks>
    /// Source: A2S 2024-2025 - Program (1).csv, Smith Squat row (line 22)
    ///
    /// BLOCK 1 (Weeks 1-7): Volume accumulation phase
    /// - 3-week mini-cycles: 75→85→90%, then 80→85→90%
    /// - Week 7: Deload at 65%
    ///
    /// BLOCK 2 (Weeks 8-14): Intensity building phase
    /// - 3-week mini-cycles: 85→90→95%, then 85→90→95%
    /// - Week 14: Deload at 65%
    ///
    /// BLOCK 3 (Weeks 15-21): Peak/realization phase
    /// - 3-week mini-cycles: 90→95→100%, then 95→100→105%
    /// - Week 21: Deload at 65%
    /// </remarks>
    private static readonly (decimal Intensity, int Sets, int Reps)[] WeeklyProgram = new[]
    {
        // Week 0 placeholder (1-indexed access)
        (0.00m, 0, 0),

        // BLOCK 1: Weeks 1-7
        (0.75m, 5, 10),  // Week 1
        (0.85m, 4, 8),   // Week 2
        (0.90m, 3, 6),   // Week 3
        (0.80m, 5, 9),   // Week 4
        (0.85m, 4, 7),   // Week 5
        (0.90m, 3, 5),   // Week 6
        (0.65m, 5, 10),  // Week 7 - DELOAD (reps n/a in spreadsheet, using base)

        // BLOCK 2: Weeks 8-14
        (0.85m, 4, 8),   // Week 8
        (0.90m, 3, 6),   // Week 9
        (0.95m, 2, 4),   // Week 10
        (0.85m, 4, 7),   // Week 11
        (0.90m, 3, 5),   // Week 12
        (0.95m, 2, 3),   // Week 13
        (0.65m, 5, 10),  // Week 14 - DELOAD

        // BLOCK 3: Weeks 15-21
        (0.90m, 3, 6),   // Week 15
        (0.95m, 2, 4),   // Week 16
        (1.00m, 1, 2),   // Week 17
        (0.95m, 2, 4),   // Week 18
        (1.00m, 1, 2),   // Week 19
        (1.05m, 1, 2),   // Week 20
        (0.65m, 5, 10),  // Week 21 - DELOAD (final week)
    };

    /// <summary>
    /// Gets intensity percentage based on week number.
    /// Uses exact values from the A2S spreadsheet.
    /// </summary>
    private static decimal GetIntensityPercentage(int weekNumber, int blockNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].Intensity;
    }

    /// <summary>
    /// Gets target reps based on week number.
    /// Uses exact values from the A2S spreadsheet.
    /// </summary>
    private static int GetTargetReps(int weekNumber, int blockNumber)
    {
        if (weekNumber < 1 || weekNumber > 21)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber),
                $"Week number must be between 1 and 21, got {weekNumber}");
        }

        return WeeklyProgram[weekNumber].Reps;
    }

    /// <summary>
    /// Gets the number of sets for a given week.
    /// Uses exact values from the A2S spreadsheet.
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
}
