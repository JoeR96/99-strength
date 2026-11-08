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

    public MobilityDrill(
        string name,
        int sets,
        DrillDose dose,
        SideBias extraSetSide,
        string? cues,
        string? standingAlternative,
        bool perSide)
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
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
