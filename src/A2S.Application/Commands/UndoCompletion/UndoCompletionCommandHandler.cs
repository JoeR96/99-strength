using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.UndoCompletion;

/// <summary>
/// Handler for UndoCompletionCommand.
/// Undoes the last completed workout day by restoring progression state.
/// </summary>
public sealed class UndoCompletionCommandHandler : IRequestHandler<UndoCompletionCommand, Result<UndoCompletionResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UndoCompletionCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<UndoCompletionResult>> Handle(
        UndoCompletionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<UndoCompletionResult>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<UndoCompletionResult>("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<UndoCompletionResult>("You do not have permission to modify this workout.");
            }

            var undoResult = workout.UndoLastCompletion();

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new UndoCompletionResult
            {
                Day = undoResult.Day,
                WeekNumber = undoResult.WeekNumber,
                WeekRolledBack = undoResult.WeekRolledBack,
                NewCurrentWeek = workout.CurrentWeek,
                NewCurrentDay = workout.CurrentDay,
                Message = undoResult.WeekRolledBack
                    ? $"Undid Day {(int)undoResult.Day} completion and rolled back to Week {undoResult.WeekNumber}"
                    : $"Undid Day {(int)undoResult.Day} completion"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<UndoCompletionResult>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<UndoCompletionResult>($"Failed to undo completion: {ex.Message}");
        }
    }
}
