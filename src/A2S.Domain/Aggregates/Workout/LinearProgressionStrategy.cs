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
    /// Intensity and reps vary by week/block according to A2S program structure.
    /// </summary>
    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        CheckRule(weekNumber >= 1 && weekNumber <= 21, "Week number must be between 1 and 21");
        CheckRule(blockNumber >= 1 && blockNumber <= 3, "Block number must be between 1 and 3");

        var intensity = GetIntensityPercentage(weekNumber, blockNumber);
        var targetReps = GetTargetReps(weekNumber, blockNumber);
        var workingWeight = TrainingMax.CalculateWorkingWeight(intensity);

        var sets = new List<PlannedSet>();
        for (int i = 1; i <= BaseSetsPerExercise; i++)
        {
            bool isAmrap = UseAmrap && i == BaseSetsPerExercise;
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
    /// Gets intensity percentage based on week and block.
    /// Intensity increases as blocks progress (Block 1 < Block 2 < Block 3).
    /// </summary>
    /// <remarks>
    /// Simplified intensity scheme for A2S 2.0:
    /// - Block 1 (weeks 1-7): 70-75% intensity
    /// - Block 2 (weeks 8-14): 75-80% intensity
    /// - Block 3 (weeks 15-21): 80-85% intensity
    /// - Deload weeks (7, 14, 21): Reduced intensity
    /// </remarks>
    private static decimal GetIntensityPercentage(int weekNumber, int blockNumber)
    {
        // Check if it's a deload week
        bool isDeloadWeek = weekNumber % 7 == 0;
        if (isDeloadWeek)
        {
            // Deload: ~65-70% intensity
            return 0.65m;
        }

        // Base intensity by block
        var baseIntensity = blockNumber switch
        {
            1 => 0.70m,  // Block 1: 70% base
            2 => 0.75m,  // Block 2: 75% base
            3 => 0.80m,  // Block 3: 80% base
            _ => throw new ArgumentException($"Invalid block number: {blockNumber}")
        };

        // Add progressive overload within the block (0-5% increase over 6 weeks)
        var weekInBlock = ((weekNumber - 1) % 7) + 1;
        var weeklyIncrease = (weekInBlock - 1) * 0.01m;

        return baseIntensity + weeklyIncrease;
    }

    /// <summary>
    /// Gets target reps based on week and block.
    /// Reps decrease as blocks progress (Block 1 > Block 2 > Block 3).
    /// </summary>
    /// <remarks>
    /// Simplified rep scheme for A2S 2.0:
    /// - Block 1 (weeks 1-7): 8-10 reps
    /// - Block 2 (weeks 8-14): 6-8 reps
    /// - Block 3 (weeks 15-21): 4-6 reps
    /// - Deload weeks: Same reps but lower intensity
    /// </remarks>
    private static int GetTargetReps(int weekNumber, int blockNumber)
    {
        // Check if it's a deload week
        bool isDeloadWeek = weekNumber % 7 == 0;

        // Base reps by block
        var baseReps = blockNumber switch
        {
            1 => 10,  // Block 1: Higher reps (8-10)
            2 => 7,   // Block 2: Medium reps (6-8)
            3 => 5,   // Block 3: Lower reps (4-6)
            _ => throw new ArgumentException($"Invalid block number: {blockNumber}")
        };

        // Deload weeks use same rep ranges
        if (isDeloadWeek)
        {
            return baseReps;
        }

        // Slight variation within block (±1 rep based on week)
        var weekInBlock = ((weekNumber - 1) % 7) + 1;
        var repAdjustment = weekInBlock switch
        {
            1 or 2 => 1,   // First two weeks: slightly higher reps
            3 or 4 or 5 => 0,  // Middle weeks: base reps
            6 => -1,       // Week 6: slightly lower reps (before deload)
            _ => 0
        };

        return baseReps + repAdjustment;
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
