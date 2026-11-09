using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the per-user Hevy pull cursor.</summary>
public class HevyPullCursorRepository : Repository<HevyPullCursor, UserId>, IHevyPullCursorRepository
{
    public HevyPullCursorRepository(A2SDbContext context) : base(context)
    {
    }
}
