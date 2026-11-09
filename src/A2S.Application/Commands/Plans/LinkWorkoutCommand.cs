using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Links the A2S2 workout whose completed days count as the plan's lifts.</summary>
public sealed record LinkWorkoutCommand(Guid PlanId, Guid WorkoutId) : ICommand<Result<bool>>;

public sealed class LinkWorkoutCommandHandler : IRequestHandler<LinkWorkoutCommand, Result<bool>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public LinkWorkoutCommandHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _plans = plans;
        _workouts = workouts;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(LinkWorkoutCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<bool>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<bool>(load);
        }

        var workout = await _workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout is null || workout.UserId != user.Value)
        {
            return Result.Failure<bool>("Workout not found.", ErrorCode.NotFound);
        }

        load.Value.LinkWorkout(workout.Id);
        _plans.Update(load.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
