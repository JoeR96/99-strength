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

    public CardioLog(int durationSeconds, int? avgHeartRate, int? rpe, decimal? distanceMeters, string? notes)
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

        DurationSeconds = durationSeconds;
        AvgHeartRate = avgHeartRate;
        Rpe = rpe;
        DistanceMeters = distanceMeters;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
