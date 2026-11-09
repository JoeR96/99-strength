namespace A2S.Domain.Services;

/// <summary>Where the trend weight sits against the straight line between checkpoints.</summary>
public enum Pace
{
    OnPace,
    Behind,
    Ahead,

    /// <summary>No checkpoints to measure against.</summary>
    NoTarget
}

/// <summary>
/// The pace reading for one day.
/// </summary>
/// <param name="Pace">On pace, behind, ahead or no target.</param>
/// <param name="TargetKgToday">Where the line between checkpoints sits today.</param>
/// <param name="TrendKg">The trend weight compared against it.</param>
/// <param name="DeltaKg">Trend minus target: positive means heavier than planned.</param>
public sealed record PaceResult(Pace Pace, decimal TargetKgToday, decimal TrendKg, decimal DeltaKg);
