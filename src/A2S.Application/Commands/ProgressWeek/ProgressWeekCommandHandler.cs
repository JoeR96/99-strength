using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.ProgressWeek;

/// <summary>
/// Handler for ProgressWeekCommand.
/// Progresses the workout to the next week, handling block transitions and deload weeks.
/// </summary>
public sealed class ProgressWeekCommandHandler : IRequestHandler<ProgressWeekCommand, Result<ProgressWeekResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ProgressWeekCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<ProgressWeekResult>> Handle(ProgressWeekCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<ProgressWeekResult>("User must be authenticated.");
            }

            // Get the workout
            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<ProgressWeekResult>("Workout not found.");
            }

            // Verify the workout belongs to the current user
            if (workout.UserId != userId)
            {
                return Result.Failure<ProgressWeekResult>("You can only progress your own workouts.");
            }

            // Verify the workout is active
            if (workout.Status != WorkoutStatus.Active)
            {
                return Result.Failure<ProgressWeekResult>("Workout must be active to progress to next week.");
            }

            // Check if already at the final week
            if (workout.CurrentWeek >= workout.TotalWeeks)
            {
                return Result.Failure<ProgressWeekResult>("Workout has already reached the final week.");
            }

            // Capture previous state
            var previousWeek = workout.CurrentWeek;

            // Progress to the next week
            workout.ProgressToNextWeek();

            // Determine if the new week is a deload week (weeks 7, 14, 21)
            var isDeloadWeek = workout.IsDeloadWeek();

            // Check if program completed (after progressing to final deload)
            var isProgramComplete = workout.Status == WorkoutStatus.Completed;

            // Save changes
            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new ProgressWeekResult
            {
                PreviousWeek = previousWeek,
                NewWeek = workout.CurrentWeek,
                NewBlock = workout.CurrentBlock,
                IsDeloadWeek = isDeloadWeek,
                IsProgramComplete = isProgramComplete
            });
        }
        catch (InvalidOperationException ex)
        {
            // Domain rule violations throw InvalidOperationException
            return Result.Failure<ProgressWeekResult>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<ProgressWeekResult>($"Failed to progress week: {ex.Message}");
        }
    }
}
