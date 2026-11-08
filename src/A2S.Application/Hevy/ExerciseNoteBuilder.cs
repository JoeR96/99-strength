using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Hevy;

/// <summary>
/// The per-exercise note a Hevy routine carries: training max, block and intensity for a linear lift,
/// set count and rep range for a reps-per-set accessory, the rep target for minimal sets. Shared by
/// the legacy <c>SyncRoutineToHevy</c> command and the Phase 6 <see cref="RoutineBuilder"/>.
/// </summary>
public static class ExerciseNoteBuilder
{
    public static string Build(Exercise exercise, WeekParameters weekParams, IReadOnlyList<PlannedSet> plannedSets)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(weekParams);
        ArgumentNullException.ThrowIfNull(plannedSets);

        var data = exercise.Progression.GetProgressionData();
        var trainingMax = exercise.Progression.GetTrainingMax();
        var noteParts = new List<string>();

        if (trainingMax != null)
        {
            // Linear progression
            noteParts.Add($"TM: {trainingMax}");
            if (weekParams.IsDeload)
            {
                noteParts.Add("DELOAD");
            }
            else
            {
                var blockPhase = weekParams.BlockNumber switch
                {
                    1 => BlockDescriptions.Volume,
                    2 => BlockDescriptions.Intensity,
                    3 => BlockDescriptions.Peaking,
                    _ => ""
                };
                noteParts.Add($"Block {weekParams.BlockNumber} - {blockPhase}");
            }
            noteParts.Add($"{weekParams.IntensityPercentage:0}% × {weekParams.Sets} sets × {weekParams.TargetReps} reps");
            if (data.UseAmrap == true)
            {
                noteParts.Add($"AMRAP last set (target: {plannedSets.LastOrDefault()?.TargetReps ?? 0}+)");
            }
        }
        else if (data.RepRangeMinimum.HasValue && data.RepRangeMaximum.HasValue)
        {
            // RepsPerSet progression
            noteParts.Add($"Sets: {data.CurrentSetCount}/{data.TargetSets}");
            noteParts.Add($"Rep range: {data.RepRangeMinimum}-{data.RepRangeMaximum} (hit {data.RepRangeMaximum} to progress)");
            if (exercise.Progression.IsUnilateral)
            {
                noteParts.Add("Unilateral");
            }
        }
        else if (data.TargetTotalReps.HasValue)
        {
            // MinimalSets progression
            noteParts.Add($"Target: {data.TargetTotalReps} reps in {data.CurrentSetCount} sets (range: {data.MinimumSets}-{data.MaximumSets}) | Complete in fewer sets to progress");
        }

        return string.Join(" | ", noteParts);
    }

    private static class BlockDescriptions
    {
        public const string Volume = "Volume phase";
        public const string Intensity = "Intensity phase";
        public const string Peaking = "Peaking phase";
    }
}
