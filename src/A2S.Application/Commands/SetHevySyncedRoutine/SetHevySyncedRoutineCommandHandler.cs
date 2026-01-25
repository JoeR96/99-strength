using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.SetHevySyncedRoutine;

/// <summary>
/// Handler for SetHevySyncedRoutineCommand.
/// Records that a routine was synced to Hevy for a specific week/day.
/// </summary>
public sealed class SetHevySyncedRoutineCommandHandler : IRequestHandler<SetHevySyncedRoutineCommand, Result<bool>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SetHevySyncedRoutineCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<bool>> Handle(SetHevySyncedRoutineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<bool>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<bool>("Workout not found.");
            }

            if (workout.UserId != userId)
            {
                return Result.Failure<bool>("You can only modify your own workouts.");
            }

            workout.SetHevySyncedRoutine(request.WeekNumber, request.DayNumber, request.RoutineId);
            _workoutRepository.Update(workout);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(true);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Failed to set synced routine: {ex.Message}");
        }
    }
}
