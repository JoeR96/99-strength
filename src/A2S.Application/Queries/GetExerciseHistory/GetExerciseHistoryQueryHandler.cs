using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetExerciseHistory;

/// <summary>
/// Handler for GetExerciseHistoryQuery.
/// Retrieves exercise history aggregated across all user workouts by exercise name.
/// </summary>
public sealed class GetExerciseHistoryQueryHandler : IRequestHandler<GetExerciseHistoryQuery, Result<AggregatedExerciseHistoryDto?>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetExerciseHistoryQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<AggregatedExerciseHistoryDto?>> Handle(GetExerciseHistoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<AggregatedExerciseHistoryDto?>("User must be authenticated.");
            }

            if (string.IsNullOrWhiteSpace(request.ExerciseName))
            {
                return Result.Failure<AggregatedExerciseHistoryDto?>("Exercise name is required.");
            }

            // Get all workouts for the user
            var workouts = await _workoutRepository.GetAllByUserAsync(userId, cancellationToken);

            if (workouts == null || !workouts.Any())
            {
                return Result.Success<AggregatedExerciseHistoryDto?>(null);
            }

            // Find all sessions for this exercise name across all workouts
            var sessions = new List<ExerciseSessionDto>();
            var exerciseName = request.ExerciseName.Trim();

            foreach (var workout in workouts)
            {
                // Find exercises matching the name
                var matchingExercises = workout.Exercises
                    .Where(e => e.Name.Equals(exerciseName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var exercise in matchingExercises)
                {
                    // Find completed activities that include this exercise
                    foreach (var activity in workout.CompletedActivities)
                    {
                        var performance = activity.Performances?.FirstOrDefault(p => p.ExerciseId == exercise.Id);
                        if (performance != null && performance.CompletedSets.Count > 0)
                        {
                            var sets = performance.CompletedSets
                                .Select(s => new CompletedSetDto
                                {
                                    SetNumber = s.SetNumber,
                                    Weight = s.Weight?.Value ?? 0,
                                    WeightUnit = s.Weight?.Unit.ToString() ?? "Kilograms",
                                    ActualReps = s.ActualReps,
                                    WasAmrap = s.WasAmrap
                                })
                                .ToList();

                            var sessionVolume = sets.Sum(s => s.Weight * s.ActualReps);

                            sessions.Add(new ExerciseSessionDto
                            {
                                WorkoutId = workout.Id.Value,
                                WorkoutName = workout.Name,
                                WeekNumber = activity.WeekNumber,
                                BlockNumber = activity.BlockNumber,
                                CompletedAt = performance.CompletedAt,
                                ProgressionType = exercise.Progression?.ProgressionType ?? "Unknown",
                                SessionVolume = sessionVolume,
                                Sets = sets
                            });
                        }
                    }
                }
            }

            if (sessions.Count == 0)
            {
                return Result.Success<AggregatedExerciseHistoryDto?>(null);
            }

            // Order sessions by date
            sessions = sessions.OrderBy(s => s.CompletedAt).ToList();

            // Calculate aggregates
            var allSets = sessions.SelectMany(s => s.Sets).ToList();
            var totalVolume = sessions.Sum(s => s.SessionVolume);
            var totalSets = allSets.Count;
            var totalReps = allSets.Sum(s => s.ActualReps);
            var prWeight = allSets.Max(s => s.Weight);
            var prVolume = allSets.Max(s => s.Weight * s.ActualReps);
            var weightUnit = allSets.FirstOrDefault()?.WeightUnit ?? "Kilograms";

            return Result.Success<AggregatedExerciseHistoryDto?>(new AggregatedExerciseHistoryDto
            {
                ExerciseName = exerciseName,
                TotalSessions = sessions.Count,
                TotalVolume = totalVolume,
                TotalSets = totalSets,
                TotalReps = totalReps,
                PersonalRecordWeight = prWeight,
                PersonalRecordVolume = prVolume,
                WeightUnit = weightUnit,
                FirstPerformed = sessions.First().CompletedAt,
                LastPerformed = sessions.Last().CompletedAt,
                Sessions = sessions
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<AggregatedExerciseHistoryDto?>($"Failed to retrieve exercise history: {ex.Message}");
        }
    }
}
