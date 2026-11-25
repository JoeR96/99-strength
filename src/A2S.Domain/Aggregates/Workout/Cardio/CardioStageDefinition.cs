using A2S.Domain.Aggregates.Cardio;

namespace A2S.Domain.Aggregates.Workout.Cardio;

/// <summary>Which cardio lane an exercise feeds: hard (stair climber) or easy (incline walk).</summary>
public enum CardioLane
{
    Hard = 1,
    Easy = 2
}

public enum CardioStageKind
{
    Continuous = 1,
    Intervals = 2
}

/// <summary>What a step up changes inside a stage.</summary>
public enum CardioStepAxis
{
    Minutes = 1,
    IntervalFormat = 2,
    Intensity = 3
}

/// <summary>Interval rounds: hard minutes, then easy minutes between rounds (none after the last).</summary>
public record IntervalFormat(int Rounds, int HardMinutes, int EasyMinutes);

/// <summary>A fixed warm-up or cool-down: minutes at an easy level. Never progresses.</summary>
public record CardioSegment(int Minutes, decimal Level);

/// <summary>
/// One stage of a cardio ladder. A step up moves along <see cref="Axis"/>; past its end the
/// stage repeats (<see cref="RepeatsAtTop"/>) or the next stage is entered.
/// </summary>
public record CardioStageDefinition(
    string Name,
    CardioStageKind Kind,
    CardioStepAxis Axis,
    IntensityUnit IntensityUnit,
    int? MinMinutes,
    int? MaxMinutes,
    int? MinuteStep,
    int? FixedMinutes,
    IReadOnlyList<IntervalFormat>? Formats,
    decimal? IntensityOnEntry,
    bool AsksForIntensity,
    decimal? AskSuggestValue,
    decimal? AskSuggestOffset,
    decimal IntensityStep,
    decimal? IntensityCap,
    decimal? SpeedStepOnEntry,
    bool RepeatsAtTop,
    decimal? RecoveryLevel);
