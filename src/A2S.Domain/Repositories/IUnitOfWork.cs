namespace A2S.Domain.Repositories;

/// <summary>
/// Unit of Work interface for coordinating repository operations.
/// Ensures transactional consistency across multiple aggregate operations.
/// Repositories are injected directly via DI — UoW handles only persistence coordination.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Commits all changes to the database.
    /// Domain events are dispatched after successful commit.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
