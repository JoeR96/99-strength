using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Cardio;

/// <summary>One logged session as the track saw it: the log, whether it counted, and what happened.</summary>
public sealed record CardioTrackSession(
    string Key,
    DateOnly Date,
    string Prescription,
    int StageIndex,
    bool Completed,
    bool Qualified,
    TrackOutcome Outcome,
    int DurationSeconds,
    EffortRating Effort,
    int? Rpe,
    PainFlag Pain,
    string? PainArea,
    int? AvgHeartRate,
    int? HighIntensityMinutes,
    int? Zone5Minutes,
    int? Floors,
    decimal? DistanceMeters,
    decimal? IntensityValue,
    CardioLogSource Source,
    decimal? WorkMinutes = null);

/// <summary>One line of the track's audit trail: what changed, why, and the prescription before and after.</summary>
public sealed record CardioTrackAuditEntry(
    DateTimeOffset AtUtc,
    DateOnly? Date,
    string Action,
    string Reason,
    string Before,
    string After,
    string? SessionKey);

/// <summary>A benchmark ("floors in 5 min", "5 K time") every <see cref="EveryWeeks"/> weeks.</summary>
public sealed record TrackBenchmark(string Name, string Unit, bool HigherIsBetter, int EveryWeeks = 4);

public sealed record BenchmarkResult(DateOnly Date, decimal Value, string? Notes, bool IsPersonalBest);

/// <summary>Everything an evaluation can change, captured before it so undo restores it exactly.</summary>
public sealed record CardioTrackSnapshot(
    CardioTrackState State,
    CardioTrackStatus Status,
    IReadOnlyList<TrackStage> Stages,
    int SessionCount,
    int AuditCount,
    IReadOnlyList<GateCondition>? StartGate = null);

/// <summary>The result of logging a session against a track.</summary>
public sealed record TrackEvaluationResult(TrackOutcome Outcome, string Reason, bool Qualified, bool AlreadyRecorded, CardioPrescription Next);

/// <summary>
/// A cardio progression track (aggregate root): an ordered list of stages, each with its own
/// strategy, plus where the person is, every session it judged, an audit trail and one level of undo.
/// Moving between stages (the stage strategy) is a suggestion the person accepts.
/// </summary>
public sealed class CardioTrack : AggregateRoot<CardioTrackId>
{
    private List<TrackStage> _stages = new();
    private List<CardioTrackSession> _sessions = new();
    private List<CardioTrackAuditEntry> _audit = new();
    private List<BenchmarkResult> _benchmarks = new();
    private List<GateCondition> _startGate = new();

    private CardioTrack(
        CardioTrackId id,
        UserId userId,
        string name,
        CardioModality modality,
        IEnumerable<TrackStage> stages,
        TrackBenchmark? benchmark,
        DateOnly startedOn,
        string? templateKey,
        string? notes)
        : base(id)
    {
        UserId = userId;
        Name = name;
        Modality = modality;
        _stages = stages.ToList();
        Benchmark = benchmark;
        StartedOn = startedOn;
        TemplateKey = templateKey;
        Notes = notes;
        Status = CardioTrackStatus.Active;
        State = new StageStrategy(_stages).Start() with { WeekStartedOn = startedOn };
    }

    // EF Core constructor
    private CardioTrack()
    {
        Name = string.Empty;
        State = null!;
    }

