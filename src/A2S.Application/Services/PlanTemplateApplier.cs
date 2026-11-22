using A2S.Application.Common;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.DTOs;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;

namespace A2S.Application.Services;

/// <summary>
/// Turns a <see cref="SuggestedPlanSpec"/> into the user's own aggregates in one unit of work: the A2S2
/// workout (started; any active workout paused), cardio protocols and tracks, mobility routines, habits
/// and the plan linking them. Nothing is special-cased afterwards: it is all editable like any other data.
/// Orchestration only; every rule lives in the aggregates.
/// </summary>
public sealed class PlanTemplateApplier
{
    public const string ReplaceNeedsConfirmation = "PlanReplaceNeedsConfirmation";

    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly ICardioProtocolRepository _protocols;
    private readonly IMobilityRoutineRepository _routines;
    private readonly IHabitRepository _habits;
    private readonly ICardioTrackRepository? _tracks;
    private readonly IExerciseLibraryProvider _library;
    private readonly IUnitOfWork _unitOfWork;

    public PlanTemplateApplier(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICardioProtocolRepository protocols,
        IMobilityRoutineRepository routines,
        IHabitRepository habits,
        IExerciseLibraryProvider library,
        IUnitOfWork unitOfWork,
        ICardioTrackRepository? tracks = null)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tracks = tracks;
    }

    /// <summary>
    /// Applies <paramref name="spec"/>. While a plan with the same name is active, returns it unchanged
    /// (idempotent). Another active plan is only replaced when <paramref name="confirmReplace"/> is true:
    /// it is made inactive (kept, with its history) and its workout paused.
    /// </summary>
    public async Task<Result<StartSuggestedPlanResult>> ApplyAsync(UserId userId, SuggestedPlanSpec spec, bool confirmReplace, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var currentPlan = await _plans.GetActiveForUserAsync(userId, ct);
        if (currentPlan is not null && string.Equals(currentPlan.Name, spec.Name, StringComparison.Ordinal))
        {
            return Result.Success(new StartSuggestedPlanResult(
                currentPlan.Id.Value, currentPlan.LinkedWorkoutId?.Value, currentPlan.StartDate, AlreadyStarted: true, Warnings: []));
        }

        if (currentPlan is not null && !confirmReplace)
        {
            return Result.Failure<StartSuggestedPlanResult>(
                $"You already have an active plan ({currentPlan.Name}). Confirm to replace it: it is kept, with its history, but no longer active.",
                ErrorCode.Conflict);
        }

        try
        {
            var workout = await CreateWorkoutAsync(userId, spec, ct);
            var protocols = await CreateCardioAsync(userId, spec, ct);
            var tracks = await EnsureTracksAsync(userId, spec, ct);
            var routines = await EnsureMobilityAsync(userId, spec, ct);
            await EnsureHabitsAsync(userId, spec, ct);

            var schedule = spec.Schedule.Select(s => new ScheduledSession(
                s.Day,
                s.Kind,
                s.LiftDay,
                s.Cardio is { } kind ? protocols[kind].Id : null,
                s.MobilityRoutine is { } name ? routines[name].Id : null,
                s.Label,
                s.WarmUp is { } warmUp ? routines[warmUp].Id : null,
                Finisher(s.Finisher, tracks),
                s.CardioTrack is { } trackKey && tracks.TryGetValue(trackKey, out var slotTrack) ? slotTrack.Id : null)).ToList();

            var plan = TrainingPlan.Create(userId, spec.Name, spec.StartDate, schedule, spec.StartWeightKg, spec.Allowances);
            foreach (var phase in spec.Phases)
            {
                plan.AddPhase(new PlanPhase(phase.Type, phase.From, phase.To, phase.Note, phase.Label, phase.Changes,
                    phase.Swaps?.Select(sw => new PhaseSwap(sw.FromExercise, sw.ToExercise)).ToList()));
            }

            foreach (var checkpoint in spec.Checkpoints)
            {
                plan.AddCheckpoint(new Checkpoint(checkpoint.Date, checkpoint.TargetKg, checkpoint.Note));
            }

            plan.LinkWorkout(workout.Id);

            if (currentPlan is not null)
            {
                currentPlan.Deactivate();
                _plans.Update(currentPlan);
            }

            await _plans.AddAsync(plan, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var warnings = PlanScheduleRules.Warnings(plan.Schedule, id => protocols.Values.FirstOrDefault(p => p.Id == id)?.Kind);
            return Result.Success(new StartSuggestedPlanResult(
                plan.Id.Value, workout.Id.Value, plan.StartDate, AlreadyStarted: false,
                warnings.Select(w => new ScheduleWarningDto(w.Code, w.Message)).ToList()));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<StartSuggestedPlanResult>(ex);
        }
        catch (InvalidOperationException ex)
        {
            // A template the library does not have: a deployment gap, not a user error.
            return Result.Failure<StartSuggestedPlanResult>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }

    private static SessionFinisher? Finisher(FinisherSpec? spec, IReadOnlyDictionary<string, CardioTrack> tracks) => spec switch
    {
        null => null,
        { Fixed: { } activity } => SessionFinisher.ForFixed(activity),
        { TrackKey: { } key } when tracks.TryGetValue(key, out var track) => SessionFinisher.ForTrack(track.Id),
        _ => null
    };

    /// <summary>The plan's A2S2 workout, started. Any currently active workout is paused, as SetActiveWorkout does.</summary>
    private async Task<Workout> CreateWorkoutAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var current = await _workouts.GetActiveWorkoutAsync(userId, ct);
        if (current is not null)
        {
            current.Deactivate();
            _workouts.Update(current);
        }

        var requests = WithSpecSupersets(spec);
        var exercises = ConfiguredExerciseFactory.Create(_library, requests);
        var workout = Workout.Create(userId, spec.Name, spec.Variant, exercises, spec.BlockSequence?.ToList());
        ConfiguredExerciseFactory.ApplyPairings(workout, exercises, requests);
        workout.Start();
        await _workouts.AddAsync(workout, ct);
        return workout;
    }

    /// <summary>
    /// Older specs name their supersets in <see cref="SuggestedPlanSpec.Supersets"/>; they become the
    /// exercises' own superset groups (numbered by the first exercise's order) so gym mode and Hevy see them.
    /// </summary>
    private static IReadOnlyList<Commands.CreateWorkout.CreateExerciseRequest> WithSpecSupersets(SuggestedPlanSpec spec) =>
        spec.Exercises.Select(e =>
        {
            if (e.SupersetGroup is not null)
            {
                return e;
            }

            var pair = spec.Supersets.FirstOrDefault(s => s.Day == e.AssignedDay
                && (string.Equals(s.First, e.TemplateName, StringComparison.OrdinalIgnoreCase) || string.Equals(s.Second, e.TemplateName, StringComparison.OrdinalIgnoreCase)));
            if (pair is null)
            {
                return e;
            }

            var first = spec.Exercises.First(x => x.AssignedDay == pair.Day && string.Equals(x.TemplateName, pair.First, StringComparison.OrdinalIgnoreCase));
            return e with { SupersetGroup = first.OrderInDay };
        }).ToList();

    private async Task<Dictionary<CardioProtocolKind, CardioProtocol>> CreateCardioAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var protocols = new Dictionary<CardioProtocolKind, CardioProtocol>();
        foreach (var cardio in spec.Cardio)
        {
            var protocol = cardio.Kind switch
            {
                CardioProtocolKind.NorwegianFourByFour => CardioProtocol.NorwegianFourByFour(userId),
                CardioProtocolKind.CouchToFiveK => CardioProtocol.CouchToFiveK(userId, cardio.WeekIndex ?? CardioProtocol.CouchToFiveKFirstWeek),
                CardioProtocolKind.Liss => CardioProtocol.Liss(userId, cardio.Minutes ?? CardioProtocol.LissMinimumMinutes, cardio.Note),
                _ => throw new BusinessRuleViolationException($"Unknown cardio protocol kind '{cardio.Kind}'")
            };

            await _protocols.AddAsync(protocol, ct);
            protocols[cardio.Kind] = protocol;
        }

        return protocols;
    }

    /// <summary>Starts each named track template, reusing a track of that template the user already has running.</summary>
    private async Task<Dictionary<string, CardioTrack>> EnsureTracksAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var result = new Dictionary<string, CardioTrack>(StringComparer.OrdinalIgnoreCase);
        if (spec.CardioTracks is not { Count: > 0 } keys || _tracks is null)
        {
            return result;
        }

        var existing = await _tracks.ListForUserAsync(userId, ct);
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var running = existing.FirstOrDefault(t => string.Equals(t.TemplateKey, key, StringComparison.OrdinalIgnoreCase) && t.Status != CardioTrackStatus.Completed);
            if (running is null)
            {
                // A track with a default start gate (walk → run) is created waiting for it, never running.
                running = CardioTrackTemplates.Create(key, userId, spec.StartDate, withGate: true);
                await _tracks.AddAsync(running, ct);
            }

            result[key] = running;
        }

        return result;
    }

    /// <summary>Creates each routine; one the user already has under the same name is kept (with their edits) and reused.</summary>
    private async Task<Dictionary<string, MobilityRoutine>> EnsureMobilityAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var existing = spec.ExtraRoutines is { Count: > 0 } ? await _routines.ListForUserAsync(userId, ct) : [];
        var routines = new Dictionary<string, MobilityRoutine>(StringComparer.OrdinalIgnoreCase);
        foreach (var routineSpec in spec.Routines)
        {
            var routine = existing.FirstOrDefault(r => string.Equals(r.Name, routineSpec.Name, StringComparison.OrdinalIgnoreCase));
            if (routine is null)
            {
                routine = MobilityRoutine.Create(userId, routineSpec.Name, routineSpec.TargetMinutes, routineSpec.Drills.Select(d => d.ToDomain()),
                    routineSpec.Kind ?? MobilityRoutine.DefaultKindFor(routineSpec.Name));
                await _routines.AddAsync(routine, ct);
            }

            routines[routineSpec.Name] = routine;
        }

        return routines;
    }

    /// <summary>
    /// Creates the habits the user does not have yet. A habit already present under the same external
    /// key (a Terran CC import) or the same name is kept and unarchived rather than duplicated.
    /// </summary>
    private async Task EnsureHabitsAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        if (spec.Habits.Count == 0)
        {
            return;
        }

        var existing = await _habits.ListForUserAsync(userId, includeArchived: true, ct);
        foreach (var habitSpec in spec.Habits)
        {
            var match = existing.FirstOrDefault(h => string.Equals(h.ExternalKey, habitSpec.ExternalKey, StringComparison.OrdinalIgnoreCase))
                ?? existing.FirstOrDefault(h => string.Equals(h.Name, habitSpec.Name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                if (match.Archived)
                {
                    match.Unarchive();
                    _habits.Update(match);
                }

                continue;
            }

            await _habits.AddAsync(HabitDefinition.Create(
                userId, habitSpec.Name, habitSpec.Type, habitSpec.Target, habitSpec.Minimum, habitSpec.Unit, habitSpec.Order, habitSpec.Icon, habitSpec.ExternalKey), ct);
        }
    }
}
