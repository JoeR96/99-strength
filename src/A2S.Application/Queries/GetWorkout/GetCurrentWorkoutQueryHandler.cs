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
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<WorkoutDto?>("User must be authenticated to retrieve workout.");
            }

            var workout = await _workoutRepository.GetActiveWorkoutAsync(userId.Value, cancellationToken);

            if (workout == null)
            {
                return Result.Success<WorkoutDto?>(null);
            }

            var exerciseDtos = workout.Exercises
                .OrderBy(e => e.AssignedDay)
                .ThenBy(e => e.OrderInDay)
                .Select(e => MapExerciseToDto(e, workout))
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
                BlockSequence = workout.BlockSequence.ToList()
            };

            return Result.Success<WorkoutDto?>(dto);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkoutDto?>($"Failed to retrieve current workout: {ex.Message}");
        }
    }

    private static LastPerformanceDto? MapLastPerformance(Exercise exercise, Workout workout)
    {
        var lastActivity = workout.CompletedActivities
            .Where(a => a.Performances.Any(p => p.ExerciseId == exercise.Id && p.CompletedSets.Count > 0))
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefault();

        if (lastActivity == null)
        {
            return null;
        }

        var performance = lastActivity.Performances.First(p => p.ExerciseId == exercise.Id);

        return new LastPerformanceDto
        {
            WeekNumber = lastActivity.WeekNumber,
            CompletedAt = lastActivity.CompletedAt,
            Sets = performance.CompletedSets
                .Select(s => new LastPerformanceSetDto
                {
                    SetNumber = s.SetNumber,
                    Weight = s.Weight?.Value ?? 0,
                    WeightUnit = s.Weight?.Unit.ToString() ?? "Kilograms",
                    Reps = s.ActualReps,
                    WasAmrap = s.WasAmrap
                })
                .ToList()
        };
    }

    private static ExerciseDto MapExerciseToDto(Exercise exercise, Workout workout)
    {
        var progression = exercise.Progression;
        var data = progression.GetProgressionData();
        var trainingMax = progression.GetTrainingMax();

        ExerciseProgressionDto progressionDto = progression.ProgressionType switch
        {
            "Linear" => new LinearProgressionDto
            {
                Type = "Linear",
                TrainingMax = new TrainingMaxDto
                {
                    Value = trainingMax!.Value,
                    Unit = (int)trainingMax.Unit
                },
                UseAmrap = data.UseAmrap ?? true,
                BaseSetsPerExercise = data.BaseSetsPerExercise ?? 4
            },
            "RepsPerSet" => new RepsPerSetProgressionDto
            {
                Type = "RepsPerSet",
                RepRange = new RepRangeDto
                {
                    Minimum = data.RepRangeMinimum ?? 0,
                    Maximum = data.RepRangeMaximum ?? 0
                },
                StartingSets = data.StartingSets ?? 2,
                CurrentSetCount = data.CurrentSetCount ?? 2,
                TargetSets = data.TargetSets ?? 4,
                CurrentWeight = progression.GetCurrentWeight()?.Value ?? 0,
                WeightUnit = progression.GetCurrentWeight()?.Unit.ToString() ?? "Kilograms",
                IsUnilateral = progression.IsUnilateral,
                IsWeightPending = progression.IsWeightPending,
                PendingWeightConfirmation = progression.PendingWeightConfirmation,
                SuggestedWeight = progression.SuggestedWeight?.Value
            },
            "MinimalSets" => new MinimalSetsProgressionDto
            {
                Type = "MinimalSets",
                CurrentWeight = progression.GetCurrentWeight()!.Value,
                WeightUnit = progression.GetCurrentWeight()!.Unit.ToString(),
                TargetTotalReps = data.TargetTotalReps ?? 0,
                CurrentSetCount = data.CurrentSetCount ?? 0,
                MinimumSets = data.MinimumSets ?? 2,
                MaximumSets = data.MaximumSets ?? 10
            },
            _ => new ExerciseProgressionDto { Type = progression.ProgressionType }
        };

        return new ExerciseDto
        {
            Id = exercise.Id.Value,
            Name = exercise.Name,
            Category = exercise.Category,
            Equipment = exercise.Equipment,
            AssignedDay = exercise.AssignedDay,
            OrderInDay = exercise.OrderInDay,
            ExternalTemplateId = exercise.ExternalTemplateId,
            Progression = progressionDto,
            LastPerformance = MapLastPerformance(exercise, workout)
        };
    }
}
