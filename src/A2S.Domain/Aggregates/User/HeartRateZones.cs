using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.User;

/// <summary>
/// Heart-rate zones as % of max HR, WHOOP-style by default (Z1 50–60, Z2 60–70, Z3 70–80, Z4 80–90,
/// Z5 90–100). <see cref="LowerPercents"/> holds the five lower bounds; each zone's ceiling is the
/// next zone's floor, and zone 5 tops out at 100 %. Editable.
/// </summary>
public sealed record HeartRateZones
{
    public static readonly IReadOnlyList<int> WhoopLowerPercents = [50, 60, 70, 80, 90];

    public int MaxHeartRate { get; init; }
    public IReadOnlyList<int> LowerPercents { get; init; }

    public HeartRateZones(int maxHeartRate, IReadOnlyList<int> lowerPercents)
    {
        if (maxHeartRate is < 100 or > 240)
        {
            throw new BusinessRuleViolationException("Max heart rate must be between 100 and 240");
        }

        if (lowerPercents is null || lowerPercents.Count != 5)
        {
            throw new BusinessRuleViolationException("Five zone boundaries are needed");
        }

        for (var i = 0; i < 5; i++)
        {
            if (lowerPercents[i] is < 30 or >= 100 || (i > 0 && lowerPercents[i] <= lowerPercents[i - 1]))
            {
                throw new BusinessRuleViolationException("Zone boundaries must rise from zone 1 to zone 5 and stay under 100 %");
            }
        }

        MaxHeartRate = maxHeartRate;
        LowerPercents = lowerPercents.ToList();
    }

    public static HeartRateZones Whoop(int maxHeartRate) => new(maxHeartRate, WhoopLowerPercents);

    /// <summary>Top of a zone in bpm (zone 5 = max HR).</summary>
    public int UpperBpm(int zone)
    {
        if (zone is < 1 or > 5)
        {
            throw new BusinessRuleViolationException("Zone must be 1–5");
        }

        var percent = zone == 5 ? 100 : LowerPercents[zone];
        return (int)Math.Round(MaxHeartRate * percent / 100m, MidpointRounding.AwayFromZero);
    }

    public int LowerBpm(int zone) =>
        (int)Math.Round(MaxHeartRate * LowerPercents[zone - 1] / 100m, MidpointRounding.AwayFromZero);

    public bool Equals(HeartRateZones? other) =>
        other is not null && MaxHeartRate == other.MaxHeartRate && LowerPercents.SequenceEqual(other.LowerPercents);

    public override int GetHashCode() => HashCode.Combine(MaxHeartRate, LowerPercents.Count);
}
