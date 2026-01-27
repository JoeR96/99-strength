using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.SubstituteExercise;

/// <summary>
/// Handler for SubstituteExerciseCommand.
/// Permanently replaces an exercise with another while preserving progression data.
/// </summary>
public sealed class SubstituteExerciseCommandHandler : IRequestHandler<SubstituteExerciseCommand, Result<SubstituteExerciseResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SubstituteExerciseCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<SubstituteExerciseResult>> Handle(SubstituteExerciseCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<SubstituteExerciseResult>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<SubstituteExerciseResult>("Workout not found.");
            }

            if (workout.UserId != userId)
            {
                return Result.Failure<SubstituteExerciseResult>("You can only modify your own workouts.");
            }

            var exerciseId = new ExerciseId(request.ExerciseId);
            var exercise = workout.GetExerciseById(exerciseId);

            if (exercise == null)
            {
                return Result.Failure<SubstituteExerciseResult>("Exercise not found in this workout.");
            }

            var originalName = workout.SubstituteExercise(
                exerciseId,
                request.NewExerciseName,
                request.NewHevyExerciseTemplateId);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new SubstituteExerciseResult
            {
                ExerciseId = request.ExerciseId,
                OriginalName = originalName,
                NewName = request.NewExerciseName,
                Success = true,
                Message = $"Successfully substituted '{originalName}' with '{request.NewExerciseName}'."
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<SubstituteExerciseResult>($"Failed to substitute exercise: {ex.Message}");
        }
    }
}
