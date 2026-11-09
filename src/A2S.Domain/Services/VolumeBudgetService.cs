using A2S.Domain.Aggregates.Workout;

namespace A2S.Domain.Services;

/// <summary>
/// Counts planned hard sets per muscle-group bucket for one program week and compares
/// them with the weekly caps (brief § 2 "Training volume budget").
/// Reads the <see cref="Workout"/> only; A2S2 progression maths are untouched.
/// </summary>
public sealed class VolumeBudgetService
{
    private readonly IA2SProgramProvider _program;

    public VolumeBudgetService(IA2SProgramProvider program)
    {
        _program = program ?? throw new ArgumentNullException(nameof(program));
    }

    /// <summary>
    /// Planned hard sets per bucket for <paramref name="weekNumber"/> across every exercise in
    /// the workout. Each planned working set counts 1.0 to the exercise's primary bucket and 0.5
    /// to each secondary bucket; totals are rounded half-up per bucket at the end.
    /// Deload weeks naturally count the deload set count because the exercise's own
    /// <see cref="Exercise.CalculatePlannedSets"/> is used.
    /// </summary>
    /// <param name="lookup">
    /// Resolves an exercise's <see cref="Exercise.ExternalTemplateId"/> (tried first) or
    /// <see cref="Exercise.Name"/> to Hevy muscle groups; null when unknown, in which case the
    /// exercise is skipped.
    /// </param>
    public IReadOnlyDictionary<VolumeBucket, int> HardSetsPerGroup(
        Workout workout,
        int weekNumber,
        Func<string, (string Primary, IReadOnlyList<string> Secondary)?> lookup)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(lookup);

        var templateWeek = workout.GetTemplateWeek(weekNumber);
        var blockType = workout.GetBlockType(weekNumber);
        if (templateWeek < 1 || templateWeek > _program.TotalWeeks)
        {
            throw new ArgumentOutOfRangeException(nameof(weekNumber), $"Week {weekNumber} maps outside the {_program.TotalWeeks}-week program table.");
        }

        var totals = new Dictionary<VolumeBucket, decimal>();

        foreach (var exercise in workout.Exercises)
        {
            var groups = lookup(exercise.ExternalTemplateId) ?? lookup(exercise.Name);
            if (groups is null)
            {
                continue;
            }

            var sets = exercise.CalculatePlannedSets(templateWeek, blockType).Count();
            if (sets == 0)
            {
                continue;
            }

            Add(totals, MuscleGroups.BucketFor(groups.Value.Primary), sets);
            foreach (var secondary in groups.Value.Secondary)
            {
                Add(totals, MuscleGroups.BucketFor(secondary), sets * 0.5m);
            }
        }

        return totals
            .OrderBy(pair => pair.Key)
            .ToDictionary(
                pair => pair.Key,
                pair => (int)Math.Round(pair.Value, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// One warning per bucket whose sets exceed its cap, in bucket order.
    /// </summary>
    public IReadOnlyList<VolumeWarning> Warnings(IReadOnlyDictionary<VolumeBucket, int> sets, VolumeCaps caps)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(caps);

        var warnings = new List<VolumeWarning>();
        foreach (var (bucket, count) in sets.OrderBy(pair => pair.Key))
        {
            var cap = caps.CapFor(bucket);
            if (cap is null || count <= cap.Value)
            {
                continue;
            }

            warnings.Add(new VolumeWarning(
                bucket,
                count,
                cap.Value,
                $"{bucket}: {count} hard sets this week, cap {cap.Value}"));
        }

        return warnings;
    }

    private static void Add(Dictionary<VolumeBucket, decimal> totals, VolumeBucket? bucket, decimal amount)
    {
        if (bucket is null)
        {
            return;
        }

        totals[bucket.Value] = totals.GetValueOrDefault(bucket.Value) + amount;
    }
}
