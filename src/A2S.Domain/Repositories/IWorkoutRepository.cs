using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>
/// Repository interface for Workout aggregate root.
/// Defines persistence operations following DDD repository pattern.
/// </summary>
public interface IWorkoutRepository
{
    /// <summary>
    /// Gets a workout by its unique identifier.
    /// </summary>
    /// <param name="id">The workout identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The workout if found, null otherwise</returns>
    Task<Workout?> GetByIdAsync(WorkoutId id, CancellationToken ct = default);

    /// <summary>
    /// Gets the currently active workout (status = Active).
    /// Users typically have one active workout at a time.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The active workout if found, null otherwise</returns>
    Task<Workout?> GetActiveWorkoutAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all workouts ordered by most recent first.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all workouts</returns>
    Task<IReadOnlyList<Workout>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets workouts by status.
    /// </summary>
    /// <param name="status">The workout status to filter by</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of workouts with the specified status</returns>
    Task<IReadOnlyList<Workout>> GetByStatusAsync(Enums.WorkoutStatus status, CancellationToken ct = default);

    /// <summary>
    /// Adds a new workout to the repository.
    /// </summary>
    /// <param name="workout">The workout to add</param>
    /// <param name="ct">Cancellation token</param>
    Task AddAsync(Workout workout, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing workout.
    /// In EF Core, this is typically a no-op as change tracking handles updates.
    /// </summary>
    /// <param name="workout">The workout to update</param>
    void Update(Workout workout);

    /// <summary>
    /// Removes a workout from the repository.
    /// </summary>
    /// <param name="workout">The workout to remove</param>
    void Remove(Workout workout);
}
