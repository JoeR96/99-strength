using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetWorkout;

/// <summary>
/// Handler for GetCurrentWorkoutQuery.
/// Retrieves the currently active workout.
/// </summary>
public sealed class GetCurrentWorkoutQueryHandler : IRequestHandler<GetCurrentWorkoutQuery, Result<WorkoutDto?>>
{
    private readonly IWorkoutRepository _workoutRepository;

    public GetCurrentWorkoutQueryHandler(IWorkoutRepository workoutRepository)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
    }

    public async Task<Result<WorkoutDto?>> Handle(GetCurrentWorkoutQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var workout = await _workoutRepository.GetActiveWorkoutAsync(cancellationToken);

            if (workout == null)
            {
                return Result.Success<WorkoutDto?>(null);
            }

            var exerciseDtos = workout.Exercises
                .OrderBy(e => e.AssignedDay)
                .ThenBy(e => e.OrderInDay)
                .Select(MapExerciseToDto)
                .ToList();

            var dto = new WorkoutDto
            {
                Id = workout.Id.Value,
                Name = workout.Name,
                Variant = workout.Variant.ToString(),
                TotalWeeks = workout.TotalWeeks,
                CurrentWeek = workout.CurrentWeek,
                CurrentBlock = workout.CurrentBlock,
                Status = workout.Status.ToString(),
                CreatedAt = workout.CreatedAt,
                StartedAt = workout.StartedAt,
                CompletedAt = workout.CompletedAt,
                ExerciseCount = workout.Exercises.Count,
                Exercises = exerciseDtos
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
                TrainingMaxValue = linear.TrainingMax.Value,
                TrainingMaxUnit = linear.TrainingMax.Unit.ToString(),
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
                WeightUnit = repsPerSet.CurrentWeight.Unit.ToString()
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
            Progression = progressionDto
        };
    }
}
