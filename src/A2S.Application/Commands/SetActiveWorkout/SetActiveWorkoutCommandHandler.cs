using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.SetActiveWorkout;

/// <summary>
/// Handler for SetActiveWorkoutCommand.
/// Sets the specified workout as active and deactivates any other active workouts.
/// </summary>
public sealed class SetActiveWorkoutCommandHandler : IRequestHandler<SetActiveWorkoutCommand, Result<bool>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SetActiveWorkoutCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<bool>> Handle(SetActiveWorkoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<bool>("User must be authenticated.");
            }

            // Get the workout to activate
            var workoutToActivate = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workoutToActivate == null)
            {
                return Result.Failure<bool>("Workout not found.");
            }

            // Verify the workout belongs to the current user
            if (workoutToActivate.UserId != userId)
            {
                return Result.Failure<bool>("You can only activate your own workouts.");
            }

            // Deactivate any currently active workout
            var currentActive = await _workoutRepository.GetActiveWorkoutAsync(userId, cancellationToken);
            if (currentActive != null && currentActive.Id != workoutToActivate.Id)
            {
                currentActive.Deactivate();
                _workoutRepository.Update(currentActive);
            }

            // Activate the requested workout
            workoutToActivate.SetAsActive();
            _workoutRepository.Update(workoutToActivate);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(true);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Failed to set active workout: {ex.Message}");
        }
    }
}
