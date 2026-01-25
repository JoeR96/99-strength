using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetWorkout;

/// <summary>
/// Handler for GetCurrentWorkoutQuery.
/// Retrieves the currently active workout for the current user.
/// </summary>
public sealed class GetCurrentWorkoutQueryHandler : IRequestHandler<GetCurrentWorkoutQuery, Result<WorkoutDto?>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentWorkoutQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<WorkoutDto?>> Handle(GetCurrentWorkoutQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<WorkoutDto?>("User must be authenticated to retrieve workout.");
            }

            var workout = await _workoutRepository.GetActiveWorkoutAsync(userId, cancellationToken);

            if (workout == null)
            {
                return Result.Success<WorkoutDto?>(null);
            }

            var exerciseDtos = workout.Exercises
                .OrderBy(e => e.AssignedDay)
                .ThenBy(e => e.OrderInDay)
                .Select(MapExerciseToDto)
                .ToList();

            var completedDays = workout.GetCompletedDaysInCurrentWeek()
                .Select(d => (int)d)
                .ToList();

            var dto = new WorkoutDto
            {
                Id = workout.Id.Value,
                Name = workout.Name,
                Variant = workout.Variant.ToString(),
                TotalWeeks = workout.TotalWeeks,
                CurrentWeek = workout.CurrentWeek,
                CurrentBlock = workout.CurrentBlock,
                CurrentDay = workout.CurrentDay,
                DaysPerWeek = workout.GetDaysPerWeek(),
                CompletedDaysInCurrentWeek = completedDays,
                IsWeekComplete = workout.AreAllDaysCompletedInCurrentWeek(),
                Status = workout.Status.ToString(),
                CreatedAt = workout.CreatedAt,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                ExerciseCount = workout.Exercises.Count,
                Exercises = exerciseDtos,
                HevyRoutineFolderId = workout.HevyRoutineFolderId,
                HevySyncedRoutines = workout.HevySyncedRoutines
            };

            return Result.Success<WorkoutDto?>(dto);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkoutDto?>($"Failed to retrieve current workout: {ex.Message}");
        }
    }

    private static ExerciseDto MapExerciseToDto(Exercise exercise)
    {
        ExerciseProgressionDto progressionDto;

        if (exercise.Progression is LinearProgressionStrategy linear)
        {
            progressionDto = new LinearProgressionDto
            {
                Type = "Linear",
                TrainingMax = new TrainingMaxDto
                {
                    Value = linear.TrainingMax.Value,
                    Unit = (int)linear.TrainingMax.Unit // 0 = Kilograms, 1 = Pounds
                },
                UseAmrap = linear.UseAmrap,
                BaseSetsPerExercise = linear.BaseSetsPerExercise
            };
        }
        else if (exercise.Progression is RepsPerSetStrategy repsPerSet)
        {
            progressionDto = new RepsPerSetProgressionDto
            {
                Type = "RepsPerSet",
                RepRange = new RepRangeDto
                {
                    Minimum = repsPerSet.RepRange.Minimum,
                    Target = repsPerSet.RepRange.Target,
                    Maximum = repsPerSet.RepRange.Maximum
                },
                CurrentSetCount = repsPerSet.CurrentSetCount,
                TargetSets = repsPerSet.TargetSets,
                CurrentWeight = repsPerSet.CurrentWeight.Value,
                WeightUnit = repsPerSet.CurrentWeight.Unit.ToString(),
                IsUnilateral = repsPerSet.IsUnilateral
            };
        }
        else if (exercise.Progression is MinimalSetsStrategy minimalSets)
        {
            progressionDto = new MinimalSetsProgressionDto
            {
                Type = "MinimalSets",
                CurrentWeight = minimalSets.CurrentWeight.Value,
                WeightUnit = minimalSets.CurrentWeight.Unit.ToString(),
                TargetTotalReps = minimalSets.TargetTotalReps,
                CurrentSetCount = minimalSets.CurrentSetCount,
                MinimumSets = minimalSets.MinimumSets,
                MaximumSets = minimalSets.MaximumSets
            };
        }
        else
        {
            progressionDto = new ExerciseProgressionDto
            {
                Type = exercise.Progression.ProgressionType
            };
        }

        return new ExerciseDto
        {
            Id = exercise.Id.Value,
            Name = exercise.Name,
            Category = exercise.Category,
            Equipment = exercise.Equipment,
            AssignedDay = exercise.AssignedDay,
            OrderInDay = exercise.OrderInDay,
            HevyExerciseTemplateId = exercise.HevyExerciseTemplateId,
            Progression = progressionDto
        };
    }
}
