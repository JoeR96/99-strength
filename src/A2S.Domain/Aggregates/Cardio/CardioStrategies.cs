using System.Globalization;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>What today's cardio session asks for. Derived from a track's state; shown on Today and pushed to Hevy.</summary>
public sealed record CardioPrescription(
    string Title,
    string StageName,
    int Minutes,
    decimal WorkMinutes,
    VolumeUnit? VolumeUnit,
    decimal? Volume,
    IntensityUnit IntensityUnit,
    decimal? Intensity,
    int? WorkSeconds,
    int? RestSeconds,
    int? Week,
    string? Note);

/// <summary>The facts about one logged session a strategy needs to judge it.</summary>
public sealed record SessionCheck(
    bool Completed,
    EffortRating Effort,
    PainFlag Pain,
    bool? ZoneTargetMet,
    bool? HeartRateWithinCeiling)
{
    /// <summary>
    /// Builds the check for <paramref name="stage"/>. HR-based criteria are null (unknown) when the
    /// log has no HR data or the person has no zones, and the strategies then fall back to effort.
    /// </summary>
    public static SessionCheck From(TrackStage stage, CardioLog log, bool completed, HeartRateZones? zones, decimal workMinutes)
    {
        bool? zoneMet = null;
        if (stage.ZoneTargetFraction is { } fraction && log.HighIntensityMinutes is { } hi)
        {
            zoneMet = hi >= fraction * workMinutes;
        }

        bool? withinCeiling = null;
        if (stage.HrCeilingZone is { } zone && log.AvgHeartRate is { } avg && zones is not null)
        {
            withinCeiling = avg <= zones.UpperBpm(zone);
        }

        return new SessionCheck(completed && log.Pain != PainFlag.Stop, log.EffectiveEffort, log.Pain, zoneMet, withinCeiling);
    }

    /// <summary>Whether the session counts towards the next step, and why not when it doesn't.</summary>
    public (bool Qualifies, string Reason) Judge(TrackStage stage)
    {
        if (!Completed)
        {
            return (false, Pain == PainFlag.Stop ? "Stopped for pain" : "Prescription not completed");
        }

        if (Pain != PainFlag.None)
        {
            return (false, "A pain flag was logged");
        }

        if (stage.Target == TrackEffortTarget.Easy)
        {
            if (Effort == EffortRating.Brutal)
            {
                return (false, "Rated Brutal on an easy track");
            }

            if (HeartRateWithinCeiling == false)
            {
                return (false, $"Average HR above the zone {stage.HrCeilingZone} ceiling");
            }

            return (true, HeartRateWithinCeiling == true ? "Completed easy, HR in range" : "Completed and felt easy enough");
        }

        if (ZoneTargetMet is { } met)
        {
            return met
                ? (true, "Completed with enough zone 4–5 minutes")
                : (false, "Not enough time in zone 4–5");
        }

        return Effort == EffortRating.Easy
            ? (false, "No HR data and rated Easy on a hard track")
            : (true, "Completed hard (no HR data, judged on effort)");
    }
}

public sealed record StageEvaluation(CardioTrackState State, TrackOutcome Outcome, string Reason);

/// <summary>
/// What the plan says about the track's current week: how many sessions are scheduled for it a week, and how
/// many of those were missed (scheduled on a past day this week with no completion). Null without a plan slot.
/// </summary>
public sealed record WeekContext(int? PlannedSessionsThisWeek, int MissedPlannedSessions);

/// <summary>A stage's progression rule. Pure: state in, state out.</summary>
public interface ICardioStageStrategy
{
    CardioTrackState Start(TrackStage stage, int stageIndex);
    CardioPrescription Prescribe(TrackStage stage, CardioTrackState state);
    StageEvaluation Evaluate(TrackStage stage, CardioTrackState state, SessionCheck check, WeekContext? context = null);
    int SessionsToStepUp(TrackStage stage, CardioTrackState state, WeekContext? context = null);

