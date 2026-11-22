using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for daily readiness.</summary>
public class ReadinessRepository : Repository<ReadinessEntry, ReadinessEntryId>, IReadinessRepository
{
    public ReadinessRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<ReadinessEntry?> GetForDateAsync(UserId userId, DateOnly date, CancellationToken ct = default)
    {
        return await Context.ReadinessEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Date == date, ct);
    }
}
