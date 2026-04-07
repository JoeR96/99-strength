using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.RetrofixLinearTm;

public sealed class RetrofixLinearTmCommandHandler
    : IRequestHandler<RetrofixLinearTmCommand, Result<List<RetrofixLinearTmResult>>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RetrofixLinearTmCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<RetrofixLinearTmResult>>> Handle(
        RetrofixLinearTmCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId), cancellationToken);

            if (workout == null)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("Workout not found.");
            }

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<List<RetrofixLinearTmResult>>("You can only modify your own workouts.");
            }

            var exerciseId = new ExerciseId(request.ExerciseId);

            var changes = workout.RetrofixTrainingMaxHistory(exerciseId, request.OriginalStartingTm);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var results = changes
                .Select(c => new RetrofixLinearTmResult(c.Week, c.OldTm, c.NewTm))
                .ToList();

            return Result.Success(results);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<List<RetrofixLinearTmResult>>(ex.Message);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<RetrofixLinearTmResult>>($"Failed to retrofix TM history: {ex.Message}");
        }
    }
}
