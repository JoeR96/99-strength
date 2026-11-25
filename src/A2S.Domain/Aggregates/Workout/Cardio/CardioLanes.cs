using A2S.Domain.Aggregates.Cardio;

namespace A2S.Domain.Aggregates.Workout.Cardio;

/// <summary>A lane's ladder plus its fixed warm-up and cool-down, and whether speed must be set before a session.</summary>
public record CardioLanePreset(
    CardioLane Lane,
    IReadOnlyList<CardioStageDefinition> Stages,
    CardioSegment? WarmUp,
    CardioSegment? CoolDown,
    bool RequiresSpeed);

/// <summary>The two shipped lanes: stair climber (hard) and incline walk (easy).</summary>
public static class CardioLanes
{
    /// <summary>
    /// Warm-up 3 min L3, cool-down 2 min L3. Stage 1: continuous 6 → 12 min at level 6 (+1 min).
    /// Stage 2: 4 × 2/2 → 4 × 3/3 at a hard level asked on entry (continuous level + 2), recoveries at L3.
    /// Stage 3: 4 × 4/3, hard level +1 per step up to 20.
    /// </summary>
    public static CardioLanePreset StairClimber() => new(
        CardioLane.Hard,
        [
            new CardioStageDefinition(
                "Continuous", CardioStageKind.Continuous, CardioStepAxis.Minutes, IntensityUnit.Level,
                MinMinutes: 6, MaxMinutes: 12, MinuteStep: 1, FixedMinutes: null, Formats: null,
                IntensityOnEntry: 6m, AsksForIntensity: false, AskSuggestValue: null, AskSuggestOffset: null,
                IntensityStep: 1m, IntensityCap: null, SpeedStepOnEntry: null, RepeatsAtTop: false, RecoveryLevel: null),
            new CardioStageDefinition(
                "Intervals", CardioStageKind.Intervals, CardioStepAxis.IntervalFormat, IntensityUnit.Level,
                MinMinutes: null, MaxMinutes: null, MinuteStep: null, FixedMinutes: null,
                Formats: [new IntervalFormat(4, 2, 2), new IntervalFormat(4, 3, 3)],
                IntensityOnEntry: null, AsksForIntensity: true, AskSuggestValue: null, AskSuggestOffset: 2m,
                IntensityStep: 1m, IntensityCap: null, SpeedStepOnEntry: null, RepeatsAtTop: false, RecoveryLevel: 3m),
            new CardioStageDefinition(
                "4 × 4", CardioStageKind.Intervals, CardioStepAxis.Intensity, IntensityUnit.Level,
                MinMinutes: null, MaxMinutes: null, MinuteStep: null, FixedMinutes: null,
                Formats: [new IntervalFormat(4, 4, 3)],
                IntensityOnEntry: null, AsksForIntensity: false, AskSuggestValue: null, AskSuggestOffset: null,
                IntensityStep: 1m, IntensityCap: 20m, SpeedStepOnEntry: null, RepeatsAtTop: false, RecoveryLevel: 3m)
        ],
        WarmUp: new CardioSegment(3, 3m),
        CoolDown: new CardioSegment(2, 3m),
        RequiresSpeed: false);

    /// <summary>
    /// No warm-up or cool-down. Stage 1: 10 → 30 min (+2 min) at an incline asked on the first session (5 % suggested).
    /// Stage 2: 30 min, incline +1 % to 12 %. Stage 3: 30 min, speed +0.3 km/h and incline back to 8 %,
    /// then +1 % to 12 % again, repeating. Speed is asked on the first session.
    /// </summary>
    public static CardioLanePreset InclineWalk() => new(
        CardioLane.Easy,
        [
            new CardioStageDefinition(
                "Longer", CardioStageKind.Continuous, CardioStepAxis.Minutes, IntensityUnit.InclinePercent,
                MinMinutes: 10, MaxMinutes: 30, MinuteStep: 2, FixedMinutes: null, Formats: null,
                IntensityOnEntry: null, AsksForIntensity: true, AskSuggestValue: 5m, AskSuggestOffset: null,
                IntensityStep: 1m, IntensityCap: null, SpeedStepOnEntry: null, RepeatsAtTop: false, RecoveryLevel: null),
            new CardioStageDefinition(
                "Steeper", CardioStageKind.Continuous, CardioStepAxis.Intensity, IntensityUnit.InclinePercent,
                MinMinutes: null, MaxMinutes: null, MinuteStep: null, FixedMinutes: 30, Formats: null,
                IntensityOnEntry: null, AsksForIntensity: false, AskSuggestValue: null, AskSuggestOffset: null,
                IntensityStep: 1m, IntensityCap: 12m, SpeedStepOnEntry: null, RepeatsAtTop: false, RecoveryLevel: null),
            new CardioStageDefinition(
                "Faster", CardioStageKind.Continuous, CardioStepAxis.Intensity, IntensityUnit.InclinePercent,
                MinMinutes: null, MaxMinutes: null, MinuteStep: null, FixedMinutes: 30, Formats: null,
                IntensityOnEntry: 8m, AsksForIntensity: false, AskSuggestValue: null, AskSuggestOffset: null,
                IntensityStep: 1m, IntensityCap: 12m, SpeedStepOnEntry: 0.3m, RepeatsAtTop: true, RecoveryLevel: null)
        ],
        WarmUp: null,
        CoolDown: null,
        RequiresSpeed: true);
}
