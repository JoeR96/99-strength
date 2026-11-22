using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>One week of a week program: a label ("C25K wk 4"), its length and what to do.</summary>
public sealed record ProgramWeek(int Number, string Label, int Minutes, string Description);

/// <summary>
/// A fixed list of weeks (generalised Couch to 5K). Move on after <see cref="SessionsToAdvance"/>
/// sessions at a week when all of them qualified. <see cref="SessionsPerWeek"/> is how often the
/// sessions are scheduled; it is informational.
/// </summary>
public sealed record WeekProgramSettings
{
    public IReadOnlyList<ProgramWeek> Weeks { get; init; }
    public int SessionsToAdvance { get; init; }
    public int SessionsPerWeek { get; init; }

    public WeekProgramSettings(IReadOnlyList<ProgramWeek> weeks, int sessionsToAdvance, int sessionsPerWeek = 3)
    {
        if (weeks is null || weeks.Count == 0)
        {
            throw new BusinessRuleViolationException("A week program needs at least one week");
        }

        if (sessionsToAdvance < 1 || sessionsPerWeek < 1)
        {
            throw new BusinessRuleViolationException("Sessions per week and to advance must be at least 1");
        }

        Weeks = weeks.ToList();
        SessionsToAdvance = sessionsToAdvance;
        SessionsPerWeek = sessionsPerWeek;
    }

    // Value equality over the week list (records compare lists by reference otherwise).
    public bool Equals(WeekProgramSettings? other) =>
        other is not null
        && Weeks.SequenceEqual(other.Weeks)
        && SessionsToAdvance == other.SessionsToAdvance
        && SessionsPerWeek == other.SessionsPerWeek;

    public override int GetHashCode() => HashCode.Combine(Weeks.Count, SessionsToAdvance, SessionsPerWeek);
}

/// <summary>
/// Volume (minutes or interval rounds) climbs from <see cref="VolumeMin"/> to <see cref="VolumeMax"/>
/// by <see cref="VolumeStep"/> (or <see cref="VolumeStepPercent"/>, rounded up to whole units); at the
/// top the intensity steps up and volume resets to the bottom. One step per
/// <see cref="SessionsToAdvance"/> qualifying sessions.
/// </summary>
public sealed record DoubleProgressionSettings
{
    public VolumeUnit VolumeUnit { get; init; }
    public decimal VolumeMin { get; init; }
    public decimal VolumeMax { get; init; }
    public decimal VolumeStep { get; init; }
    public decimal? VolumeStepPercent { get; init; }
    public IntensityUnit IntensityUnit { get; init; }
    public decimal? IntensityStart { get; init; }
    public decimal? IntensityStep { get; init; }
    public decimal? IntensityMax { get; init; }
    public int SessionsToAdvance { get; init; }
    public int? WorkSeconds { get; init; }
    public int? RestSeconds { get; init; }

    public DoubleProgressionSettings(
        VolumeUnit volumeUnit,
        decimal volumeMin,
        decimal volumeMax,
        decimal volumeStep,
        decimal? volumeStepPercent,
        IntensityUnit intensityUnit,
        decimal? intensityStart,
        decimal? intensityStep,
        decimal? intensityMax,
        int sessionsToAdvance = 2,
        int? workSeconds = null,
        int? restSeconds = null)
    {
        if (volumeMin <= 0 || volumeMax < volumeMin)
        {
            throw new BusinessRuleViolationException("Volume range must be positive with the top at or above the bottom");
        }

        if (volumeStep <= 0 && volumeStepPercent is null)
        {
            throw new BusinessRuleViolationException("Volume step must be positive");
        }

        if (volumeStepPercent is <= 0)
        {
            throw new BusinessRuleViolationException("Volume step percent must be positive");
        }

        if (sessionsToAdvance < 1)
        {
            throw new BusinessRuleViolationException("Sessions to advance must be at least 1");
        }

        if (intensityUnit != IntensityUnit.None)
        {
            if (intensityStart is null or < 0 || intensityStep is null or <= 0)
            {
                throw new BusinessRuleViolationException("An intensity needs a starting value and a positive step");
            }

            if (intensityMax is { } max && max < intensityStart)
            {
                throw new BusinessRuleViolationException("Intensity top cannot be below its start");
            }
        }

        if (volumeUnit == VolumeUnit.Rounds && (workSeconds is null or <= 0 || restSeconds is null or < 0))
        {
            throw new BusinessRuleViolationException("Interval rounds need work and rest seconds");
        }

        VolumeUnit = volumeUnit;
        VolumeMin = volumeMin;
        VolumeMax = volumeMax;
        VolumeStep = volumeStep;
        VolumeStepPercent = volumeStepPercent;
        IntensityUnit = intensityUnit;
        IntensityStart = intensityUnit == IntensityUnit.None ? null : intensityStart;
        IntensityStep = intensityUnit == IntensityUnit.None ? null : intensityStep;
        IntensityMax = intensityUnit == IntensityUnit.None ? null : intensityMax;
        SessionsToAdvance = sessionsToAdvance;
        WorkSeconds = volumeUnit == VolumeUnit.Rounds ? workSeconds : null;
        RestSeconds = volumeUnit == VolumeUnit.Rounds ? restSeconds : null;
    }
}

