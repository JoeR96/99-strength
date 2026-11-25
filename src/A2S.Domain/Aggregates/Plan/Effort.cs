using A2S.Domain.Aggregates.Cardio;

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

    /// <summary>The button as the cardio rules read it: Easy / Solid / Brutal → <see cref="EffortRating"/> (RPE 4 / 7 / 9).</summary>
    public static EffortRating ToRating(this Effort effort) => effort switch
    {
        Effort.Easy => EffortRating.Easy,
        Effort.Solid => EffortRating.Solid,
        Effort.Brutal => EffortRating.Brutal,
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unknown effort")
    };

    /// <summary>The button for a rating: <see cref="EffortRating"/> Easy / Solid / Brutal → Easy / Solid / Brutal.</summary>
    public static Effort ToEffort(this EffortRating rating) => rating switch
    {
        EffortRating.Easy => Effort.Easy,
        EffortRating.Solid => Effort.Solid,
        EffortRating.Brutal => Effort.Brutal,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Unknown effort rating")
    };
}
