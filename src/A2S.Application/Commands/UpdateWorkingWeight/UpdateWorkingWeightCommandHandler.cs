using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.UpdateWorkingWeight;

/// <summary>
/// Handler for UpdateWorkingWeightCommand.
/// Updates the working weight for accessory exercises (RepsPerSet or MinimalSets).
/// Linear progression exercises must use skip progression instead.
/// </summary>
public sealed class UpdateWorkingWeightCommandHandler : IRequestHandler<UpdateWorkingWeightCommand, Result>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateWorkingWeightCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result> Handle(UpdateWorkingWeightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure("You can only modify your own workouts.");
            }

            var exerciseId = new ExerciseId(request.ExerciseId);
            var weight = Weight.Create(request.NewWeight, request.Unit);
            workout.UpdateExerciseWorkingWeight(exerciseId, weight);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to update working weight: {ex.Message}");
        }
    }
}
