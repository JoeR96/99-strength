using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>How hard one interval of a cardio protocol is meant to feel.</summary>
public enum CardioEffort
{
    WarmUp,
    Work,
    Recovery,
    CoolDown,
    Easy
}

/// <summary>One timed block of a cardio protocol. Immutable value object.</summary>
public sealed record CardioInterval
{
    public string Label { get; }
    public int Seconds { get; }
    public CardioEffort Effort { get; }

    public CardioInterval(string label, int seconds, CardioEffort effort)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new BusinessRuleViolationException("Interval label is required");
        }

        if (seconds <= 0)
        {
            throw new BusinessRuleViolationException("Interval seconds must be positive");
        }

        Label = label.Trim();
        Seconds = seconds;
        Effort = effort;
    }
}
