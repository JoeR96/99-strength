using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the CardioTrack aggregate.</summary>
public class CardioTrackRepository : Repository<CardioTrack, CardioTrackId>, ICardioTrackRepository
{
    public CardioTrackRepository(A2SDbContext context) : base(context)
    {
    }

    public override async Task<CardioTrack?> GetByIdAsync(CardioTrackId id, CancellationToken ct = default)
    {
        return await Context.CardioTracks.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<CardioTrack>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.CardioTracks
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }
}
