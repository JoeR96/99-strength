using System.Globalization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Today's cardio prescription for a cardio exercise: warm-up, the work block (continuous minutes or
/// interval rounds) and cool-down, with totals and a one-line text such as
/// "3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)".
/// </summary>
public sealed class PlannedCardioBlock : ValueObject
{
    public CardioLane Lane { get; private init; }
    public string StageName { get; private init; } = string.Empty;
    public int StageNumber { get; private init; }
    public int StageCount { get; private init; }
    public CardioSegment? WarmUp { get; private init; }
    public CardioSegment? CoolDown { get; private init; }
    public CardioStageKind Kind { get; private init; }
    public int WorkMinutes { get; private init; }
    public IntervalFormat? Intervals { get; private init; }
    public decimal? RecoveryLevel { get; private init; }
    public IntensityUnit IntensityUnit { get; private init; }
    public decimal? Intensity { get; private init; }
    public decimal? SuggestedIntensity { get; private init; }
    public decimal? Speed { get; private init; }

    /// <summary>The intensity is not set yet, or the lane needs a speed and none is set.</summary>
    public bool IsSettingsPending { get; private init; }

    public int TotalMinutes { get; private init; }
    public string Text { get; private set; } = string.Empty;

    /// <summary>The work part only, without warm-up or cool-down: "7 min @ level 6", "4 × 2 min @ L8 / 2 min @ L3".</summary>
    public string Title { get; private set; } = string.Empty;

    private PlannedCardioBlock()
    {
    }

    public static PlannedCardioBlock From(
        CardioLane lane,
        IReadOnlyList<CardioStageDefinition> stages,
        CardioSegment? warmUp,
        CardioSegment? coolDown,
        bool requiresSpeed,
        CardioStageState state)
    {
        var stage = stages[state.StageIndex];
        var intervals = stage.Kind == CardioStageKind.Intervals && stage.Formats is { } formats && state.FormatIndex is { } index
            ? formats[index]
            : null;
        var workMinutes = intervals is null
            ? state.Minutes ?? 0
            : intervals.Rounds * intervals.HardMinutes + (intervals.Rounds - 1) * intervals.EasyMinutes;

        var block = new PlannedCardioBlock
        {
            Lane = lane,
            StageName = stage.Name,
            StageNumber = state.StageIndex + 1,
            StageCount = stages.Count,
            WarmUp = warmUp,
            CoolDown = coolDown,
            Kind = stage.Kind,
            WorkMinutes = workMinutes,
            Intervals = intervals,
            RecoveryLevel = stage.RecoveryLevel,
            IntensityUnit = stage.IntensityUnit,
            Intensity = state.Intensity,
            SuggestedIntensity = state.SuggestedIntensity,
            Speed = state.Speed,
            IsSettingsPending = state.Intensity is null || (requiresSpeed && state.Speed is null),
            TotalMinutes = (warmUp?.Minutes ?? 0) + workMinutes + (coolDown?.Minutes ?? 0)
        };

        block.Title = block.BuildTitle();
        block.Text = block.BuildText();
        return block;
    }

    private string BuildText()
    {
        var parts = new List<string>();
        if (WarmUp is not null)
        {
            parts.Add(Easy(WarmUp));
        }

        parts.Add(Title);

        if (CoolDown is not null)
        {
            parts.Add(Easy(CoolDown));
        }

        return string.Join(" → ", parts);
    }

    private string BuildTitle() => Intervals is null ? Continuous() : Rounds(Intervals);

    private static string Easy(CardioSegment segment) => $"{segment.Minutes} min easy (L{Format(segment.Level)})";

    private string Continuous()
    {
        var work = $"{WorkMinutes} min";
        var setting = IntensityUnit switch
        {
            IntensityUnit.Level => $" @ level {Value(Intensity)}",
            IntensityUnit.InclinePercent => $" @ {Value(Intensity)} % incline",
            IntensityUnit.SpeedKmh => $" @ {Value(Intensity)} km/h",
            _ => string.Empty
        };
        var speed = IntensityUnit != IntensityUnit.SpeedKmh && Speed is { } kmh ? $", {Format(kmh)} km/h" : string.Empty;
        return work + setting + speed;
    }

    private string Rounds(IntervalFormat format) =>
        $"{format.Rounds} × {format.HardMinutes} min @ L{Value(Intensity)} / {format.EasyMinutes} min @ L{Value(RecoveryLevel)}";

    private static string Value(decimal? value) => value is { } v ? Format(v) : "?";

    private static string Format(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Lane;
        yield return StageName;
        yield return StageNumber;
        yield return StageCount;
        yield return WarmUp;
        yield return CoolDown;
        yield return Kind;
        yield return WorkMinutes;
        yield return Intervals;
        yield return RecoveryLevel;
        yield return IntensityUnit;
        yield return Intensity;
        yield return SuggestedIntensity;
        yield return Speed;
        yield return IsSettingsPending;
        yield return TotalMinutes;
        yield return Text;
        yield return Title;
    }
}
