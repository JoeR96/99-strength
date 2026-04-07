using System.Text.Json;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.SubstituteExercise;

/// <summary>
/// Handler for SubstituteExerciseCommand.
/// Permanently replaces an exercise with another while preserving or changing progression data.
/// Records audit entries for tracking substitutions.
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
            var userId = _currentUserService.GetUserId();
            if (userId == null)
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

            if (workout.UserId != userId.Value)
            {
                return Result.Failure<SubstituteExerciseResult>("You can only modify your own workouts.");
            }

            var exerciseId = new ExerciseId(request.ExerciseId);
            var exercise = workout.GetExerciseById(exerciseId);

            if (exercise == null)
            {
                return Result.Failure<SubstituteExerciseResult>("Exercise not found in this workout.");
            }

            var oldProgressionSnapshot = exercise.CaptureProgressionSnapshot();
            var oldProgressionJson = JsonSerializer.Serialize(new
            {
                Type = oldProgressionSnapshot.ProgressionType,
                State = oldProgressionSnapshot.ProgressionStateJson
            });

            var originalName = workout.SubstituteExercise(
                exerciseId,
                request.NewExerciseName,
                request.NewExternalTemplateId);

            var progressionTypeChanged = false;
            string? newProgressionType = null;
            string? newProgressionJson = null;

            if (request.NewProgressionConfig != null)
            {
                var config = request.NewProgressionConfig;

                TrainingMax? trainingMax = config.TrainingMaxValue.HasValue
                    ? TrainingMax.Create(config.TrainingMaxValue.Value, config.TrainingMaxUnit ?? WeightUnit.Kilograms)
                    : null;

                RepRange? repRange = config.RepRangeMinimum.HasValue && config.RepRangeMaximum.HasValue
                    ? RepRange.Create(config.RepRangeMinimum.Value, config.RepRangeMaximum.Value)
                    : null;

                Weight? startingWeight = config.StartingWeight.HasValue
                    ? Weight.Create(config.StartingWeight.Value, config.WeightUnit ?? WeightUnit.Kilograms)
                    : null;

                try
                {
                    var newProgression = ExerciseProgression.CreateFromConfig(new ProgressionConfig
                    {
                        ProgressionType = config.Type,
                        EquipmentType = exercise.Equipment,
                        TrainingMax = trainingMax,
                        RepRange = repRange,
                        StartingSets = config.StartingSets,
                        StartingWeight = startingWeight,
                        UseAmrap = config.UseAmrap,
                        BaseSetsPerExercise = config.BaseSetsPerExercise,
                        TargetSets = config.TargetSets,
                        IsUnilateral = config.IsUnilateral,
                        TargetTotalReps = config.TargetTotalReps,
                        MinimumSets = config.MinimumSets,
                        MaximumSets = config.MaximumSets,
                    });

                    workout.ReplaceExerciseProgression(exerciseId, newProgression);

                    progressionTypeChanged = true;
                    newProgressionType = config.Type;

                    var newProgressionSnapshot = exercise.CaptureProgressionSnapshot();
                    newProgressionJson = JsonSerializer.Serialize(new
                    {
                        Type = newProgressionSnapshot.ProgressionType,
                        State = newProgressionSnapshot.ProgressionStateJson
                    });
                }
                catch (ArgumentException ex)
                {
                    return Result.Failure<SubstituteExerciseResult>(ex.Message);
                }
            }

            var auditEntry = ProgressionAuditEntry.PermanentSubstitution(
                exerciseId,
                originalName,
                request.NewExerciseName,
                workout.CurrentWeek,
                workout.CurrentDay,
                oldProgressionJson,
                newProgressionJson,
                request.Reason);

            workout.RecordAuditEntry(auditEntry);

            _workoutRepository.Update(workout);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var message = progressionTypeChanged
                ? $"Successfully substituted '{originalName}' with '{request.NewExerciseName}' and changed progression to {newProgressionType}."
                : $"Successfully substituted '{originalName}' with '{request.NewExerciseName}'.";

            return Result.Success(new SubstituteExerciseResult
            {
                ExerciseId = request.ExerciseId,
                OriginalName = originalName,
                NewName = request.NewExerciseName,
                Success = true,
                ProgressionTypeChanged = progressionTypeChanged,
                NewProgressionType = newProgressionType,
                Message = message
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<SubstituteExerciseResult>($"Failed to substitute exercise: {ex.Message}");
        }
    }

}
