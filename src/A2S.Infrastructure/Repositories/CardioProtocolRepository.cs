using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the CardioProtocol aggregate.</summary>
public class CardioProtocolRepository : Repository<CardioProtocol, CardioProtocolId>, ICardioProtocolRepository
{
    public CardioProtocolRepository(A2SDbContext context) : base(context)
    {
    }

    public override async Task<CardioProtocol?> GetByIdAsync(CardioProtocolId id, CancellationToken ct = default)
    {
        return await Context.CardioProtocols.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<CardioProtocol>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.CardioProtocols
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Kind)
            .ThenBy(p => p.WeekIndex)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
    }
}
