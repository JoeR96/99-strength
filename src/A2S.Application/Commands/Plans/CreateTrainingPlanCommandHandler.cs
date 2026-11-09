using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Plans;

public sealed class CreateTrainingPlanCommandHandler : IRequestHandler<CreateTrainingPlanCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateTrainingPlanCommandHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<TrainingPlanDto>> Handle(CreateTrainingPlanCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(user);
        }

        WorkoutId? linkedWorkoutId = null;
        if (request.LinkedWorkoutId is { } workoutGuid)
        {
            var workout = await _workouts.GetByIdAsync(new WorkoutId(workoutGuid), cancellationToken);
            if (workout is null || workout.UserId != user.Value)
            {
                return Result.Failure<TrainingPlanDto>("Workout not found.", ErrorCode.NotFound);
            }

            linkedWorkoutId = workout.Id;
        }

        try
        {
            var plan = TrainingPlan.Create(
                user.Value,
                request.Name,
                request.RequestedStart,
                request.Schedule.Select(s => s.ToDomain()),
                request.StartWeightKg,
                new WeeklyAllowances(request.FlexibleMeals, request.AlcoholOccasions));

            if (linkedWorkoutId is { } id)
            {
                plan.LinkWorkout(id);
            }

            var currentActive = await _plans.GetActiveForUserAsync(user.Value, cancellationToken);
            if (currentActive is not null)
            {
                currentActive.Deactivate();
                _plans.Update(currentActive);
            }

            await _plans.AddAsync(plan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(plan.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<TrainingPlanDto>(ex);
        }
    }
}