    public UserId UserId { get; private set; }
    public string Name { get; private set; }
    public CardioModality Modality { get; private set; }
    public CardioTrackStatus Status { get; private set; }
    public string? TemplateKey { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly StartedOn { get; private set; }
    public TrackBenchmark? Benchmark { get; private set; }
    public CardioTrackState State { get; private set; }
    public CardioTrackSnapshot? LastSnapshot { get; private set; }

    public IReadOnlyList<TrackStage> Stages => _stages.AsReadOnly();
    public IReadOnlyList<CardioTrackSession> Sessions => _sessions.AsReadOnly();
    public IReadOnlyList<CardioTrackAuditEntry> Audit => _audit.AsReadOnly();
    public IReadOnlyList<BenchmarkResult> Benchmarks => _benchmarks.AsReadOnly();

    /// <summary>Conditions to meet before the track starts. Empty: no gate.</summary>
    public IReadOnlyList<GateCondition> StartGate => _startGate.AsReadOnly();

    /// <summary>The stage strategy over this track's stages (ordered stages, exits, next-stage suggestion).</summary>
    public StageStrategy Strategy => new(_stages);

    public TrackStage CurrentStage => Strategy.Current(State);
    public CardioPrescription Prescription => Strategy.Prescribe(State);
    public int SessionsToStepUp => Strategy.SessionsToStepUp(State);

    /// <summary>Sessions to the next step given what the plan schedules this week.</summary>
    public int SessionsToStepUpGiven(WeekContext? context) => Strategy.SessionsToStepUp(State, context);
    public bool CanUndo => LastSnapshot is not null;

    /// <summary>Hard tracks (zone 4–5 work) count toward the weekly high-intensity guardrails.</summary>
    public bool IsHard => _stages.Any(s => s.Target == TrackEffortTarget.Hard);

    public static CardioTrack Create(
        UserId userId,
        string name,
        CardioModality modality,
        IReadOnlyList<TrackStage> stages,
        DateOnly startedOn,
        TrackBenchmark? benchmark = null,
        string? templateKey = null,
        string? notes = null,
        IReadOnlyList<GateCondition>? startGate = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(userId.Value), "A track needs an owner");
        CheckRule(!string.IsNullOrWhiteSpace(name), "Track name is required");
        CheckRule(stages is { Count: > 0 }, "A track needs at least one stage");
        CheckBenchmark(benchmark);

        var track = new CardioTrack(new CardioTrackId(Guid.NewGuid()), userId, name.Trim(), modality, stages, benchmark,
            startedOn, templateKey, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
        if (startGate is { Count: > 0 })
        {
            track.InitialiseGate(startGate);
        }

        return track;
    }

    /// <summary>
    /// Records a session and evaluates it. Idempotent on <paramref name="key"/> (a completion id, or
    /// "hevy:{workoutId}:{exerciseIndex}"). Paused tracks and tracks waiting on a suggestion record
    /// the session without moving.
    /// </summary>
    public TrackEvaluationResult LogSession(
        string key,
        DateOnly date,
        CardioLog log,
        bool completedAsPrescribed,
        HeartRateZones? zones,
        DateTimeOffset at,
        WeekContext? week = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(key), "A session key is required");
        ArgumentNullException.ThrowIfNull(log);

        var existing = _sessions.FirstOrDefault(s => s.Key == key);
        if (existing is not null)
        {
            return new TrackEvaluationResult(existing.Outcome, "Already recorded", existing.Qualified, true, Prescription);
        }

        TakeSnapshot();
        var stage = CurrentStage;
        var stageIndex = State.StageIndex;
        var before = Prescription;
        var check = SessionCheck.From(stage, log, completedAsPrescribed, zones, before.WorkMinutes);
        var (qualified, why) = check.Judge(stage);

        StageEvaluation evaluation;
        if (Status != CardioTrackStatus.Active)
        {
            evaluation = new StageEvaluation(State, TrackOutcome.Paused, Status == CardioTrackStatus.Gated
                ? "Not started (gate): recorded, not evaluated"
                : "Track paused: recorded, not evaluated");
        }
        else if (State.Suggestion != TrackSuggestion.None)
        {
            evaluation = new StageEvaluation(State, TrackOutcome.AwaitingDecision,
                $"Waiting for you to accept or decline: {Describe(State.Suggestion)}. Recorded, not evaluated");
        }
        else
        {
            evaluation = Strategy.Evaluate(State, check, week);
            if (evaluation.State.Week != State.Week || evaluation.State.StageIndex != State.StageIndex)
            {
                // A new week starts the day after the session that earned it.
                evaluation = evaluation with { State = evaluation.State with { WeekStartedOn = date.AddDays(1) } };
            }
        }

        State = evaluation.State;
        _sessions.Add(new CardioTrackSession(key, date, before.Text, stageIndex,
            check.Completed, qualified && evaluation.Outcome is not (TrackOutcome.Paused or TrackOutcome.AwaitingDecision),
            evaluation.Outcome, log.DurationSeconds, check.Effort, log.EffectiveRpe, log.Pain, log.PainArea, log.AvgHeartRate,
            log.HighIntensityMinutes, log.Zone5Minutes, log.Floors, log.DistanceMeters, log.IntensityValue, log.Source, log.WorkMinutes));
        AddAudit(at, date, evaluation.Outcome.ToString(), evaluation.Reason, before.Title, key);

        return new TrackEvaluationResult(evaluation.Outcome, evaluation.Reason, qualified, false, Prescription);
    }

