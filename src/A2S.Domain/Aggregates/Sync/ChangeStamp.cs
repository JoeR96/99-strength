namespace A2S.Domain.Aggregates.Sync;

/// <summary>
/// The parts of a user's data a client can cache and refresh separately. A successful write bumps the
/// areas it touched; clients compare versions to know what to refetch.
/// </summary>
[Flags]
public enum SyncArea
{
    None = 0,
    Habits = 1,
    Plan = 2,
    Workout = 4,
    Cardio = 8,
    Mobility = 16,
    All = Habits | Plan | Workout | Cardio | Mobility
}

/// <summary>Wire names ("habits", "plan", …) for <see cref="SyncArea"/>, in a fixed order.</summary>
public static class SyncAreas
{
    public static IReadOnlyList<(SyncArea Area, string Name)> Each { get; } =
    [
        (SyncArea.Habits, "habits"),
        (SyncArea.Plan, "plan"),
        (SyncArea.Workout, "workout"),
        (SyncArea.Cardio, "cardio"),
        (SyncArea.Mobility, "mobility")
    ];

    /// <summary>The single areas in <paramref name="areas"/>, as wire names.</summary>
    public static IReadOnlyList<string> Names(SyncArea areas) =>
        Each.Where(e => areas.HasFlag(e.Area)).Select(e => e.Name).ToList();
}

/// <summary>
/// A user's change stamp: <see cref="Version"/> goes up by one on every successful write, and each area
/// remembers the version of its own last change (0: never changed). Area versions are therefore comparable
/// with the overall version and with each other, and every one of them only ever grows.
/// </summary>
public sealed record ChangeStamp(long Version, long Habits, long Plan, long Workout, long Cardio, long Mobility)
{
    public static ChangeStamp Empty { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>One write touching <paramref name="areas"/>: the version goes up and those areas take it.</summary>
    public ChangeStamp Bump(SyncArea areas)
    {
        var next = Version + 1;
        return new ChangeStamp(
            next,
            areas.HasFlag(SyncArea.Habits) ? next : Habits,
            areas.HasFlag(SyncArea.Plan) ? next : Plan,
            areas.HasFlag(SyncArea.Workout) ? next : Workout,
            areas.HasFlag(SyncArea.Cardio) ? next : Cardio,
            areas.HasFlag(SyncArea.Mobility) ? next : Mobility);
    }

    public long Of(SyncArea area) => area switch
    {
        SyncArea.Habits => Habits,
        SyncArea.Plan => Plan,
        SyncArea.Workout => Workout,
        SyncArea.Cardio => Cardio,
        SyncArea.Mobility => Mobility,
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "One area at a time")
    };

    /// <summary>Area name → version, in <see cref="SyncAreas.Each"/> order.</summary>
    public IReadOnlyDictionary<string, long> Areas() =>
        SyncAreas.Each.ToDictionary(e => e.Name, e => Of(e.Area));

    /// <summary>The areas changed after <paramref name="version"/> (what a client that saw that version missed).</summary>
    public SyncArea ChangedSince(long version) =>
        SyncAreas.Each.Where(e => Of(e.Area) > version).Aggregate(SyncArea.None, (all, e) => all | e.Area);
}
