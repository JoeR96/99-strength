using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// What was logged for a completed cardio session. Immutable value object. Everything past the
/// duration is optional: a WHOOP-wearing person who doesn't want to log much taps an effort button
/// and is done; the watch, Hevy or a later edit fills in the rest.
/// </summary>
public sealed record CardioLog
{
    public int DurationSeconds { get; init; }
    public int? AvgHeartRate { get; init; }
    public int? Rpe { get; init; }
    public decimal? DistanceMeters { get; init; }
    public string? Notes { get; init; }

    /// <summary>Minutes spent in heart-rate zones 4–5 during this session (watch/WHOOP, or the protocol default).</summary>
    public int? HighIntensityMinutes { get; init; }

    /// <summary>Easy / Solid / Brutal (RPE 4 / 7 / 9). Null when only an exact RPE (or nothing) was given.</summary>
    public EffortRating? Effort { get; init; }

    /// <summary>The machine setting the session ran at: stair level, treadmill incline % or speed.</summary>
    public IntensityUnit? IntensityUnit { get; init; }
    public decimal? IntensityValue { get; init; }

    public int? Floors { get; init; }
    public int? Steps { get; init; }
    public int? MaxHeartRate { get; init; }

    /// <summary>Minutes in zone 5 alone (a subset of <see cref="HighIntensityMinutes"/>).</summary>
    public int? Zone5Minutes { get; init; }

    public PainFlag Pain { get; init; }
    public string? PainArea { get; init; }
    public CardioLogSource Source { get; init; }

    /// <summary>
    /// Minutes of the work block alone when the session had a warm-up or cool-down around it (then
    /// <see cref="DurationSeconds"/> is the whole session). Null: the whole session was the work.
    /// </summary>
    public decimal? WorkMinutes { get; init; }

    public CardioLog(
        int durationSeconds,
        int? avgHeartRate,
        int? rpe,
        decimal? distanceMeters,
        string? notes,
        int? highIntensityMinutes = null,
        EffortRating? effort = null,
        IntensityUnit? intensityUnit = null,
        decimal? intensityValue = null,
        int? floors = null,
        int? steps = null,
        int? maxHeartRate = null,
        int? zone5Minutes = null,
        PainFlag pain = PainFlag.None,
        string? painArea = null,
        CardioLogSource source = CardioLogSource.Manual)
    {
        if (durationSeconds <= 0)
        {
            throw new BusinessRuleViolationException("Cardio duration must be positive");
        }

        if (avgHeartRate is < 30 or > 250)
        {
            throw new BusinessRuleViolationException("Average heart rate must be between 30 and 250");
        }

        if (maxHeartRate is < 30 or > 250)
        {
            throw new BusinessRuleViolationException("Max heart rate must be between 30 and 250");
        }

        if (rpe is < 1 or > 10)
        {
            throw new BusinessRuleViolationException("RPE must be between 1 and 10");
        }

        if (distanceMeters is < 0)
        {
            throw new BusinessRuleViolationException("Distance cannot be negative");
        }

        if (highIntensityMinutes is < 0 || zone5Minutes is < 0)
        {
            throw new BusinessRuleViolationException("High-intensity minutes cannot be negative");
        }

        var sessionMinutes = (durationSeconds + 59) / 60;
        if (highIntensityMinutes is { } hi && hi > sessionMinutes)
        {
            throw new BusinessRuleViolationException("High-intensity minutes cannot exceed the session length");
        }

        if (zone5Minutes is { } z5 && z5 > sessionMinutes)
        {
            throw new BusinessRuleViolationException("Zone 5 minutes cannot exceed the session length");
        }

        if (floors is < 0 || steps is < 0)
        {
            throw new BusinessRuleViolationException("Floors and steps cannot be negative");
        }

        if (intensityValue is < 0)
        {
            throw new BusinessRuleViolationException("Intensity cannot be negative");
        }

        DurationSeconds = durationSeconds;
        AvgHeartRate = avgHeartRate;
        Rpe = rpe;
        DistanceMeters = distanceMeters;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        HighIntensityMinutes = highIntensityMinutes;
        Effort = effort;
        IntensityUnit = intensityValue is null ? null : intensityUnit;
        IntensityValue = intensityValue;
        Floors = floors;
        Steps = steps;
        MaxHeartRate = maxHeartRate;
        Zone5Minutes = zone5Minutes;
        Pain = pain;
        PainArea = pain == PainFlag.None || string.IsNullOrWhiteSpace(painArea) ? null : painArea.Trim();
        Source = source;
    }

    /// <summary>The effort to judge the session by: the button if pressed, else the exact RPE, else Solid.</summary>
    public EffortRating EffectiveEffort => Effort ?? (Rpe is { } rpe ? EffortRatings.FromRpe(rpe) : EffortRating.Solid);

    /// <summary>The RPE to store: exact RPE when given, else the effort button's 4 / 7 / 9.</summary>
    public int? EffectiveRpe => Rpe ?? (Effort is { } e ? (int)e : null);
}
