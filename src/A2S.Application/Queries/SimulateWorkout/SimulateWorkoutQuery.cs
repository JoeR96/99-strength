using A2S.Application.Common;
using A2S.Domain.Services;

namespace A2S.Application.Queries.SimulateWorkout;

/// <summary>
/// Query to simulate workout progression over a specified number of sessions.
/// </summary>
public sealed record SimulateWorkoutQuery(
    Guid WorkoutId,
    int SessionCount) : IQuery<Result<SimulationResult>>;
