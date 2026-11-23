using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// How a cardio exercise went: finished or stopped early, the effort button, any pain, and what was
/// actually done (total time, work minutes, level or incline, speed, floors). Drives the cardio lane rules.
/// </summary>
public sealed class CardioResult : ValueObject
{
    public bool Finished { get; private init; }
    public EffortRating Effort { get; private init; }
    public PainFlag Pain { get; private init; }
    public string? PainArea { get; private init; }
    public int TotalSeconds { get; private init; }
    public decimal? WorkMinutes { get; private init; }
    public decimal? Intensity { get; private init; }
    public decimal? Speed { get; private init; }
    public int? Floors { get; private init; }

    /// <summary>Pain or stopped early: the session holds, and two in a row step back.</summary>
    public bool IsMiss => !Finished || Pain != PainFlag.None;

    // EF Core constructor for JSON deserialization
    private CardioResult()
    {
    }

    public CardioResult(
        bool finished,
        EffortRating effort,
        PainFlag pain = PainFlag.None,
        string? painArea = null,
        int totalSeconds = 0,
        decimal? workMinutes = null,
        decimal? intensity = null,
        decimal? speed = null,
        int? floors = null)
    {
        CheckRule(totalSeconds >= 0, "Total seconds cannot be negative");

        Finished = finished;
        Effort = effort;
        Pain = pain;
        PainArea = painArea;
        TotalSeconds = totalSeconds;
        WorkMinutes = workMinutes;
        Intensity = intensity;
        Speed = speed;
        Floors = floors;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Finished;
        yield return Effort;
        yield return Pain;
        yield return PainArea;
        yield return TotalSeconds;
        yield return WorkMinutes;
        yield return Intensity;
        yield return Speed;
        yield return Floors;
    }
}
