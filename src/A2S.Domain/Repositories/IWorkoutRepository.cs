using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>
/// Repository interface for Workout aggregate root.
/// Extends generic repository with workout-specific query methods.
/// </summary>
public interface IWorkoutRepository : IRepository<Workout, WorkoutId>
{

    /// <summary>
    /// Gets the currently active workout for a specific user (status = Active).
    /// Users typically have one active workout at a time.
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The active workout if found, null otherwise</returns>
    Task<Workout?> GetActiveWorkoutAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all workouts ordered by most recent first.
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all workouts for the user</returns>
    Task<IReadOnlyList<Workout>> GetAllAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Gets workouts by status for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="status">The workout status to filter by</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of workouts with the specified status for the user</returns>
    Task<IReadOnlyList<Workout>> GetByStatusAsync(UserId userId, Enums.WorkoutStatus status, CancellationToken ct = default);

    /// <summary>
    /// Gets all workouts for a user without loading the full exercise graph (summary only).
    /// </summary>
    Task<IReadOnlyList<Workout>> GetAllByUserSummaryAsync(UserId userId, CancellationToken ct = default);
}
