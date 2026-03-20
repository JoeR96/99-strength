using A2S.Domain.Common;
using A2S.Domain.Events;
using A2S.Domain.Services;
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

        var weekData = A2SHypertrophyProgram.GetWeekData(weekNumber);
        var workingWeight = TrainingMax.CalculateWorkingWeight(weekData.Intensity);
        var setsForWeek = weekData.Sets;

        var sets = new List<PlannedSet>();
        for (int i = 1; i <= setsForWeek; i++)
        {
            bool isAmrap = UseAmrap && i == setsForWeek && !A2SHypertrophyProgram.IsDeloadWeek(weekNumber);
            // AMRAP set uses RepOutTarget for correct delta calculation;
            // normal sets and deload sets use RepsPerSet.
            var reps = (isAmrap && weekData.RepOutTarget.HasValue)
                ? weekData.RepOutTarget.Value
                : weekData.RepsPerSet;
            sets.Add(new PlannedSet(i, workingWeight, reps, isAmrap));
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
            // No AMRAP planned (deload week) — TM carries forward unchanged
            return;
        }

        var amrapCompleted = performance.CompletedSets.LastOrDefault(s => s.WasAmrap);
        if (amrapCompleted == null)
        {
            // No AMRAP completed — TM carries forward unchanged
            return;
        }

        // Calculate delta and adjustment
        var delta = performance.GetAmrapDelta();
        var adjustment = AmrapDeltaTable.GetAdjustment(delta);

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
    /// Updates the Training Max to a new value.
    /// Used for manual TM adjustments by the user.
    /// </summary>
    public void UpdateTrainingMax(TrainingMax newTrainingMax, string? reason = null)
    {
        TrainingMax = newTrainingMax;

        // Note: Domain events are raised by the Workout aggregate root, not by child entities
        // The Exercise/Workout will handle raising the TrainingMaxAdjusted event
    }

    internal void RestoreState(TrainingMax trainingMax)
    {
        TrainingMax = trainingMax;
    }
}
