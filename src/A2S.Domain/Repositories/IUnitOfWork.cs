namespace A2S.Domain.Repositories;

/// <summary>
/// Unit of Work interface for coordinating repository operations.
/// Ensures transactional consistency across multiple aggregate operations.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets the workout repository.
    /// </summary>
    IWorkoutRepository Workouts { get; }

    /// <summary>
    /// Commits all changes to the database within a transaction.
    /// Domain events are dispatched after successful commit.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The number of state entries written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a new database transaction.
    /// Use when you need explicit transaction control beyond SaveChangesAsync.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