    /// <summary>Moves <paramref name="state"/> inside the (possibly edited) ranges of <paramref name="stage"/>.</summary>
    CardioTrackState Clamp(TrackStage stage, CardioTrackState state);
}

/// <summary>
/// Volume through its range, then intensity up one step with volume back to the bottom.
/// Advance after N qualifying sessions; Brutal-on-easy, pain or a missed zone target holds;
/// two missed prescriptions in a row step back once.
/// </summary>
public sealed class DoubleProgressionStrategy : ICardioStageStrategy
{
    public static readonly DoubleProgressionStrategy Instance = new();

    public CardioTrackState Start(TrackStage stage, int stageIndex)
    {
        var s = stage.DoubleProgression!;
        return new CardioTrackState(stageIndex, 0, s.VolumeMin, s.IntensityStart, 0, 0, 0, 0, 0, false, TrackSuggestion.None, 0);
    }

    public CardioPrescription Prescribe(TrackStage stage, CardioTrackState state)
    {
        var s = stage.DoubleProgression!;
        var intensity = state.Intensity is { } i ? " @ " + FormatIntensity(s.IntensityUnit, i) : string.Empty;

        if (s.VolumeUnit == VolumeUnit.Rounds)
        {
            var rounds = (int)state.Volume;
            var work = s.WorkSeconds!.Value;
            var rest = s.RestSeconds!.Value;
            var title = $"{rounds} × {Seconds(work)} hard / {Seconds(rest)} easy{intensity}";
            var minutes = (int)Math.Ceiling(rounds * (work + rest) / 60m);
            return new CardioPrescription(title, stage.Name, minutes, rounds * work / 60m, s.VolumeUnit, state.Volume,
                s.IntensityUnit, state.Intensity, work, rest, null, stage.Note);
        }

        var whole = (int)Math.Ceiling(state.Volume);
        return new CardioPrescription($"{Number(state.Volume)} min continuous{intensity}", stage.Name, whole, state.Volume,
            s.VolumeUnit, state.Volume, s.IntensityUnit, state.Intensity, null, null, null, stage.Note);
    }

    public StageEvaluation Evaluate(TrackStage stage, CardioTrackState state, SessionCheck check, WeekContext? context = null)
    {
        var s = stage.DoubleProgression!;
        var (qualifies, reason) = check.Judge(stage);

        if (!check.Completed)
        {
            var misses = state.ConsecutiveMisses + 1;
            if (misses >= 2)
            {
                var back = StepBack(s, state) with { ConsecutiveMisses = 0, Qualifying = 0 };
                return new StageEvaluation(back, TrackOutcome.SteppedBack, $"{reason}, twice in a row: stepped back once");
            }

            return new StageEvaluation(state with { ConsecutiveMisses = misses }, TrackOutcome.Held, $"{reason}: holding");
        }

        var cleared = state with { ConsecutiveMisses = 0 };
        if (!qualifies)
        {
            return new StageEvaluation(cleared, TrackOutcome.Held, $"{reason}: holding");
        }

        var qualifying = cleared.Qualifying + 1;
        if (qualifying < s.SessionsToAdvance)
        {
            return new StageEvaluation(cleared with { Qualifying = qualifying }, TrackOutcome.Qualified,
                $"{reason} ({qualifying} of {s.SessionsToAdvance})");
        }

        if (state.Volume < s.VolumeMax)
        {
            var next = cleared with { Volume = NextVolume(s, state.Volume), Qualifying = 0, Step = state.Step + 1 };
            return new StageEvaluation(next, TrackOutcome.SteppedUp, $"{reason}: {s.SessionsToAdvance} qualifying sessions, volume up");
        }

        if (s.IntensityUnit != IntensityUnit.None && (s.IntensityMax is null || state.Intensity < s.IntensityMax))
        {
            var intensity = state.Intensity!.Value + s.IntensityStep!.Value;
            if (s.IntensityMax is { } max)
            {
                intensity = Math.Min(intensity, max);
            }

            var next = cleared with { Intensity = intensity, Volume = s.VolumeMin, Qualifying = 0, Step = state.Step + 1 };
            return new StageEvaluation(next, TrackOutcome.SteppedUp, $"{reason}: top of the range, intensity up and volume reset");
        }

        if (state.StageComplete)
        {
            return new StageEvaluation(cleared with { Qualifying = 0 }, TrackOutcome.AtTop, $"{reason}: at the top, holding");
        }

        return new StageEvaluation(cleared with { Qualifying = 0, StageComplete = true }, TrackOutcome.StageComplete,
            $"{reason}: top of this stage reached");
    }

