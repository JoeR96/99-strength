using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// Weekly minutes in heart-rate zones 4–5 (roughly 80–100 % HRmax) the plan asks for.
/// Default 15–30 min/week: one Norwegian 4×4 (16 min of work) lands inside the band, which is why
/// the brief schedules it. Logged per cardio session via <see cref="CardioLog.HighIntensityMinutes"/>
/// (from the watch/WHOOP, or defaulted from the protocol's work intervals).
/// </summary>
public sealed record HighIntensityTarget
{
    public const int MaximumMinutes = 600;

    public int MinMinutes { get; }
    public int MaxMinutes { get; }

    public HighIntensityTarget(int minMinutes = 15, int maxMinutes = 30)
    {
        if (minMinutes < 0 || maxMinutes < 0)
        {
            throw new BusinessRuleViolationException("High-intensity minutes cannot be negative");
        }

        if (minMinutes > maxMinutes)
        {
            throw new BusinessRuleViolationException("High-intensity minimum cannot exceed the maximum");
        }

        if (maxMinutes > MaximumMinutes)
        {
            throw new BusinessRuleViolationException($"High-intensity maximum cannot exceed {MaximumMinutes} minutes a week");
        }

        MinMinutes = minMinutes;
        MaxMinutes = maxMinutes;
    }

    public static HighIntensityTarget Default => new();
}
