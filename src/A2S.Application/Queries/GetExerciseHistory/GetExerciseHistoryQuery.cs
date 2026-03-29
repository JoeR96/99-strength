using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetExerciseHistory;

/// <summary>
/// Query to get history for a specific exercise by name, aggregated across all workouts.
/// </summary>
public sealed record GetExerciseHistoryQuery(string ExerciseName) : IQuery<Result<AggregatedExerciseHistoryDto?>>;
