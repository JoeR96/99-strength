using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
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
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<WorkoutHistoryDto?>("User must be authenticated.");
            }

            Workout? workout;

            if (request.WorkoutId.HasValue)
            {
                workout = await _workoutRepository.GetByIdAsync(
                    new WorkoutId(request.WorkoutId.Value),
                    cancellationToken);

                if (workout != null && workout.UserId != userId.Value)
                {
                    return Result.Failure<WorkoutHistoryDto?>("Workout not found.");
                }
            }
            else
            {
                workout = await _workoutRepository.GetActiveWorkoutAsync(userId.Value, cancellationToken);
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

        var exerciseHistories = BuildExerciseHistories(workout);

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

    private static List<ExerciseHistoryDto> BuildExerciseHistories(Workout workout)
    {
        var histories = new List<ExerciseHistoryDto>();
        var domainActivities = workout.CompletedActivities
            .OrderBy(a => a.WeekNumber)
            .ThenBy(a => a.Day)
            .ToList();

        foreach (var exercise in workout.Exercises.OrderBy(e => e.AssignedDay).ThenBy(e => e.OrderInDay))
        {
            var weeklyHistory = BuildWeeklyHistory(exercise, domainActivities);
            var progressionChanges = BuildProgressionChanges(exercise, workout.AuditEntries);

            var progression = exercise.Progression;
            var tm = progression.GetTrainingMax();
            var currentWeight = progression.GetCurrentWeight();
            var data = progression.GetProgressionData();

            histories.Add(new ExerciseHistoryDto
            {
                ExerciseId = exercise.Id.Value,
                Name = exercise.Name,
                ProgressionType = progression.ProgressionType,
                AssignedDay = (int)exercise.AssignedDay,
                Category = exercise.Category.ToString(),
                Equipment = exercise.Equipment.ToString(),
                CurrentWeight = tm?.Value ?? currentWeight?.Value ?? 0,
                WeightUnit = tm?.Unit.ToString() ?? currentWeight?.Unit.ToString() ?? "Kilograms",
                CurrentSets = data.CurrentSetCount ?? data.BaseSetsPerExercise ?? 0,
                TargetSets = data.TargetSets ?? data.BaseSetsPerExercise ?? data.MinimumSets ?? 0,
                TrainingMax = tm?.Value,
                WeeklyHistory = weeklyHistory,
                ProgressionChanges = progressionChanges
            });
        }

        return histories;
    }

    private static List<ProgressionChangeDto> BuildProgressionChanges(
        Exercise exercise,
        IReadOnlyCollection<ProgressionAuditEntry> auditEntries)
    {
        return auditEntries
            .Where(a => a.ExerciseId == exercise.Id
                     && a.Type == AuditEntryType.PermanentSubstitution
                     && a.OldValue != null && a.NewValue != null)
            .Select(a =>
            {
                string? oldType = null, newType = null;
                try
                {
                    using var oldDoc = JsonDocument.Parse(a.OldValue!);
                    using var newDoc = JsonDocument.Parse(a.NewValue!);
                    if (oldDoc.RootElement.TryGetProperty("Type", out var oldTypeEl))
                        oldType = oldTypeEl.GetString();
                    if (newDoc.RootElement.TryGetProperty("Type", out var newTypeEl))
                        newType = newTypeEl.GetString();
                }
                catch (JsonException) { }

                return new ProgressionChangeDto
                {
                    OccurredAt = a.OccurredAt,
                    WeekNumber = a.WeekNumber,
                    OldProgressionType = oldType,
                    NewProgressionType = newType,
                    Reason = a.Reason
                };
            })
            .Where(c => c.OldProgressionType != c.NewProgressionType) // Only actual type changes
            .OrderBy(c => c.WeekNumber)
            .ToList();
    }

    private static List<WeeklyPerformanceDto> BuildWeeklyHistory(
        Exercise exercise,
        List<WorkoutActivity> domainActivities)
    {
        var weeklyHistory = new List<WeeklyPerformanceDto>();

        // Filter to activities for this exercise's assigned day, grouped by week
        var activitiesByWeek = domainActivities
            .Where(a => (int)a.Day == (int)exercise.AssignedDay)
            .GroupBy(a => a.WeekNumber);

        foreach (var weekGroup in activitiesByWeek.OrderBy(g => g.Key))
        {
            var activity = weekGroup.First();
            var performance = activity.Performances
                ?.FirstOrDefault(p => p.ExerciseId == exercise.Id);

            if (performance == null || performance.CompletedSets == null || performance.CompletedSets.Count == 0)
                continue;

            var sets = performance.CompletedSets.Select(s => new CompletedSetDto
            {
                SetNumber = s.SetNumber,
                Weight = s.Weight?.Value ?? 0,
                WeightUnit = s.Weight?.Unit.ToString() ?? "Kilograms",
                ActualReps = s.ActualReps,
                WasAmrap = s.WasAmrap
            }).ToList();

            var totalVolume = sets.Sum(s => s.Weight * s.ActualReps);
            var totalReps = sets.Sum(s => s.ActualReps);
            var avgWeight = sets.Count > 0 ? sets.Average(s => s.Weight) : 0;
            var amrapSet = sets.FirstOrDefault(s => s.WasAmrap);

            // Extract progression snapshot data for this exercise
            decimal? tmAtWeek = null;
            string? tmUnitAtWeek = null;
            decimal? weightAtWeek = null;
            int? setCountAtWeek = null;

            var snapshot = activity.ProgressionSnapshots
                ?.FirstOrDefault(s => s.ExerciseId == exercise.Id);

            if (snapshot != null && !string.IsNullOrEmpty(snapshot.ProgressionStateJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(snapshot.ProgressionStateJson);
                    var root = doc.RootElement;

                    if (snapshot.ProgressionType == "Linear")
                    {
                        if (root.TryGetProperty("TrainingMaxValue", out var tmVal))
                            tmAtWeek = tmVal.GetDecimal();
                        if (root.TryGetProperty("TrainingMaxUnit", out var tmUnit))
                            tmUnitAtWeek = tmUnit.GetInt32() == 1 ? "Kilograms" : "Pounds";
                    }
                    else if (snapshot.ProgressionType is "RepsPerSet" or "MinimalSets")
                    {
                        if (root.TryGetProperty("CurrentWeight", out var wVal))
                            weightAtWeek = wVal.GetDecimal();
                        if (root.TryGetProperty("CurrentSetCount", out var scVal))
                            setCountAtWeek = scVal.GetInt32();
                    }
                }
                catch (JsonException)
                {
                    // Malformed snapshot JSON — skip snapshot data for this week
                }
            }

            weeklyHistory.Add(new WeeklyPerformanceDto
            {
                WeekNumber = activity.WeekNumber,
                BlockNumber = activity.BlockNumber,
                CompletedAt = activity.CompletedAt,
                IsDeloadWeek = activity.IsDeloadWeek(),
                TotalVolume = totalVolume,
                AverageWeight = avgWeight,
                TotalReps = totalReps,
                SetsCompleted = sets.Count,
                AmrapReps = amrapSet?.ActualReps,
                Sets = sets,
                TrainingMaxAtWeek = tmAtWeek,
                TrainingMaxUnitAtWeek = tmUnitAtWeek,
                WeightAtWeek = weightAtWeek,
                SetCountAtWeek = setCountAtWeek,
                ProgressionTypeAtWeek = snapshot?.ProgressionType
            });
        }

        return weeklyHistory;
    }
}
