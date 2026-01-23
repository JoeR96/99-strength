using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetAllWorkouts;

/// <summary>
/// Query to get all workouts for the current user.
/// </summary>
public sealed record GetAllWorkoutsQuery : IQuery<Result<IReadOnlyList<WorkoutSummaryDto>>>;
