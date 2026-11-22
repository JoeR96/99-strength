using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Mobility;

/// <summary>
/// One drill in a mobility routine: sets × dose, optional side bias, coaching cues and a
/// standing alternative for when the floor is not an option. Immutable value object.
/// </summary>
public sealed record MobilityDrill
{
    public string Name { get; }
    public int Sets { get; }
    public DrillDose Dose { get; }
    public SideBias ExtraSetSide { get; }
    public string? Cues { get; }
    public string? StandingAlternative { get; }

    /// <summary>True when the dose is performed per side (e.g. 5 reps each side).</summary>
    public bool PerSide { get; }

    /// <summary>The progression chain, easiest first. Empty when the drill does not progress.</summary>
    public IReadOnlyList<DrillLevel> Levels { get; }

    /// <summary>Index into <see cref="Levels"/>; <see cref="Sets"/> and <see cref="Dose"/> are this level's current prescription.</summary>
    public int CurrentLevel { get; }

    /// <summary>Clean sessions in a row since the last change.</summary>
    public int CleanStreak { get; }

    /// <summary>The level the rule suggests moving to, waiting for the user to accept or decline.</summary>
    public int? SuggestedLevel { get; }

    public DrillLevel? CurrentLevelDetails => Levels.Count == 0 ? null : Levels[CurrentLevel];

    /// <summary>The current level's cues, else the drill's.</summary>
    public string? EffectiveCues => CurrentLevelDetails?.Cues ?? Cues;

    // EF Core constructor: an owned navigation (Dose) cannot be bound to a constructor parameter.
    private MobilityDrill()
    {
        Name = string.Empty;
        Dose = null!;
        Levels = [];
    }

    public MobilityDrill(
        string name,
        int sets,
        DrillDose dose,
        SideBias extraSetSide,
        string? cues,
        string? standingAlternative,
        bool perSide,
        IReadOnlyList<DrillLevel>? levels = null,
        int currentLevel = 0,
        int cleanStreak = 0,
        int? suggestedLevel = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Drill name is required");
        }

        if (sets <= 0)
        {
            throw new BusinessRuleViolationException("Drill sets must be positive");
        }

        Name = name.Trim();
        Sets = sets;
        Dose = dose ?? throw new BusinessRuleViolationException("Drill dose is required");
        ExtraSetSide = extraSetSide;
        Cues = NullIfBlank(cues);
        StandingAlternative = NullIfBlank(standingAlternative);
        PerSide = perSide;

        var list = levels?.ToList() ?? [];
        if (list.Count == 0 ? currentLevel != 0 : currentLevel < 0 || currentLevel >= list.Count)
        {
            throw new BusinessRuleViolationException("The current level must be one of the drill's levels");
        }

        if (suggestedLevel is { } s && (s < 0 || s >= list.Count))
        {
            throw new BusinessRuleViolationException("The suggested level must be one of the drill's levels");
        }

        if (list.Select(l => l.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
        {
            throw new BusinessRuleViolationException("Level names must be unique within a drill");
        }

        Levels = list.AsReadOnly();
        CurrentLevel = currentLevel;
        CleanStreak = Math.Max(0, cleanStreak);
        SuggestedLevel = suggestedLevel;
    }

    /// <summary>A drill with a progression chain, starting at the given level's sets, starting dose and nothing pending.</summary>
    public static MobilityDrill WithLevels(
        string name,
        IEnumerable<DrillLevel> levels,
        int startingLevel,
        SideBias extraSetSide,
        bool perSide,
        string? cues = null,
        string? standingAlternative = null)
    {
        var list = levels?.ToList() ?? [];
        if (list.Count == 0 || startingLevel < 0 || startingLevel >= list.Count)
        {
            throw new BusinessRuleViolationException("The starting level must be one of the drill's levels");
        }

        var level = list[startingLevel];
        return new MobilityDrill(name, level.Sets, level.StartingDose, extraSetSide, cues, standingAlternative, perSide, list, startingLevel);
    }

    /// <summary>The same drill at a new point in its chain (level, dose, streak, pending suggestion). Sets follow the level.</summary>
    public MobilityDrill WithProgress(int level, DrillDose dose, int cleanStreak, int? suggestedLevel)
    {
        var sets = Levels.Count == 0 ? Sets : Levels[level].Sets;
        return new MobilityDrill(Name, sets, dose, ExtraSetSide, Cues, StandingAlternative, PerSide, Levels, level, cleanStreak, suggestedLevel);
    }

    /// <summary>Timed holds get longer by <paramref name="seconds"/>; the current level's range shifts with them. Null when not a timed hold.</summary>
    public MobilityDrill? WithHoldIncrease(int seconds)
    {
        if (Dose.Unit != DoseUnit.Seconds)
        {
            return null;
        }

        var levels = Levels.Select((l, i) => i == CurrentLevel && l.Range is { } r
            ? new DrillLevel(l.Name, l.Description, l.Sets, l.Dose, r.Shift(seconds), l.Cues)
            : l).ToList();
        return new MobilityDrill(Name, Sets, new DrillDose(DoseUnit.Seconds, Dose.Amount + seconds), ExtraSetSide, Cues,
            StandingAlternative, PerSide, levels, CurrentLevel, CleanStreak, SuggestedLevel);
    }

    public bool Equals(MobilityDrill? other) =>
        other is not null
        && Name == other.Name && Sets == other.Sets && Dose == other.Dose && ExtraSetSide == other.ExtraSetSide
        && Cues == other.Cues && StandingAlternative == other.StandingAlternative && PerSide == other.PerSide
        && Levels.SequenceEqual(other.Levels) && CurrentLevel == other.CurrentLevel
        && CleanStreak == other.CleanStreak && SuggestedLevel == other.SuggestedLevel;

    public override int GetHashCode() => HashCode.Combine(Name, Sets, Dose, CurrentLevel, CleanStreak, SuggestedLevel, Levels.Count);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
