namespace A2S.Domain.Services;

/// <summary>
/// The buckets the weekly volume budget is counted in. Hevy's finer-grained muscle
/// groups are folded into these so the caps in brief § 5 can be applied.
/// </summary>
public enum VolumeBucket
{
    Chest,
    Back,
    Shoulders,
    Quads,
    Hamstrings,
    Glutes,
    Core,
    Arms,
    Calves,
    Other
}

/// <summary>
/// Maps Hevy muscle-group names (as stored on <c>ExerciseDefinition.MuscleGroup</c> and
/// <c>SecondaryMuscleGroups</c>) to a <see cref="VolumeBucket"/>.
/// </summary>
public static class MuscleGroups
{
    private static readonly IReadOnlyDictionary<string, VolumeBucket> Buckets =
        new Dictionary<string, VolumeBucket>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = VolumeBucket.Chest,
            ["lats"] = VolumeBucket.Back,
            ["upper_back"] = VolumeBucket.Back,
            ["traps"] = VolumeBucket.Back,
            ["lower_back"] = VolumeBucket.Back,
            ["shoulders"] = VolumeBucket.Shoulders,
            ["quadriceps"] = VolumeBucket.Quads,
            ["hamstrings"] = VolumeBucket.Hamstrings,
            ["glutes"] = VolumeBucket.Glutes,
            ["abductors"] = VolumeBucket.Glutes,
            ["adductors"] = VolumeBucket.Glutes,
            ["abdominals"] = VolumeBucket.Core,
            ["biceps"] = VolumeBucket.Arms,
            ["triceps"] = VolumeBucket.Arms,
            ["forearms"] = VolumeBucket.Arms,
            ["calves"] = VolumeBucket.Calves,
            ["neck"] = VolumeBucket.Other,
            ["full_body"] = VolumeBucket.Other,
            ["cardio"] = VolumeBucket.Other,
            ["other"] = VolumeBucket.Other
        };

    /// <summary>
    /// Returns the bucket for a Hevy muscle group, or null when the group is unknown
    /// (for example the legacy "General" placeholder).
    /// </summary>
    public static VolumeBucket? BucketFor(string hevyMuscleGroup)
    {
        if (string.IsNullOrWhiteSpace(hevyMuscleGroup))
        {
            return null;
        }

        return Buckets.TryGetValue(hevyMuscleGroup.Trim(), out var bucket) ? bucket : null;
    }
}
