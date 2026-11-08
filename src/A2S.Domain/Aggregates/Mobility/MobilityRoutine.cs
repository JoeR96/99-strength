using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Mobility;

/// <summary>
/// An ordered list of mobility drills with a target duration. Drill names are unique within a
/// routine so completions can tick drills by name.
/// </summary>
public sealed class MobilityRoutine : AggregateRoot<MobilityRoutineId>
{
    private readonly List<MobilityDrill> _drills = new();

    private MobilityRoutine(MobilityRoutineId id, UserId userId, string name, int targetMinutes, List<MobilityDrill> drills)
        : base(id)
    {
        UserId = userId;
        Name = name;
        TargetMinutes = targetMinutes;
        _drills = drills;
    }

    // EF Core constructor
    private MobilityRoutine()
    {
        Name = string.Empty;
    }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }
    public int TargetMinutes { get; private set; }
    public IReadOnlyList<MobilityDrill> Drills => _drills.AsReadOnly();

    public static MobilityRoutine Create(UserId userId, string name, int targetMinutes, IEnumerable<MobilityDrill> drills)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Routine name is required");
        CheckRule(targetMinutes > 0, "Target minutes must be positive");
        var list = ValidatedDrills(drills);

        return new MobilityRoutine(new MobilityRoutineId(Guid.NewGuid()), userId, name.Trim(), targetMinutes, list);
    }

    public void AddDrill(MobilityDrill drill)
    {
        CheckRule(drill is not null, "Drill is required");
        CheckRule(!HasDrill(drill!.Name), $"Drill names must be unique within a routine: '{drill.Name}' already exists");
        _drills.Add(drill);
    }

    /// <summary>Removes the named drill. Returns false when no drill has that name.</summary>
    public bool RemoveDrill(string name)
    {
        var index = IndexOf(name);
        if (index < 0)
        {
            return false;
        }

        CheckRule(_drills.Count > 1, "A routine needs at least one drill");
        _drills.RemoveAt(index);
        return true;
    }

    public void MoveDrill(string name, int newIndex)
    {
        var index = IndexOf(name);
        CheckRule(index >= 0, $"Drill '{name}' not found");
        CheckRule(newIndex >= 0 && newIndex < _drills.Count, $"New index must be between 0 and {_drills.Count - 1}");

        var drill = _drills[index];
        _drills.RemoveAt(index);
        _drills.Insert(newIndex, drill);
    }

    public void ReplaceDrills(IEnumerable<MobilityDrill> drills)
    {
        var list = ValidatedDrills(drills);
        _drills.Clear();
        _drills.AddRange(list);
    }

    private bool HasDrill(string name) => IndexOf(name) >= 0;

    private int IndexOf(string name) =>
        _drills.FindIndex(d => string.Equals(d.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static List<MobilityDrill> ValidatedDrills(IEnumerable<MobilityDrill> drills)
    {
        var list = drills?.ToList() ?? new List<MobilityDrill>();
        CheckRule(list.Count > 0, "A routine needs at least one drill");

        var distinctNames = list.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        CheckRule(distinctNames == list.Count, "Drill names must be unique within a routine");

        return list;
    }
}
