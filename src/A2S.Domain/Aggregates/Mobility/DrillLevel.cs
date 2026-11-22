using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Mobility;

/// <summary>What kind of mobility routine this is. Drives the Hevy exercise it is logged as.</summary>
public enum MobilityRoutineKind
{
    WarmUp,
    Corrective,
    Stretch,
    YogaFlow,
    FollowAlong
}

/// <summary>
/// A dose range a level progresses through: hold 15 → 30 s, adding 5 s per step. In the unit of the level's dose.
/// Immutable value object.
/// </summary>
public sealed record DoseRange
{
    public int Min { get; }
    public int Max { get; }
    public int Step { get; }

    public DoseRange(int min, int max, int step)
    {
        if (min <= 0 || max < min)
        {
            throw new BusinessRuleViolationException("A dose range needs a positive bottom no higher than its top");
        }

        if (step <= 0)
        {
            throw new BusinessRuleViolationException("A dose range step must be positive");
        }

        Min = min;
        Max = max;
        Step = step;
    }

    public DoseRange Shift(int by) => new(Min + by, Max + by, Step);
}

/// <summary>
/// One rung of a drill's progression chain (Easier / Standard / Harder, or Level 1 / 2 / 3): what it is,
/// how much of it, the range it progresses through and its cues. Immutable value object.
/// </summary>
public sealed record DrillLevel
{
    public string Name { get; }
    public string? Description { get; }
    public int Sets { get; }
    public DrillDose Dose { get; }
    public DoseRange? Range { get; }
    public string? Cues { get; }

    public DrillLevel(string name, string? description, int sets, DrillDose dose, DoseRange? range, string? cues)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Level name is required");
        }

        if (sets <= 0)
        {
            throw new BusinessRuleViolationException("Level sets must be positive");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Sets = sets;
        Dose = dose ?? throw new BusinessRuleViolationException("Level dose is required");
        Range = range;
        Cues = string.IsNullOrWhiteSpace(cues) ? null : cues.Trim();
    }

    /// <summary>The dose a drill starts this level at: the bottom of the range, or the level's dose.</summary>
    public DrillDose StartingDose => Range is null ? Dose : new DrillDose(Dose.Unit, Range.Min);

    /// <summary>"Level 2: hands on a bench".</summary>
    public string Label => Description is null ? Name : $"{Name}: {Description}";
}
