using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>What was logged for a completed cardio session. Immutable value object.</summary>
public sealed record CardioLog
{
    public int DurationSeconds { get; }
    public int? AvgHeartRate { get; }
    public int? Rpe { get; }
    public decimal? DistanceMeters { get; }
    public string? Notes { get; }

    /// <summary>Minutes spent in heart-rate zones 4–5 during this session (watch/WHOOP, or the protocol default).</summary>
    public int? HighIntensityMinutes { get; }

    public CardioLog(int durationSeconds, int? avgHeartRate, int? rpe, decimal? distanceMeters, string? notes, int? highIntensityMinutes = null)
    {
        if (durationSeconds <= 0)
        {
            throw new BusinessRuleViolationException("Cardio duration must be positive");
        }

        if (avgHeartRate is < 30 or > 250)
        {
            throw new BusinessRuleViolationException("Average heart rate must be between 30 and 250");
        }

        if (rpe is < 1 or > 10)
        {
            throw new BusinessRuleViolationException("RPE must be between 1 and 10");
        }

        if (distanceMeters is < 0)
        {
            throw new BusinessRuleViolationException("Distance cannot be negative");
        }

        if (highIntensityMinutes is < 0)
        {
            throw new BusinessRuleViolationException("High-intensity minutes cannot be negative");
        }

        if (highIntensityMinutes is { } hi && hi > (durationSeconds + 59) / 60)
        {
            throw new BusinessRuleViolationException("High-intensity minutes cannot exceed the session length");
        }

        DurationSeconds = durationSeconds;
        AvgHeartRate = avgHeartRate;
        Rpe = rpe;
        DistanceMeters = distanceMeters;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        HighIntensityMinutes = highIntensityMinutes;
    }
}
