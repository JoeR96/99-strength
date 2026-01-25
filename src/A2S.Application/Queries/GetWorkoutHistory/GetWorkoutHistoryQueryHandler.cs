using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Queries.GetWorkoutHistory;

/// <summary>
/// Handler for GetWorkoutHistoryQuery.
/// Retrieves comprehensive workout history including completed activities and performance data.
/// </summary>
public sealed class GetWorkoutHistoryQueryHandler : IRequestHandler<GetWorkoutHistoryQuery, Result<WorkoutHistoryDto?>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetWorkoutHistoryQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<WorkoutHistoryDto?>> Handle(GetWorkoutHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<WorkoutHistoryDto?>("User must be authenticated.");
            }

            Workout? workout;

            if (request.WorkoutId.HasValue)
            {
                workout = await _workoutRepository.GetByIdAsync(
                    new WorkoutId(request.WorkoutId.Value),
                    cancellationToken);

                if (workout != null && workout.UserId != userId)
                {
                    return Result.Failure<WorkoutHistoryDto?>("Workout not found.");
                }
            }
            else
            {
                workout = await _workoutRepository.GetActiveWorkoutAsync(userId, cancellationToken);
            }

            if (workout == null)
            {
                return Result.Success<WorkoutHistoryDto?>(null);
            }

            var dto = MapToHistoryDto(workout);
            return Result.Success<WorkoutHistoryDto?>(dto);
        }
        catch (Exception ex)
        {
            return Result.Failure<WorkoutHistoryDto?>($"Failed to retrieve workout history: {ex.Message}");
        }
    }

    private static WorkoutHistoryDto MapToHistoryDto(Workout workout)
    {
        var completedActivities = workout.CompletedActivities
            .OrderBy(a => a.WeekNumber)
            .ThenBy(a => a.Day)
            .Select(a => new WorkoutActivityDto
            {
                Day = a.Day.ToString(),
                DayNumber = (int)a.Day,
                WeekNumber = a.WeekNumber,
                BlockNumber = a.BlockNumber,
                CompletedAt = a.CompletedAt,
                IsDeloadWeek = a.WeekNumber % 7 == 0,
                Performances = a.Performances?.Select(p => new ExercisePerformanceDto
                {
                    ExerciseId = p.ExerciseId.Value,
                    CompletedAt = p.CompletedAt,
                    CompletedSets = p.CompletedSets?.Select(s => new CompletedSetDto
                    {
                        SetNumber = s.SetNumber,
                        Weight = s.Weight?.Value ?? 0,
                        WeightUnit = s.Weight?.Unit.ToString() ?? "Kilograms",
                        ActualReps = s.ActualReps,
                        WasAmrap = s.WasAmrap
                    }).ToList() ?? new List<CompletedSetDto>()
                }).ToList() ?? new List<ExercisePerformanceDto>()
            })
            .ToList();

        var exerciseHistories = BuildExerciseHistories(workout, completedActivities);

        return new WorkoutHistoryDto
        {
            WorkoutId = workout.Id.Value,
            WorkoutName = workout.Name,
            Variant = workout.Variant.ToString(),
            TotalWeeks = workout.TotalWeeks,
            CurrentWeek = workout.CurrentWeek,
            CurrentBlock = workout.CurrentBlock,
            DaysPerWeek = workout.GetDaysPerWeek(),
            StartedAt = workout.StartedAt,
            TotalWorkoutsCompleted = completedActivities.Count,
            CompletedActivities = completedActivities,
            ExerciseHistories = exerciseHistories
        };
    }

    private static List<ExerciseHistoryDto> BuildExerciseHistories(
        Workout workout,
        List<WorkoutActivityDto> completedActivities)
    {
        var histories = new List<ExerciseHistoryDto>();

        foreach (var exercise in workout.Exercises.OrderBy(e => e.AssignedDay).ThenBy(e => e.OrderInDay))
        {
            var weeklyHistory = BuildWeeklyHistory(exercise, completedActivities);

            decimal currentWeight = 0;
            string weightUnit = "Kilograms";
            int currentSets = 0;
            int targetSets = 0;
            decimal? trainingMax = null;

            if (exercise.Progression is LinearProgressionStrategy linear)
            {
                currentWeight = linear.TrainingMax.Value;
                weightUnit = linear.TrainingMax.Unit.ToString();
                currentSets = linear.BaseSetsPerExercise;
                targetSets = linear.BaseSetsPerExercise;
                trainingMax = linear.TrainingMax.Value;
            }
            else if (exercise.Progression is RepsPerSetStrategy repsPerSet)
            {
                currentWeight = repsPerSet.CurrentWeight.Value;
                weightUnit = repsPerSet.CurrentWeight.Unit.ToString();
                currentSets = repsPerSet.CurrentSetCount;
                targetSets = repsPerSet.TargetSets;
            }
            else if (exercise.Progression is MinimalSetsStrategy minimalSets)
            {
                currentWeight = minimalSets.CurrentWeight.Value;
                weightUnit = minimalSets.CurrentWeight.Unit.ToString();
                currentSets = minimalSets.CurrentSetCount;
                targetSets = minimalSets.MinimumSets;
            }

            histories.Add(new ExerciseHistoryDto
            {
                ExerciseId = exercise.Id.Value,
                Name = exercise.Name,
                ProgressionType = exercise.Progression.ProgressionType,
                AssignedDay = (int)exercise.AssignedDay,
                Category = exercise.Category.ToString(),
                Equipment = exercise.Equipment.ToString(),
                CurrentWeight = currentWeight,
                WeightUnit = weightUnit,
                CurrentSets = currentSets,
                TargetSets = targetSets,
                TrainingMax = trainingMax,
                WeeklyHistory = weeklyHistory
            });
        }

        return histories;
    }

    private static List<WeeklyPerformanceDto> BuildWeeklyHistory(
        Exercise exercise,
        List<WorkoutActivityDto> completedActivities)
    {
        var weeklyHistory = new List<WeeklyPerformanceDto>();

        // Group activities by week and find performances for this exercise
        var activitiesByWeek = completedActivities
            .Where(a => a.DayNumber == (int)exercise.AssignedDay)
            .GroupBy(a => a.WeekNumber);

        foreach (var weekGroup in activitiesByWeek.OrderBy(g => g.Key))
        {
            var weekActivity = weekGroup.First();
            var performance = weekActivity.Performances
                .FirstOrDefault(p => p.ExerciseId == exercise.Id.Value);

            if (performance == null || performance.CompletedSets.Count == 0)
            {
                // No performance data for this week, skip
                continue;
            }

            var sets = performance.CompletedSets;
            var totalVolume = sets.Sum(s => s.Weight * s.ActualReps);
            var totalReps = sets.Sum(s => s.ActualReps);
            var avgWeight = sets.Count > 0 ? sets.Average(s => s.Weight) : 0;
            var amrapSet = sets.FirstOrDefault(s => s.WasAmrap);

            weeklyHistory.Add(new WeeklyPerformanceDto
            {
                WeekNumber = weekActivity.WeekNumber,
                BlockNumber = weekActivity.BlockNumber,
                CompletedAt = weekActivity.CompletedAt,
                IsDeloadWeek = weekActivity.IsDeloadWeek,
                TotalVolume = totalVolume,
                AverageWeight = avgWeight,
                TotalReps = totalReps,
                SetsCompleted = sets.Count,
                AmrapReps = amrapSet?.ActualReps,
                Sets = sets.ToList()
            });
        }

        return weeklyHistory;
    }
}
