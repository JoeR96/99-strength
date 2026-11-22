namespace A2S.Domain.Aggregates.Cardio;

/// <summary>What a cardio track trains on. Walking and running are separate tracks.</summary>
public enum CardioModality
{
    Walk,
    Run,
    StairClimber,
    Bike,
    Row,
    Other
}

public enum CardioTrackStatus
{
    Active,
    Paused,
    Completed
}

/// <summary>The three big effort buttons. The numeric value is the RPE stored for each.</summary>
public enum EffortRating
{
    Easy = 4,
    Solid = 7,
    Brutal = 9
}

public static class EffortRatings
{
    /// <summary>Maps an exact RPE to the nearest button: 1–5 Easy, 6–8 Solid, 9–10 Brutal.</summary>
    public static EffortRating FromRpe(int rpe) => rpe switch
    {
        <= 5 => EffortRating.Easy,
        <= 8 => EffortRating.Solid,
        _ => EffortRating.Brutal
    };
}

/// <summary>A pain flag on a session: none, a niggle to watch, or a stop (session cut short).</summary>
public enum PainFlag
{
    None,
    Niggle,
    Stop
}

/// <summary>Where a cardio log came from.</summary>
public enum CardioLogSource
{
    Manual,
    Hevy,
    Whoop
}

/// <summary>The machine setting that a double progression steps: stair level, treadmill incline % or speed.</summary>
public enum IntensityUnit
{
    None,
    Level,
    InclinePercent,
    SpeedKmh
}

/// <summary>What a double progression adds first: minutes of continuous work, or interval rounds.</summary>
public enum VolumeUnit
{
    Minutes,
    Rounds
}

public enum TrackStageKind
{
    WeekProgram,
    DoubleProgression
}

/// <summary>Easy tracks must not feel Brutal; hard tracks must reach zone 4–5.</summary>
public enum TrackEffortTarget
{
    Easy,
    Hard
}

/// <summary>A change the track proposes and waits for the person to accept or decline.</summary>
public enum TrackSuggestion
{
    None,
    RepeatWeek,
    DropBackWeek,
    NextStage
}

/// <summary>What one evaluation did to a track.</summary>
public enum TrackOutcome
{
    Qualified,
    SteppedUp,
    Held,
    SteppedBack,
    StageComplete,
    SuggestRepeat,
    SuggestDropBack,
    AwaitingDecision,
    Paused,
    AtTop
}
