using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.UpdateExercises;

/// <summary>
/// Handler for UpdateExercisesCommand.
/// Updates one or more exercises in a workout, supporting batch updates.
/// </summary>
public sealed class UpdateExercisesCommandHandler : IRequestHandler<UpdateExercisesCommand, Result<UpdateExercisesResult>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateExercisesCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<UpdateExercisesResult>> Handle(UpdateExercisesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<UpdateExercisesResult>("User must be authenticated.");
            }

            var workout = await _workoutRepository.GetByIdAsync(
                new WorkoutId(request.WorkoutId),
                cancellationToken);

            if (workout == null)
            {
                return Result.Failure<UpdateExercisesResult>("Workout not found.");
            }

            if (workout.UserId != userId)
            {
                return Result.Failure<UpdateExercisesResult>("You can only modify your own workouts.");
            }

            var results = new List<ExerciseUpdateResult>();
            var successCount = 0;

            foreach (var update in request.Updates)
            {
                var result = ApplyUpdate(workout, update);
                results.Add(result);
                if (result.Success)
                {
                    successCount++;
                }
            }

            // Only save if at least one update succeeded
            if (successCount > 0)
            {
                _workoutRepository.Update(workout);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new UpdateExercisesResult
            {
                WorkoutId = request.WorkoutId,
                UpdatedCount = successCount,
                Results = results
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateExercisesResult>($"Failed to update exercises: {ex.Message}");
        }
    }

    private static ExerciseUpdateResult ApplyUpdate(Workout workout, ExerciseUpdateRequest update)
    {
        var exerciseId = new ExerciseId(update.ExerciseId);
        var exercise = workout.GetExerciseById(exerciseId);

        if (exercise == null)
        {
            return new ExerciseUpdateResult
            {
                ExerciseId = update.ExerciseId,
                ExerciseName = "Unknown",
                Success = false,
                Message = "Exercise not found in this workout."
            };
        }

        try
        {
            // Determine what type of update to apply based on the progression type
            if (exercise.Progression is LinearProgressionStrategy linearProgression)
            {
                if (update.TrainingMaxValue.HasValue)
                {
                    var previousTm = linearProgression.TrainingMax;
                    var previousValue = $"{previousTm.Value} {previousTm.Unit}";

                    var newTm = TrainingMax.Create(
                        update.TrainingMaxValue.Value,
                        update.TrainingMaxUnit ?? previousTm.Unit);

                    workout.AdjustTrainingMax(exerciseId, newTm, update.Reason);

                    return new ExerciseUpdateResult
                    {
                        ExerciseId = update.ExerciseId,
                        ExerciseName = exercise.Name,
                        Success = true,
                        Message = "Training Max updated successfully.",
                        PreviousValue = previousValue,
                        NewValue = $"{newTm.Value} {newTm.Unit}"
                    };
                }

                return new ExerciseUpdateResult
                {
                    ExerciseId = update.ExerciseId,
                    ExerciseName = exercise.Name,
                    Success = false,
                    Message = "No Training Max value provided for Linear progression exercise."
                };
            }
            else if (exercise.Progression is RepsPerSetStrategy repsStrategy)
            {
                if (update.WeightValue.HasValue)
                {
                    var previousWeight = repsStrategy.CurrentWeight;
                    var previousValue = $"{previousWeight.Value} {previousWeight.Unit}";

                    var newWeight = Weight.Create(
                        update.WeightValue.Value,
                        update.WeightUnit ?? previousWeight.Unit);

                    workout.AdjustWeight(exerciseId, newWeight);

                    return new ExerciseUpdateResult
                    {
                        ExerciseId = update.ExerciseId,
                        ExerciseName = exercise.Name,
                        Success = true,
                        Message = "Weight updated successfully.",
                        PreviousValue = previousValue,
                        NewValue = $"{newWeight.Value} {newWeight.Unit}"
                    };
                }

                return new ExerciseUpdateResult
                {
                    ExerciseId = update.ExerciseId,
                    ExerciseName = exercise.Name,
                    Success = false,
                    Message = "No weight value provided for RepsPerSet progression exercise."
                };
            }
            else if (exercise.Progression is MinimalSetsStrategy minimalSetsStrategy)
            {
                if (update.WeightValue.HasValue)
                {
                    var previousWeight = minimalSetsStrategy.CurrentWeight;
                    var previousValue = $"{previousWeight.Value} {previousWeight.Unit}";

                    var newWeight = Weight.Create(
                        update.WeightValue.Value,
                        update.WeightUnit ?? previousWeight.Unit);

                    workout.AdjustWeight(exerciseId, newWeight);

                    return new ExerciseUpdateResult
                    {
                        ExerciseId = update.ExerciseId,
                        ExerciseName = exercise.Name,
                        Success = true,
                        Message = "Weight updated successfully.",
                        PreviousValue = previousValue,
                        NewValue = $"{newWeight.Value} {newWeight.Unit}"
                    };
                }

                return new ExerciseUpdateResult
                {
                    ExerciseId = update.ExerciseId,
                    ExerciseName = exercise.Name,
                    Success = false,
                    Message = "No weight value provided for MinimalSets progression exercise."
                };
            }

            return new ExerciseUpdateResult
            {
                ExerciseId = update.ExerciseId,
                ExerciseName = exercise.Name,
                Success = false,
                Message = "Unknown progression type."
            };
        }
        catch (Exception ex)
        {
            return new ExerciseUpdateResult
            {
                ExerciseId = update.ExerciseId,
                ExerciseName = exercise.Name,
                Success = false,
                Message = $"Failed to update: {ex.Message}"
            };
        }
    }
}
