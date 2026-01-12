using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetWorkout;

/// <summary>
/// Query to retrieve a workout by ID.
/// </summary>
public sealed record GetWorkoutQuery(Guid WorkoutId) : IQuery<Result<WorkoutDto>>;
