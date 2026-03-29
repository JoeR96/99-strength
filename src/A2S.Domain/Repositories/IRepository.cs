namespace A2S.Domain.Repositories;

/// <summary>
/// Generic repository interface for aggregate roots.
/// Provides standard CRUD operations.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type</typeparam>
/// <typeparam name="TId">The aggregate's identifier type</typeparam>
public interface IRepository<TAggregate, in TId>
    where TAggregate : Common.AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TAggregate entity, CancellationToken ct = default);
    void Update(TAggregate entity);
    void Remove(TAggregate entity);
}
