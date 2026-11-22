using System.Globalization;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>The kinds of condition a track's start gate can hold.</summary>
public enum GateConditionType
{
    /// <summary>The smoothed body-weight trend is at or below <see cref="GateCondition.Kg"/>.</summary>
    TrendWeightAtOrBelow,

    /// <summary>
    /// Another track (by template key) has a session in each of the last <see cref="GateCondition.Weeks"/> weeks of at least
    /// <see cref="GateCondition.Minutes"/> at intensity ≥ <see cref="GateCondition.Intensity"/>, rated no harder than <see cref="GateCondition.Effort"/>.
    /// </summary>
    TrackAtOrAbove,

    /// <summary>No pain flagged in any of <see cref="GateCondition.Areas"/> in the last <see cref="GateCondition.Weeks"/> weeks.</summary>
    NoPainInAreas
}

/// <summary>
/// One condition of a start gate. A flat record (only the fields its type uses are set) so it stores as one jsonb list.
/// Build them with the factory methods.
/// </summary>
public sealed record GateCondition(
    GateConditionType Type,
    decimal? Kg = null,
    string? TrackTemplateKey = null,
    int? Minutes = null,
    decimal? Intensity = null,
    EffortRating? Effort = null,
    int? Weeks = null,
    IReadOnlyList<string>? Areas = null)
{
    public static GateCondition TrendWeightAtOrBelow(decimal kg) =>
        Validate(new GateCondition(GateConditionType.TrendWeightAtOrBelow, Kg: kg));

    public static GateCondition TrackAtOrAbove(string trackTemplateKey, int minutes, decimal? intensity, EffortRating effort, int weeks) =>
        Validate(new GateCondition(GateConditionType.TrackAtOrAbove, TrackTemplateKey: trackTemplateKey, Minutes: minutes,
            Intensity: intensity, Effort: effort, Weeks: weeks));

    public static GateCondition NoPainInAreas(IReadOnlyList<string> areas, int weeks) =>
        Validate(new GateCondition(GateConditionType.NoPainInAreas, Weeks: weeks,
            Areas: areas.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList()));

    /// <summary>Checks the fields its type needs; throws <see cref="BusinessRuleViolationException"/> otherwise.</summary>
    public static GateCondition Validate(GateCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        switch (condition.Type)
        {
            case GateConditionType.TrendWeightAtOrBelow:
                Require(condition.Kg is > 0 and <= 300, "A weight gate needs a target between 0 and 300 kg");
                break;
            case GateConditionType.TrackAtOrAbove:
                Require(!string.IsNullOrWhiteSpace(condition.TrackTemplateKey), "A track gate needs the track it watches");
                Require(condition.Minutes is > 0 and <= 600, "A track gate needs minutes between 1 and 600");
                Require(condition.Intensity is null or >= 0, "A track gate's intensity cannot be negative");
                Require(condition.Effort is not null, "A track gate needs an effort rating");
                Require(condition.Weeks is >= 1 and <= 12, "A track gate needs 1–12 weeks");
                break;
            case GateConditionType.NoPainInAreas:
                Require(condition.Areas is { Count: > 0 }, "A pain gate needs at least one body area");
                Require(condition.Weeks is >= 1 and <= 52, "A pain gate needs 1–52 weeks");
                break;
            default:
                throw new BusinessRuleViolationException("Unknown gate condition");
        }

        return condition;
    }

    private static void Require(bool rule, string message)
    {
        if (!rule)
        {
            throw new BusinessRuleViolationException(message);
        }
    }
}

/// <summary>A pain flag from any cardio log (track session or plan completion).</summary>
public sealed record PainReport(DateOnly Date, PainFlag Pain, string? Area);

/// <summary>What a gate is checked against. Gathered by the application layer; the specs are pure.</summary>
public sealed record GateInputs(
    DateOnly Today,
    decimal? TrendKg,
    IReadOnlyList<CardioTrack> Tracks,
    IReadOnlyList<PainReport> PainReports);

/// <summary>One condition's result: met or not, its label, and progress in words.</summary>
public sealed record GateConditionResult(GateCondition Condition, bool Met, string Label, string Progress);

/// <summary>A specification over <see cref="GateInputs"/> for one condition type.</summary>
public interface IGateSpec
{
    GateConditionResult Evaluate(GateInputs inputs);
}

/// <summary>Trend weight (EWMA) at or below a target.</summary>
public sealed class TrendWeightAtOrBelowSpec(GateCondition condition) : IGateSpec
{
    public GateConditionResult Evaluate(GateInputs inputs)
    {
        var target = condition.Kg!.Value;
        var label = $"Trend weight ≤ {StartGate.Number(target)} kg";
        if (inputs.TrendKg is not { } trend)
        {
            return new GateConditionResult(condition, false, label, "No weigh-ins yet");
        }

        var met = trend <= target;
        var progress = met
            ? $"Trend {StartGate.Number(trend)} kg"
            : $"Trend {StartGate.Number(trend)} kg, {StartGate.Number(trend - target)} kg to go";
        return new GateConditionResult(condition, met, label, progress);
    }
}