    /// <summary>Accepts the pending suggestion: repeat the week, drop back a week, or move to the next stage.</summary>
    public void AcceptSuggestion(DateTimeOffset at, DateOnly? today = null)
    {
        CheckRule(State.Suggestion != TrackSuggestion.None, "There is no suggestion to accept");
        TakeSnapshot();
        var before = Prescription.Title;
        var stage = CurrentStage;

        switch (State.Suggestion)
        {
            case TrackSuggestion.RepeatWeek:
                State = WeekProgramStrategy.ResetWeek(State, today);
                AddAudit(at, null, "RepeatAccepted", "You chose to repeat the week", before, null);
                break;
            case TrackSuggestion.DropBackWeek:
                State = stage.Kind == TrackStageKind.WeekProgram
                    ? WeekProgramStrategy.DropBack(stage, State) with { WeekStartedOn = today ?? State.WeekStartedOn }
                    : State with { Suggestion = TrackSuggestion.None };
                AddAudit(at, null, "DropBackAccepted", "You chose to drop back a week", before, null);
                break;
            case TrackSuggestion.NextStage:
                State = Strategy.MoveToNext(State) with { WeekStartedOn = today ?? State.WeekStartedOn };
                AddAudit(at, null, "StageAccepted", $"Moved on to {CurrentStage.Name}", before, null);
                break;
        }
    }

    /// <summary>Declines the pending suggestion: advance anyway (repeat), carry on (drop back), or stay (next stage).</summary>
    public void DeclineSuggestion(DateTimeOffset at, DateOnly? today = null)
    {
        CheckRule(State.Suggestion != TrackSuggestion.None, "There is no suggestion to decline");
        TakeSnapshot();
        var before = Prescription.Title;
        var stage = CurrentStage;
        var suggestion = State.Suggestion;

        switch (suggestion)
        {
            case TrackSuggestion.RepeatWeek:
                var advanced = WeekProgramStrategy.Advance(stage, State, "Overridden").State with { WeekStartedOn = today ?? State.WeekStartedOn };
                State = advanced.StageComplete && Strategy.HasNext(State) && stage.Exit == StageExit.AtTop
                    ? advanced with { Suggestion = TrackSuggestion.NextStage }
                    : advanced;
                break;
            case TrackSuggestion.DropBackWeek:
                State = WeekProgramStrategy.ResetWeek(State, today);
                break;
            default:
                State = State with { Suggestion = TrackSuggestion.None };
                break;
        }

        AddAudit(at, null, "SuggestionOverridden", $"You declined: {Describe(suggestion)}", before, null);
    }

    /// <summary>Replaces the stages (edited parameters); the position is kept and pulled inside the new ranges.</summary>
    public void UpdateStages(IReadOnlyList<TrackStage> stages, DateTimeOffset at)
    {
        CheckRule(stages is { Count: > 0 }, "A track needs at least one stage");
        TakeSnapshot();
        var before = Prescription.Title;
        var previous = _stages.ToList();
        _stages = stages.ToList();
        State = Strategy.Fit(State, previous);
        AddAudit(at, null, "Edited", "Track settings changed", before, null);
    }

