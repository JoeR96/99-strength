using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetWorkout;

/// <summary>
/// Query to get the currently active workout.
/// </summary>
public sealed record GetCurrentWorkoutQuery : IQuery<Result<WorkoutDto?>>;
