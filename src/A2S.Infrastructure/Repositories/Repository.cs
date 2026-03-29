using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>
/// Generic repository base class providing standard CRUD operations.
/// </summary>
public abstract class Repository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected readonly A2SDbContext Context;

    protected Repository(A2SDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public virtual async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        return await Context.Set<TAggregate>().FindAsync([id], ct);
    }

    public async Task AddAsync(TAggregate entity, CancellationToken ct = default)
    {
        await Context.Set<TAggregate>().AddAsync(entity, ct);
    }

    public void Update(TAggregate entity)
    {
        Context.Set<TAggregate>().Update(entity);
    }

    public void Remove(TAggregate entity)
    {
        Context.Set<TAggregate>().Remove(entity);
    }
}