    public int SessionsToStepUp(TrackStage stage, CardioTrackState state, WeekContext? context = null) =>
        Math.Max(0, stage.DoubleProgression!.SessionsToAdvance - state.Qualifying);

    public CardioTrackState Clamp(TrackStage stage, CardioTrackState state)
    {
        var s = stage.DoubleProgression!;
        decimal? intensity = s.IntensityUnit == IntensityUnit.None
            ? null
            : Math.Max(s.IntensityStart!.Value, Math.Min(state.Intensity ?? s.IntensityStart!.Value, s.IntensityMax ?? decimal.MaxValue));
        return state with
        {
            Volume = Math.Max(s.VolumeMin, Math.Min(state.Volume, s.VolumeMax)),
            Intensity = intensity,
            Qualifying = Math.Min(state.Qualifying, s.SessionsToAdvance - 1)
        };
    }

    private static decimal NextVolume(DoubleProgressionSettings s, decimal volume)
    {
        var next = s.VolumeStepPercent is { } pct
            ? Math.Max(volume + 1, Math.Ceiling(volume * (1 + pct / 100m)))
            : volume + s.VolumeStep;
        return Math.Min(next, s.VolumeMax);
    }

    private static CardioTrackState StepBack(DoubleProgressionSettings s, CardioTrackState state)
    {
        if (state.Volume > s.VolumeMin)
        {
            var step = s.VolumeStepPercent is { } pct ? Math.Max(1, Math.Floor(state.Volume * pct / 100m)) : s.VolumeStep;
            return state with { Volume = Math.Max(s.VolumeMin, state.Volume - step), StageComplete = false };
        }

        if (state.Intensity is { } i && s.IntensityStart is { } start && i > start)
        {
            return state with { Intensity = Math.Max(start, i - s.IntensityStep!.Value), Volume = s.VolumeMax, StageComplete = false };
        }

        return state;
    }

    public static string FormatIntensity(IntensityUnit unit, decimal value) => unit switch
    {
        IntensityUnit.Level => $"level {Number(value)}",
        IntensityUnit.InclinePercent => $"{Number(value)}% incline",
        IntensityUnit.SpeedKmh => $"{Number(value)} km/h",
        _ => Number(value)
    };

    private static string Seconds(int seconds) =>
        seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds} s";

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// A fixed list of weeks (generalised Couch to 5K). After the planned sessions at a week: all
/// qualified → next week; otherwise suggest repeating it. Two niggles in a week, or any stop,
/// suggest dropping back a week. Suggestions wait for the person.
/// </summary>
public sealed class WeekProgramStrategy : ICardioStageStrategy
{
    public static readonly WeekProgramStrategy Instance = new();

    public CardioTrackState Start(TrackStage stage, int stageIndex) =>
        new(stageIndex, stage.WeekProgram!.Weeks[0].Number, 0, null, 0, 0, 0, 0, 0, false, TrackSuggestion.None, 0);

    public CardioPrescription Prescribe(TrackStage stage, CardioTrackState state)
    {
        var week = CurrentWeek(stage.WeekProgram!, state.Week);
        return new CardioPrescription($"{week.Label} day {state.WeekSessions + 1}", stage.Name, week.Minutes, week.Minutes,
            null, null, IntensityUnit.None, null, null, null, week.Number, stage.Note ?? week.Description);
    }

