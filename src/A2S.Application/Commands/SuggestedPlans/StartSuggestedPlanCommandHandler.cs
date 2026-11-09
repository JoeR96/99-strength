using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Commands.SuggestedPlans;

public sealed class StartSuggestedPlanCommandHandler : IRequestHandler<StartSuggestedPlanCommand, Result<StartSuggestedPlanResult>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly ICardioProtocolRepository _protocols;
    private readonly IMobilityRoutineRepository _routines;
    private readonly IHabitRepository _habits;
    private readonly IExerciseLibraryProvider _library;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public StartSuggestedPlanCommandHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICardioProtocolRepository protocols,
        IMobilityRoutineRepository routines,
        IHabitRepository habits,
        IExerciseLibraryProvider library,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<StartSuggestedPlanResult>> Handle(StartSuggestedPlanCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<StartSuggestedPlanResult>(user);
        }

        var userId = user.Value;
        var currentPlan = await _plans.GetActiveForUserAsync(userId, cancellationToken);
        if (currentPlan is not null && string.Equals(currentPlan.Name, CutTo85Nov2026.Name, StringComparison.Ordinal))
        {
            return Result.Success(new StartSuggestedPlanResult(
                currentPlan.Id.Value,
                currentPlan.LinkedWorkoutId?.Value,
                currentPlan.StartDate,
                AlreadyStarted: true,
                Warnings: []));
        }

        var spec = CutTo85Nov2026.Build(new SuggestedPlanInputs(
            request.BenchTm, request.SquatTm, request.OhpTm, request.DeadliftTm, request.StartWeightKg, _clock.Today()));

        try
        {
            var workout = await CreateWorkoutAsync(userId, spec, cancellationToken);
            var protocols = await CreateCardioAsync(userId, spec, cancellationToken);
            var routines = await CreateMobilityAsync(userId, spec, cancellationToken);
            await EnsureHabitsAsync(userId, spec, cancellationToken);

            var schedule = spec.Schedule.Select(s => new ScheduledSession(
                s.Day,
                s.Kind,
                s.LiftDay,
                s.Cardio is { } kind ? protocols[kind].Id : null,
                s.MobilityRoutine is { } name ? routines[name].Id : null,
                s.Label)).ToList();

            var plan = TrainingPlan.Create(userId, spec.Name, spec.StartDate, schedule, spec.StartWeightKg, spec.Allowances);
            foreach (var phase in spec.Phases)
            {
                plan.AddPhase(new PlanPhase(phase.Type, phase.From, phase.To, phase.Note));
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

            await _plans.AddAsync(plan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var warnings = PlanScheduleRules.Warnings(
                plan.Schedule,
                id => protocols.Values.FirstOrDefault(p => p.Id == id)?.Kind);

            return Result.Success(new StartSuggestedPlanResult(
                plan.Id.Value,
                workout.Id.Value,
                plan.StartDate,
                AlreadyStarted: false,
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

    /// <summary>The seed's A2S2 workout, started. Any currently active workout is paused, as SetActiveWorkout does.</summary>
    private async Task<Workout> CreateWorkoutAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var current = await _workouts.GetActiveWorkoutAsync(userId, ct);
        if (current is not null)
        {
            current.Deactivate();
            _workouts.Update(current);
        }

        var exercises = ConfiguredExerciseFactory.Create(_library, spec.Exercises);
        var workout = Workout.Create(userId, spec.Name, spec.Variant, exercises);
        workout.Start();
        await _workouts.AddAsync(workout, ct);
        return workout;
    }

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

    private async Task<Dictionary<string, MobilityRoutine>> CreateMobilityAsync(UserId userId, SuggestedPlanSpec spec, CancellationToken ct)
    {
        var routines = new Dictionary<string, MobilityRoutine>(StringComparer.OrdinalIgnoreCase);
        foreach (var routineSpec in new[] { spec.MobilityA, spec.MobilityB })
        {
            var routine = MobilityRoutine.Create(userId, routineSpec.Name, routineSpec.TargetMinutes, routineSpec.Drills.Select(d => d.ToDomain()));
            await _routines.AddAsync(routine, ct);
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

            var habit = HabitDefinition.Create(
                userId,
                habitSpec.Name,
                habitSpec.Type,
                habitSpec.Target,
                habitSpec.Minimum,
                habitSpec.Unit,
                habitSpec.Order,
                habitSpec.Icon,
                habitSpec.ExternalKey);

            await _habits.AddAsync(habit, ct);
        }
    }
}
