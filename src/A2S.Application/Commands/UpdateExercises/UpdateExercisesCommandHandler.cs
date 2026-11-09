using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
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
    private readonly DayBudgetCalculator _budget;

    public UpdateExercisesCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        DayBudgetCalculator budget)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    public async Task<Result<UpdateExercisesResult>> Handle(UpdateExercisesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
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

            if (workout.UserId != userId.Value)
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

            if (successCount > 0)
            {
                _workoutRepository.Update(workout);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Warn, never block: the update has already been saved.
            var affectedDays = request.Updates
                .Select(u => workout.GetExerciseById(new ExerciseId(u.ExerciseId))?.AssignedDay)
                .Where(day => day.HasValue)
                .Select(day => day!.Value);
            var (warnings, overTimeBudget) = await _budget.AfterChangeAsync(workout, affectedDays, cancellationToken);

            return Result.Success(new UpdateExercisesResult
            {
                WorkoutId = request.WorkoutId,
                UpdatedCount = successCount,
                Results = results,
                Warnings = warnings,
                OverTimeBudget = overTimeBudget
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
            var messages = new List<string>();
            string? previousValue = null;
            string? newValue = null;
            var updated = false;

            if (update.TrainingMaxValue.HasValue)
            {
                var existingTm = exercise.GetTrainingMax();
                if (existingTm == null)
                {
                    return new ExerciseUpdateResult
                    {
                        ExerciseId = update.ExerciseId,
                        ExerciseName = exercise.Name,
                        Success = false,
                        Message = "This exercise does not support Training Max updates."
                    };
                }

                previousValue = $"{existingTm.Value} {existingTm.Unit}";
                var newTm = TrainingMax.Create(
                    update.TrainingMaxValue.Value,
                    update.TrainingMaxUnit ?? existingTm.Unit);
                workout.AdjustTrainingMax(exerciseId, newTm, update.Reason);
                newValue = $"{newTm.Value} {newTm.Unit}";
                messages.Add("Training Max updated");
                updated = true;
            }

            if (update.WeightValue.HasValue)
            {
                var currentWeight = exercise.GetCurrentWeight();
                previousValue = currentWeight != null
                    ? $"{currentWeight.Value} {currentWeight.Unit}"
                    : "Pending";

                var newWeight = Weight.Create(
                    update.WeightValue.Value,
                    update.WeightUnit ?? currentWeight?.Unit ?? WeightUnit.Kilograms);

                workout.AdjustWeight(exerciseId, newWeight);
                newValue = $"{newWeight.Value} {newWeight.Unit}";
                messages.Add("Weight updated");
                updated = true;
            }

            if (updated)
            {
                return new ExerciseUpdateResult
                {
                    ExerciseId = update.ExerciseId,
                    ExerciseName = exercise.Name,
                    Success = true,
                    Message = string.Join(", ", messages),
                    PreviousValue = previousValue,
                    NewValue = newValue
                };
            }

            return new ExerciseUpdateResult
            {
                ExerciseId = update.ExerciseId,
                ExerciseName = exercise.Name,
                Success = false,
                Message = "No applicable update values provided for this exercise's progression type."
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
