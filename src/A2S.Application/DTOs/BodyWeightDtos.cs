using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Services;

namespace A2S.Application.DTOs;

/// <summary>One stored weigh-in.</summary>
public sealed record BodyWeightEntryDto(DateOnly Date, decimal Kg, DateTimeOffset RecordedAtUtc)
{
    public static BodyWeightEntryDto From(BodyWeightEntry entry) => new(entry.Date, entry.Kg, entry.RecordedAtUtc);
}

/// <summary>One point on the trend chart.</summary>
public sealed record TrendPointDto(DateOnly Date, decimal Kg, decimal? MovingAverage7, decimal Ewma)
{
    public static TrendPointDto From(TrendPoint point) => new(point.Date, point.Kg, point.MovingAverage7, point.Ewma);
}

/// <summary>Where the trend sits against the plan's checkpoints today.</summary>
public sealed record PaceDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter<Pace>))] Pace Pace,
    decimal TargetKgToday,
    decimal TrendKg,
    decimal DeltaKg)
{
    public static PaceDto From(PaceResult result) => new(result.Pace, result.TargetKgToday, result.TrendKg, result.DeltaKg);
}

/// <summary>The trend over a range plus, when a plan with checkpoints exists, today's pace against it.</summary>
public sealed record WeightTrendDto(IReadOnlyList<TrendPointDto> Points, PaceDto? Pace);
