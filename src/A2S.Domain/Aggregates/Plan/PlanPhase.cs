using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>A dated phase of the plan (Cut, Maintenance, Pause, Deload). Both ends inclusive.</summary>
public sealed record PlanPhase
{
    public PhaseType Type { get; }
    public DateOnly From { get; }
    public DateOnly To { get; }
    public string? Note { get; }

    public PlanPhase(PhaseType type, DateOnly from, DateOnly to, string? note)
    {
        if (from > to)
        {
            throw new BusinessRuleViolationException("Phase start must be on or before its end");
        }

        Type = type;
        From = from;
        To = to;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public bool Overlaps(PlanPhase other) => From <= other.To && other.From <= To;
}
