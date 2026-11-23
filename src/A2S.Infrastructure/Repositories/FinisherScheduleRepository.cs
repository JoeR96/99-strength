using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for live finisher nudge schedules.</summary>
public class FinisherScheduleRepository : Repository<FinisherSchedule, FinisherScheduleId>, IFinisherScheduleRepository
{
    public FinisherScheduleRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public Task<FinisherSchedule?> GetAsync(UserId userId, string clientSessionId, CancellationToken ct = default)
    {
        return Context.FinisherSchedules.FirstOrDefaultAsync(s => s.UserId == userId && s.ClientSessionId == clientSessionId, ct);
    }

    public async Task<IReadOnlyList<FinisherSchedule>> ListDueAsync(DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        return await Context.FinisherSchedules
            .Where(s => s.NextDueAtUtc != null && s.NextDueAtUtc <= nowUtc)
            .OrderBy(s => s.NextDueAtUtc)
            .ToListAsync(ct);
    }
}