    public StageEvaluation Evaluate(TrackStage stage, CardioTrackState state, SessionCheck check, WeekContext? context = null)
    {
        var (qualifies, reason) = check.Judge(stage);
        var required = Required(stage, context);
        var next = state with
        {
            WeekSessions = state.WeekSessions + 1,
            WeekFailures = state.WeekFailures + (qualifies ? 0 : 1),
            WeekNiggles = state.WeekNiggles + (check.Pain == PainFlag.Niggle ? 1 : 0),
            WeekMissed = Math.Max(state.WeekMissed, context?.MissedPlannedSessions ?? 0)
        };

        if (check.Pain == PainFlag.Stop || next.WeekNiggles >= 2)
        {
            var why = check.Pain == PainFlag.Stop ? "Stopped for pain" : "Two niggles this week";
            return new StageEvaluation(next with { Suggestion = TrackSuggestion.DropBackWeek }, TrackOutcome.SuggestDropBack,
                $"{why}: suggest dropping back a week");
        }

        if (next.WeekSessions < required)
        {
            return new StageEvaluation(next, qualifies ? TrackOutcome.Qualified : TrackOutcome.Held,
                $"{reason} ({next.WeekSessions} of {required} this week)");
        }

        if (next.WeekFailures > 0 || next.WeekMissed > 0)
        {
            var why = next.WeekMissed > 0 && next.WeekFailures == 0
                ? $"{next.WeekMissed} planned session(s) missed"
                : next.WeekMissed > 0
                    ? $"{next.WeekFailures} session(s) didn't qualify and {next.WeekMissed} planned session(s) missed"
                    : $"{next.WeekFailures} session(s) this week didn't qualify";
            return new StageEvaluation(next with { Suggestion = TrackSuggestion.RepeatWeek }, TrackOutcome.SuggestRepeat,
                $"{why}: suggest repeating the week");
        }

        return Advance(stage, next, "Every session this week qualified");
    }

    /// <summary>Moves to the next week (or marks the stage complete after the last one).</summary>
    internal static StageEvaluation Advance(TrackStage stage, CardioTrackState state, string why)
    {
        var weeks = stage.WeekProgram!.Weeks;
        var index = IndexOf(weeks, state.Week);
        var reset = ResetWeek(state);
        if (index >= weeks.Count - 1)
        {
            return new StageEvaluation(reset with { StageComplete = true }, TrackOutcome.StageComplete,
                $"{why}: last week done, stage complete");
        }

        return new StageEvaluation(reset with { Week = weeks[index + 1].Number, Step = state.Step + 1 }, TrackOutcome.SteppedUp,
            $"{why}: on to {weeks[index + 1].Label}");
    }

    internal static CardioTrackState DropBack(TrackStage stage, CardioTrackState state)
    {
        var weeks = stage.WeekProgram!.Weeks;
        var index = Math.Max(0, IndexOf(weeks, state.Week) - 1);
        return ResetWeek(state) with { Week = weeks[index].Number, StageComplete = false };
    }

    internal static CardioTrackState ResetWeek(CardioTrackState state, DateOnly? weekStartsOn = null) =>
        state with
        {
            WeekSessions = 0,
            WeekFailures = 0,
            WeekNiggles = 0,
            WeekMissed = 0,
            Suggestion = TrackSuggestion.None,
            WeekStartedOn = weekStartsOn ?? state.WeekStartedOn
        };

    public int SessionsToStepUp(TrackStage stage, CardioTrackState state, WeekContext? context = null) =>
        Math.Max(0, Required(stage, context) - state.WeekSessions);

    /// <summary>
    /// Sessions needed at a week: the planned sessions from the schedule when known (never fewer than
    /// <see cref="WeekProgramSettings.SessionsToAdvance"/>, so walk → run at 2 a week still needs 3), else SessionsToAdvance.
    /// </summary>
    internal static int Required(TrackStage stage, WeekContext? context) =>
        context?.PlannedSessionsThisWeek is { } planned and > 0
            ? Math.Max(planned, stage.WeekProgram!.SessionsToAdvance)
            : stage.WeekProgram!.SessionsToAdvance;