    public void Rename(string name)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Track name is required");
        Name = name.Trim();
    }

    public void SetNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void SetBenchmark(TrackBenchmark? benchmark)
    {
        CheckBenchmark(benchmark);
        Benchmark = benchmark;
    }

    /// <summary>
    /// Sets (or, with an empty list, clears) the start gate. A gate on an active track holds it at
    /// "Not started — gate"; clearing the gate of a gated track starts it.
    /// </summary>
    public void SetStartGate(IReadOnlyList<GateCondition> conditions, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        CheckRule(Status != CardioTrackStatus.Completed, "A completed track has no start gate");
        var validated = conditions.Select(GateCondition.Validate).ToList();

        TakeSnapshot();
        var before = Prescription.Title;
        _startGate = validated;
        if (validated.Count > 0 && Status == CardioTrackStatus.Active)
        {
            Status = CardioTrackStatus.Gated;
        }
        else if (validated.Count == 0 && Status == CardioTrackStatus.Gated)
        {
            Status = CardioTrackStatus.Active;
        }

        AddAudit(at, null, "GateSet", validated.Count == 0 ? "Start gate removed" : $"Start gate set ({validated.Count} conditions)", before, null);
    }

    /// <summary>
    /// Starts a gated track. The query offers this once every condition is met; the person accepts it
    /// (it is never automatic), so the check whether the gate is met lives with the caller.
    /// </summary>
    public void StartFromGate(DateOnly today, DateTimeOffset at)
    {
        CheckRule(Status == CardioTrackStatus.Gated, "Only a track waiting on its gate can be started");
        TakeSnapshot();
        var before = Prescription.Title;
        Status = CardioTrackStatus.Active;
        State = State with { WeekStartedOn = today };
        AddAudit(at, today, "GateOpened", "Start gate met: track started", before, null);
    }

    /// <summary>A gate set at creation: no snapshot or audit line, so there is nothing to undo.</summary>
    internal void InitialiseGate(IReadOnlyList<GateCondition> conditions)
    {
        _startGate = conditions.Select(GateCondition.Validate).ToList();
        Status = CardioTrackStatus.Gated;
    }

    public void Pause()
    {
        CheckRule(Status == CardioTrackStatus.Active, "Only an active track can be paused");
        Status = CardioTrackStatus.Paused;
    }

    public void Resume()
    {
        CheckRule(Status == CardioTrackStatus.Paused, "Only a paused track can be resumed");
        Status = CardioTrackStatus.Active;
    }

    /// <summary>Restores the exact state before the last evaluation, suggestion decision or edit.</summary>
    public void UndoLastEvaluation()
    {
        CheckRule(LastSnapshot is not null, "There is nothing to undo");
        var snapshot = LastSnapshot!;
        State = snapshot.State;
        Status = snapshot.Status;
        _stages = snapshot.Stages.ToList();
        _sessions = _sessions.Take(snapshot.SessionCount).ToList();
        _audit = _audit.Take(snapshot.AuditCount).ToList();
        if (snapshot.StartGate is not null)
        {
            _startGate = snapshot.StartGate.ToList();
        }

        LastSnapshot = null;
    }

    /// <summary>A benchmark is due every N weeks after the last one (or after the track started).</summary>
    public bool IsBenchmarkDue(DateOnly today)
    {
        if (Benchmark is null || Status != CardioTrackStatus.Active)
        {
            return false;
        }

        var last = _benchmarks.Count == 0 ? StartedOn : _benchmarks.Max(b => b.Date);
        return today.DayNumber - last.DayNumber >= Benchmark.EveryWeeks * 7;
    }

    /// <summary>Records a benchmark result. Never changes progression.</summary>
    public BenchmarkResult RecordBenchmark(DateOnly date, decimal value, string? notes)
    {
        CheckRule(Benchmark is not null, "This track has no benchmark");
        CheckRule(value > 0, "Benchmark result must be positive");

        var best = _benchmarks.Count == 0
            ? (decimal?)null
            : Benchmark!.HigherIsBetter ? _benchmarks.Max(b => b.Value) : _benchmarks.Min(b => b.Value);
        var isBest = best is null || (Benchmark!.HigherIsBetter ? value > best : value < best);
        var result = new BenchmarkResult(date, value, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), isBest);
        _benchmarks.Add(result);
        return result;
    }

    private void TakeSnapshot() =>
        LastSnapshot = new CardioTrackSnapshot(State, Status, _stages.ToList(), _sessions.Count, _audit.Count, _startGate.ToList());

    private void AddAudit(DateTimeOffset at, DateOnly? date, string action, string reason, string before, string? key) =>
        _audit.Add(new CardioTrackAuditEntry(at.ToUniversalTime(), date, action, reason,
            before, Prescription.Title, key));

    private string Describe(TrackSuggestion suggestion) => suggestion switch
    {
        TrackSuggestion.RepeatWeek => "repeat this week",
        TrackSuggestion.DropBackWeek => "drop back a week",
        TrackSuggestion.NextStage => State.StageIndex + 1 < _stages.Count ? $"move on to {_stages[State.StageIndex + 1].Name}" : "move on",
        _ => "nothing"
    };

    private static void CheckBenchmark(TrackBenchmark? benchmark)
    {
        if (benchmark is null)
        {
            return;
        }

        CheckRule(!string.IsNullOrWhiteSpace(benchmark.Name), "Benchmark name is required");
        CheckRule(benchmark.EveryWeeks is >= 1 and <= 52, "Benchmark interval must be 1–52 weeks");
    }
}
