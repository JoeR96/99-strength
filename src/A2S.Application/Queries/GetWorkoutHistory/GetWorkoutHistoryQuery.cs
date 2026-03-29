using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetWorkoutHistory;

/// <summary>
/// Query to get workout history including all completed activities.
/// </summary>
public sealed record GetWorkoutHistoryQuery(Guid? WorkoutId = null) : IQuery<Result<WorkoutHistoryDto?>>;