    public CardioTrackState Clamp(TrackStage stage, CardioTrackState state)
    {
        var weeks = stage.WeekProgram!.Weeks;
        var week = weeks.Any(w => w.Number == state.Week) ? state.Week : weeks[^1].Number;
        return state with { Week = Math.Min(week, weeks[^1].Number) };
    }

    private static ProgramWeek CurrentWeek(WeekProgramSettings s, int week) =>
        s.Weeks.FirstOrDefault(w => w.Number == week) ?? s.Weeks[^1];

    private static int IndexOf(IReadOnlyList<ProgramWeek> weeks, int number)
    {
        for (var i = 0; i < weeks.Count; i++)
        {
            if (weeks[i].Number == number)
            {
                return i;
            }
        }

        return weeks.Count - 1;
    }
}

/// <summary>
/// The track-level strategy (brief § 4b.3): an ordered list of stages, each with its own strategy and exit
/// condition. Reaching a stage's exit (<see cref="StageExit.AtTop"/>) with another stage after it raises a
/// <see cref="TrackSuggestion.NextStage"/> suggestion; moving on happens only when the person accepts it.
/// </summary>
public sealed class StageStrategy
{
    private readonly IReadOnlyList<TrackStage> _stages;

    public StageStrategy(IReadOnlyList<TrackStage> stages)
    {
        if (stages is null || stages.Count == 0)
        {
            throw new A2S.Domain.Common.BusinessRuleViolationException("A track needs at least one stage");
        }

        _stages = stages;
    }

    public IReadOnlyList<TrackStage> Stages => _stages;

    public CardioTrackState Start() => _stages[0].Strategy.Start(_stages[0], 0);

    public TrackStage Current(CardioTrackState state) => _stages[Math.Clamp(state.StageIndex, 0, _stages.Count - 1)];

    public CardioPrescription Prescribe(CardioTrackState state) => Current(state).Strategy.Prescribe(Current(state), state);

    public int SessionsToStepUp(CardioTrackState state, WeekContext? context = null) =>
        Current(state).Strategy.SessionsToStepUp(Current(state), state, context);

    public bool HasNext(CardioTrackState state) => state.StageIndex + 1 < _stages.Count;

    public StageEvaluation Evaluate(CardioTrackState state, SessionCheck check, WeekContext? context = null)
    {
        var stage = Current(state);
        var evaluation = stage.Strategy.Evaluate(stage, state, check, context);
        if (evaluation.Outcome != TrackOutcome.StageComplete)
        {
            return evaluation;
        }

        if (stage.Exit == StageExit.Never)
        {
            return evaluation with { Outcome = TrackOutcome.AtTop, State = evaluation.State with { StageComplete = false } };
        }

        return HasNext(state)
            ? evaluation with
            {
                State = evaluation.State with { Suggestion = TrackSuggestion.NextStage },
                Reason = $"{evaluation.Reason}. Suggest moving on to {_stages[state.StageIndex + 1].Name}"
            }
            : evaluation;
    }

    /// <summary>Starts the next stage (the accepted NextStage suggestion). One step on the chart.</summary>
    public CardioTrackState MoveToNext(CardioTrackState state)
    {
        if (!HasNext(state))
        {
            throw new A2S.Domain.Common.BusinessRuleViolationException("There is no next stage");
        }

        var next = state.StageIndex + 1;
        return _stages[next].Strategy.Start(_stages[next], next) with { Step = state.Step + 1, WeekStartedOn = state.WeekStartedOn };
    }

    /// <summary>Keeps <paramref name="state"/> (from the previous stage list) inside these stages' ranges.</summary>
    public CardioTrackState Fit(CardioTrackState state, IReadOnlyList<TrackStage> previous)
    {
        var index = Math.Min(state.StageIndex, _stages.Count - 1);
        var stage = _stages[index];
        var sameKind = index == state.StageIndex && index < previous.Count && stage.Kind == previous[index].Kind;
        var current = sameKind ? state : stage.Strategy.Start(stage, index) with { Step = state.Step };
        return stage.Strategy.Clamp(stage, current);
    }
}
