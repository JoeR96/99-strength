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
    private readonly ICardioTrackRepository? _tracks;

    public StartSuggestedPlanCommandHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICardioProtocolRepository protocols,
        IMobilityRoutineRepository routines,
        IHabitRepository habits,
        IExerciseLibraryProvider library,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        ICardioTrackRepository? tracks = null)
    {
        _tracks = tracks;
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

        var spec = CutTo85Nov2026.Build(new SuggestedPlanInputs(
            request.BenchTm, request.SquatTm, request.OhpTm, request.DeadliftTm, request.StartWeightKg, _clock.Today()));

        // The original one-tap endpoint has always replaced whatever plan was active; the template
        // endpoint (ApplyPlanTemplate) is the one that asks first.
        var applier = new PlanTemplateApplier(_plans, _workouts, _protocols, _routines, _habits, _library, _unitOfWork, _tracks);
        return await applier.ApplyAsync(user.Value, spec, confirmReplace: true, cancellationToken);
    }
}
