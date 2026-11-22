namespace A2S.Domain.Aggregates.Plan;

/// <summary>How hard a cardio piece felt, on the app's three-point scale (brief § 10).</summary>
public enum Effort
{
    Easy,
    Solid,
    Brutal
}

public static class EffortScale
{
    /// <summary>Easy = RPE 4, Solid = RPE 7, Brutal = RPE 9.</summary>
    public static int ToRpe(this Effort effort) => effort switch
    {
        Effort.Easy => 4,
        Effort.Solid => 7,
        Effort.Brutal => 9,
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unknown effort")
    };
}
