using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for local key to Hevy id links.</summary>
public class HevyLinkRepository : Repository<HevyLink, HevyLinkId>, IHevyLinkRepository
{
    public HevyLinkRepository(A2SDbContext context) : base(context)
    {
    }

    public Task<HevyLink?> FindAsync(UserId userId, HevyLinkKind kind, string localKey, CancellationToken ct = default)
    {
        var key = localKey.Trim();
        return Context.HevyLinks.FirstOrDefaultAsync(l => l.UserId == userId && l.Kind == kind && l.LocalKey == key, ct);
    }

    public Task<HevyLink?> FindByHevyIdAsync(UserId userId, HevyLinkKind kind, string hevyId, CancellationToken ct = default)
    {
        var id = hevyId.Trim();
        return Context.HevyLinks.FirstOrDefaultAsync(l => l.UserId == userId && l.Kind == kind && l.HevyId == id, ct);
    }

    public async Task<IReadOnlyList<HevyLink>> ListAsync(UserId userId, HevyLinkKind kind, CancellationToken ct = default)
    {
        return await Context.HevyLinks
            .Where(l => l.UserId == userId && l.Kind == kind)
            .OrderBy(l => l.LocalKey)
            .ToListAsync(ct);
    }
}
