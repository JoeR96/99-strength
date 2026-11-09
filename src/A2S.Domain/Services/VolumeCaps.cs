namespace A2S.Domain.Services;

/// <summary>
/// Weekly hard-set caps per <see cref="VolumeBucket"/>. Buckets without an entry are uncapped.
/// </summary>
public sealed record VolumeCaps(IReadOnlyDictionary<VolumeBucket, int> HardSetsPerWeek)
{
    /// <summary>
    /// The defaults from brief § 5: chest 10, back 14, shoulders 10, quads 10, hamstrings 10,
    /// glutes 12, core 12. Arms, calves and other are uncapped.
    /// </summary>
    public static VolumeCaps Default { get; } = new(new Dictionary<VolumeBucket, int>
    {
        [VolumeBucket.Chest] = 10,
        [VolumeBucket.Back] = 14,
        [VolumeBucket.Shoulders] = 10,
        [VolumeBucket.Quads] = 10,
        [VolumeBucket.Hamstrings] = 10,
        [VolumeBucket.Glutes] = 12,
        [VolumeBucket.Core] = 12
    });

    public int? CapFor(VolumeBucket bucket) =>
        HardSetsPerWeek.TryGetValue(bucket, out var cap) ? cap : null;
}