/// <summary>
/// One stage of a track: its own strategy (week program or double progression), how hard it should
/// feel, and the checks a session must pass to count. The exit condition is reaching the top of the
/// stage (last week passed, or volume and intensity both at their tops) and qualifying there.
/// </summary>
public sealed record TrackStage
{
    public string Name { get; init; }
    public TrackStageKind Kind { get; init; }
    public TrackEffortTarget Target { get; init; }
    public WeekProgramSettings? WeekProgram { get; init; }
    public DoubleProgressionSettings? DoubleProgression { get; init; }

    /// <summary>Easy stages: average HR must sit at or under this zone's ceiling, when HR is logged.</summary>
    public int? HrCeilingZone { get; init; }

    /// <summary>Hard stages: share of the work time that must be in zone 4–5, when zone minutes are logged.</summary>
    public decimal? ZoneTargetFraction { get; init; }

    public string? Note { get; init; }

    public TrackStage(
        string name,
        TrackStageKind kind,
        TrackEffortTarget target,
        WeekProgramSettings? weekProgram,
        DoubleProgressionSettings? doubleProgression,
        int? hrCeilingZone = null,
        decimal? zoneTargetFraction = null,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Stage name is required");
        }

        if (kind == TrackStageKind.WeekProgram && weekProgram is null)
        {
            throw new BusinessRuleViolationException("A week-program stage needs its weeks");
        }

        if (kind == TrackStageKind.DoubleProgression && doubleProgression is null)
        {
            throw new BusinessRuleViolationException("A double-progression stage needs its ranges");
        }

        if (hrCeilingZone is < 1 or > 5)
        {
            throw new BusinessRuleViolationException("Heart-rate ceiling zone must be 1–5");
        }

        if (zoneTargetFraction is <= 0 or > 1)
        {
            throw new BusinessRuleViolationException("Zone target must be a share between 0 and 1");
        }

        Name = name.Trim();
        Kind = kind;
        Target = target;
        WeekProgram = kind == TrackStageKind.WeekProgram ? weekProgram : null;
        DoubleProgression = kind == TrackStageKind.DoubleProgression ? doubleProgression : null;
        HrCeilingZone = hrCeilingZone;
        ZoneTargetFraction = zoneTargetFraction;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public static TrackStage Weeks(string name, WeekProgramSettings settings, string? note = null) =>
        new(name, TrackStageKind.WeekProgram, TrackEffortTarget.Easy, settings, null, note: note);

    public static TrackStage Double(
        string name,
        TrackEffortTarget target,
        DoubleProgressionSettings settings,
        int? hrCeilingZone = null,
        decimal? zoneTargetFraction = null,
        string? note = null) =>
        new(name, TrackStageKind.DoubleProgression, target, null, settings, hrCeilingZone, zoneTargetFraction, note);

    internal ICardioStageStrategy Strategy => Kind == TrackStageKind.WeekProgram
        ? WeekProgramStrategy.Instance
        : DoubleProgressionStrategy.Instance;
}

/// <summary>
/// Where a track is. One shape for every strategy: week programs use <see cref="Week"/> and the
/// week counters; double progressions use <see cref="Volume"/>, <see cref="Intensity"/> and the
/// qualifying / miss counters. <see cref="Step"/> counts every step up (for charts).
/// </summary>
public sealed record CardioTrackState(
    int StageIndex,
    int Week,
    decimal Volume,
    decimal? Intensity,
    int Qualifying,
    int ConsecutiveMisses,
    int WeekSessions,
    int WeekFailures,
    int WeekNiggles,
    bool StageComplete,
    TrackSuggestion Suggestion,
    int Step);
