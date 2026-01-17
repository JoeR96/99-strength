using A2S.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation for coordinating repository operations.
/// Manages database transactions and ensures consistency.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly A2SDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(
        A2SDbContext context,
        IWorkoutRepository workoutRepository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Workouts = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
    }

    public IWorkoutRepository Workouts { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            await _currentTransaction.CommitAsync(ct);
        }
        catch
        {
            await RollbackTransactionAsync(ct);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        try
        {
            await _currentTransaction.RollbackAsync(ct);
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
