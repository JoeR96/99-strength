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

            // Capture old progression state for audit
            var oldProgressionSnapshot = exercise.CaptureProgressionSnapshot();
            var oldProgressionJson = JsonSerializer.Serialize(new
            {
                Type = oldProgressionSnapshot.ProgressionType,
                State = oldProgressionSnapshot.ProgressionStateJson
            });

            // Perform the substitution (name change)
            var originalName = workout.SubstituteExercise(
                exerciseId,
                request.NewExerciseName,
                request.NewExternalTemplateId);

            var progressionTypeChanged = false;
            string? newProgressionType = null;
            string? newProgressionJson = null;

            // Handle progression configuration change if provided
            if (request.NewProgressionConfig != null)
            {
                var newProgressionResult = CreateProgressionFromConfig(
                    request.NewProgressionConfig,
                    exercise.Equipment);

                if (!newProgressionResult.IsSuccess)
                {
                    return Result.Failure<SubstituteExerciseResult>(newProgressionResult.Error);
                }

                var newProgression = newProgressionResult.Value;
                exercise.ReplaceProgression(newProgression);

                progressionTypeChanged = true;
                newProgressionType = request.NewProgressionConfig.Type;

                // Capture new progression state for audit
                var newProgressionSnapshot = exercise.CaptureProgressionSnapshot();
                newProgressionJson = JsonSerializer.Serialize(new
                {
                    Type = newProgressionSnapshot.ProgressionType,
                    State = newProgressionSnapshot.ProgressionStateJson
                });
            }

            // Record audit entry for permanent substitution
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

    /// <summary>
    /// Creates a new ExerciseProgression based on the provided configuration DTO.
    /// </summary>
    private static Result<ExerciseProgression> CreateProgressionFromConfig(
        ProgressionConfigDto config,
        EquipmentType defaultEquipment)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "linear" => CreateLinearProgression(config),
            "repsset" or "repsperset" => CreateRepsPerSetProgression(config, defaultEquipment),
            "minimalsets" => CreateMinimalSetsProgression(config, defaultEquipment),
            _ => Result.Failure<ExerciseProgression>($"Unknown progression type: {config.Type}. Valid types are: Linear, RepsPerSet, MinimalSets")
        };
    }

    private static Result<ExerciseProgression> CreateLinearProgression(ProgressionConfigDto config)
    {
        if (!config.TrainingMaxValue.HasValue)
        {
            return Result.Failure<ExerciseProgression>("Linear progression requires TrainingMaxValue.");
        }

        var unit = config.TrainingMaxUnit ?? WeightUnit.Kilograms;
        var trainingMax = TrainingMax.Create(config.TrainingMaxValue.Value, unit);
        var useAmrap = config.UseAmrap ?? true;
        var baseSets = config.BaseSetsPerExercise ?? 4;

        var progression = LinearProgressionStrategy.Create(trainingMax, useAmrap, baseSets);
        return Result.Success<ExerciseProgression>(progression);
    }

    private static Result<ExerciseProgression> CreateRepsPerSetProgression(
        ProgressionConfigDto config,
        EquipmentType defaultEquipment)
    {
        // Validate required fields
        if (!config.RepRangeMinimum.HasValue || !config.RepRangeMaximum.HasValue)
        {
            return Result.Failure<ExerciseProgression>(
                "RepsPerSet progression requires RepRangeMinimum and RepRangeMaximum.");
        }

        if (!config.StartingWeight.HasValue)
        {
            return Result.Failure<ExerciseProgression>("RepsPerSet progression requires StartingWeight.");
        }

        var repRange = RepRange.Create(
            config.RepRangeMinimum.Value,
            config.RepRangeMaximum.Value);

        var weightUnit = config.WeightUnit ?? WeightUnit.Kilograms;
        var startingWeight = Weight.Create(config.StartingWeight.Value, weightUnit);
        var targetSets = config.TargetSets ?? 4;
        var isUnilateral = config.IsUnilateral ?? false;

        var startingSets = config.StartingSets ?? 2;

        var progression = RepsPerSetStrategy.Create(
            repRange,
            defaultEquipment,
            startingSets: startingSets,
            targetSets: targetSets,
            isUnilateral: isUnilateral,
            startingWeight: startingWeight);

        return Result.Success<ExerciseProgression>(progression);
    }

    private static Result<ExerciseProgression> CreateMinimalSetsProgression(
        ProgressionConfigDto config,
        EquipmentType defaultEquipment)
    {
        if (!config.StartingWeight.HasValue)
        {
            return Result.Failure<ExerciseProgression>("MinimalSets progression requires StartingWeight.");
        }

        if (!config.TargetTotalReps.HasValue)
        {
            return Result.Failure<ExerciseProgression>("MinimalSets progression requires TargetTotalReps.");
        }

        var weightUnit = config.WeightUnit ?? WeightUnit.Kilograms;
        var startingWeight = Weight.Create(config.StartingWeight.Value, weightUnit);
        var targetTotalReps = config.TargetTotalReps.Value;
        var minimumSets = config.MinimumSets ?? 2;
        var maximumSets = config.MaximumSets ?? 10;

        // Default starting sets to middle of range
        var startingSets = Math.Max(minimumSets, Math.Min(maximumSets, (minimumSets + maximumSets) / 2));

        var progression = MinimalSetsStrategy.Create(
            startingWeight,
            targetTotalReps,
            startingSets,
            defaultEquipment,
            minimumSets,
            maximumSets);

        return Result.Success<ExerciseProgression>(progression);
    }
}