/// <summary>
/// "Sustained" work on another track: a qualifying session (long enough, steep or fast enough, rated no harder than the
/// effort) in each of the last N weeks, counting back in 7-day blocks from today.
/// </summary>
public sealed class TrackAtOrAboveSpec(GateCondition condition) : IGateSpec
{
    public GateConditionResult Evaluate(GateInputs inputs)
    {
        var minutes = condition.Minutes!.Value;
        var weeks = condition.Weeks!.Value;
        var effort = condition.Effort!.Value;
        var intensity = condition.Intensity;
        var label = $"{TrackName(inputs)} ≥ {minutes} min"
            + (intensity is { } i ? $" at ≥ {StartGate.Number(i)}" + IntensitySuffix(inputs) : "")
            + $", rated {effort}, for {weeks} week{(weeks == 1 ? "" : "s")}";

        var track = inputs.Tracks.FirstOrDefault(t => t.TemplateKey == condition.TrackTemplateKey);
        if (track is null)
        {
            return new GateConditionResult(condition, false, label, "Start that track first");
        }

        // Consecutive 7-day blocks counted back from today. A current block with nothing in it yet doesn't break the run:
        // counting then starts from the block before.
        bool BlockQualifies(int block)
        {
            var to = inputs.Today.AddDays(-7 * block);
            var from = to.AddDays(-6);
            return track.Sessions.Any(s => s.Date >= from && s.Date <= to && Qualifies(s, minutes, intensity, effort));
        }

        var start = BlockQualifies(0) ? 0 : 1;
        var weeksMet = 0;
        while (weeksMet < weeks && BlockQualifies(start + weeksMet))
        {
            weeksMet++;
        }

        var met = weeksMet >= weeks;
        return new GateConditionResult(condition, met, label, $"{weeksMet} of {weeks} week{(weeks == 1 ? "" : "s")}");
    }

    public static bool Qualifies(CardioTrackSession session, int minutes, decimal? intensity, EffortRating effort) =>
        session.Completed
        && session.DurationSeconds >= minutes * 60
        && (intensity is null || session.IntensityValue >= intensity)
        && (int)session.Effort <= (int)effort
        && session.Pain == PainFlag.None;

    private string TrackName(GateInputs inputs) =>
        inputs.Tracks.FirstOrDefault(t => t.TemplateKey == condition.TrackTemplateKey)?.Name
        ?? CardioTrackTemplates.All.FirstOrDefault(t => t.Key == condition.TrackTemplateKey)?.Name
        ?? condition.TrackTemplateKey!;

    private string IntensitySuffix(GateInputs inputs)
    {
        var track = inputs.Tracks.FirstOrDefault(t => t.TemplateKey == condition.TrackTemplateKey);
        var unit = track?.Stages.Select(s => s.DoubleProgression?.IntensityUnit).FirstOrDefault(u => u is not null and not IntensityUnit.None)
            ?? (condition.TrackTemplateKey == CardioTrackTemplates.WalkBuilder ? IntensityUnit.InclinePercent : IntensityUnit.None);
        return unit switch
        {
            IntensityUnit.InclinePercent => " % incline",
            IntensityUnit.Level => " level",
            IntensityUnit.SpeedKmh => " km/h",
            _ => ""
        };
    }
}

/// <summary>No pain (niggle or stop) flagged in the given body areas for N weeks. Areas match case-insensitively by substring.</summary>
public sealed class NoPainInAreasSpec(GateCondition condition) : IGateSpec
{
    public GateConditionResult Evaluate(GateInputs inputs)
    {
        var areas = condition.Areas!;
        var weeks = condition.Weeks!.Value;
        var label = $"No {string.Join("/", areas)} pain for {weeks} week{(weeks == 1 ? "" : "s")}";
        var from = inputs.Today.AddDays(-7 * weeks + 1);

        var last = inputs.PainReports
            .Where(r => r.Pain != PainFlag.None && r.Date >= from && r.Date <= inputs.Today && Matches(r.Area, areas))
            .OrderByDescending(r => r.Date)
            .FirstOrDefault();

        if (last is null)
        {
            return new GateConditionResult(condition, true, label, "Clear");
        }

        var clearOn = last.Date.AddDays(7 * weeks);
        return new GateConditionResult(condition, false, label,
            $"{last.Area} pain on {last.Date.ToString("d MMM", CultureInfo.InvariantCulture)}; clear from {clearOn.ToString("d MMM", CultureInfo.InvariantCulture)}");
    }

    public static bool Matches(string? area, IReadOnlyList<string> areas) =>
        !string.IsNullOrWhiteSpace(area) && areas.Any(a => area.Contains(a, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A gate's overall result.</summary>
public sealed record StartGateResult(IReadOnlyList<GateConditionResult> Conditions)
{
    public bool AllMet => Conditions.Count > 0 && Conditions.All(c => c.Met);
    public int MetCount => Conditions.Count(c => c.Met);
}

/// <summary>Evaluates a start gate: one spec per condition. Pure, never changes the track.</summary>
public static class StartGate
{
    public static IGateSpec SpecFor(GateCondition condition) => condition.Type switch
    {
        GateConditionType.TrendWeightAtOrBelow => new TrendWeightAtOrBelowSpec(condition),
        GateConditionType.TrackAtOrAbove => new TrackAtOrAboveSpec(condition),
        GateConditionType.NoPainInAreas => new NoPainInAreasSpec(condition),
        _ => throw new BusinessRuleViolationException("Unknown gate condition")
    };

    public static StartGateResult Evaluate(IReadOnlyList<GateCondition> conditions, GateInputs inputs) =>
        new(conditions.Select(c => SpecFor(c).Evaluate(inputs)).ToList());

    /// <summary>The running gate the Walk → run template ships with (trend ≤ 90 kg; walk builder 45 min at 8 %, easy, 2 weeks; no knee/shin/Achilles pain for 4 weeks).</summary>
    public static IReadOnlyList<GateCondition> RunningGate() =>
    [
        GateCondition.TrendWeightAtOrBelow(90m),
        GateCondition.TrackAtOrAbove(CardioTrackTemplates.WalkBuilder, 45, 8m, EffortRating.Easy, 2),
        GateCondition.NoPainInAreas(["knee", "shin", "Achilles"], 4)
    ];

    internal static string Number(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
