using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;

namespace A2S.Application.Cardio;

/// <summary>A start-gate condition as the API sends and receives it.</summary>
public sealed record GateConditionDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter<GateConditionType>))] GateConditionType Type,
    decimal? Kg = null,
    string? TrackTemplateKey = null,
    int? Minutes = null,
    decimal? Intensity = null,
    EffortRating? Effort = null,
    int? Weeks = null,
    IReadOnlyList<string>? Areas = null)
{
    public static GateConditionDto From(GateCondition c) =>
        new(c.Type, c.Kg, c.TrackTemplateKey, c.Minutes, c.Intensity, c.Effort, c.Weeks, c.Areas);

    public GateCondition ToDomain() =>
        GateCondition.Validate(new GateCondition(Type, Kg, TrackTemplateKey, Minutes, Intensity, Effort, Weeks, Areas));
}

public sealed record GateConditionResultDto(GateConditionDto Condition, bool Met, string Label, string Progress);

/// <summary>A gated track's progress: each condition, and whether "Start" should be suggested.</summary>
public sealed record GateProgressDto(IReadOnlyList<GateConditionResultDto> Conditions, bool AllMet, int MetCount);

/// <summary>
/// Gathers what start gates are checked against (trend weight, the user's tracks, pain flags from track sessions and
/// plan completions) and runs the domain specs. Read-only.
/// </summary>
public sealed class CardioGateEvaluator
{
    private readonly IBodyWeightRepository _weights;
    private readonly ITrainingPlanRepository _plans;
    private readonly TrendService _trend;

    public CardioGateEvaluator(IBodyWeightRepository weights, ITrainingPlanRepository plans, TrendService trend)
    {
        _weights = weights;
        _plans = plans;
        _trend = trend;
    }

    /// <summary>Progress for every gated track, keyed by track id. Empty when no track is gated.</summary>
    public async Task<IReadOnlyDictionary<CardioTrackId, GateProgressDto>> EvaluateAsync(
        UserId userId, IReadOnlyList<CardioTrack> tracks, DateOnly today, CancellationToken ct)
    {
        var gated = tracks.Where(t => t.Status == CardioTrackStatus.Gated && t.StartGate.Count > 0).ToList();
        if (gated.Count == 0)
        {
            return new Dictionary<CardioTrackId, GateProgressDto>();
        }

        // 90 days is plenty to warm the EWMA; the latest point is the trend.
        var entries = await _weights.ListAsync(userId, today.AddDays(-90), today, ct);
        var series = _trend.Series(entries);
        decimal? trendKg = series.Count == 0 ? null : series[^1].Ewma;

        var plan = await _plans.GetActiveForUserAsync(userId, ct);
        var pain = tracks.SelectMany(t => t.Sessions).Select(s => new PainReport(s.Date, s.Pain, s.PainArea))
            .Concat(plan?.Completions.Where(c => c.Cardio is not null).Select(c => new PainReport(c.Date, c.Cardio!.Pain, c.Cardio.PainArea)) ?? [])
            .ToList();

        var inputs = new GateInputs(today, trendKg, tracks, pain);
        return gated.ToDictionary(t => t.Id, t => ToDto(StartGate.Evaluate(t.StartGate, inputs)));
    }

    private static GateProgressDto ToDto(StartGateResult result) => new(
        result.Conditions.Select(c => new GateConditionResultDto(GateConditionDto.From(c.Condition), c.Met, c.Label, c.Progress)).ToList(),
        result.AllMet,
        result.MetCount);
}
