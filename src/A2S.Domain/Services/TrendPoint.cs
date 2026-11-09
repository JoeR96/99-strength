namespace A2S.Domain.Services;

/// <summary>
/// One day on the body-weight trend chart.
/// </summary>
/// <param name="Date">The local date of the weigh-in.</param>
/// <param name="Kg">The raw weigh-in.</param>
/// <param name="MovingAverage7">Mean of the trailing seven calendar days; null until all seven have an entry.</param>
/// <param name="Ewma">Exponentially weighted moving average, seeded with the first weigh-in.</param>
public sealed record TrendPoint(DateOnly Date, decimal Kg, decimal? MovingAverage7, decimal Ewma);
