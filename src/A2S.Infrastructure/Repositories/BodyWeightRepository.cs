using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for morning weigh-ins.</summary>
public class BodyWeightRepository : Repository<BodyWeightEntry, BodyWeightEntryId>, IBodyWeightRepository
{
    public BodyWeightRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<BodyWeightEntry?> GetForDateAsync(UserId userId, DateOnly date, CancellationToken ct = default)
    {
        return await Context.BodyWeightEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Date == date, ct);
    }

    public async Task<IReadOnlyList<BodyWeightEntry>> ListAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await Context.BodyWeightEntries
            .Where(e => e.UserId == userId && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);
    }
}
