using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for push subscriptions and the daily nudge log.</summary>
public class PushSubscriptionRepository : Repository<PushSubscription, PushSubscriptionId>, IPushSubscriptionRepository
{
    public PushSubscriptionRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<PushSubscription>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.PushSubscriptions
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<PushSubscription?> GetByEndpointAsync(UserId userId, string endpoint, CancellationToken ct = default)
    {
        return await Context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
    }

    public async Task<IReadOnlyList<UserId>> ListSubscribedUsersAsync(CancellationToken ct = default)
    {
        return await Context.PushSubscriptions
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<bool> HasNudgeAsync(UserId userId, DateOnly localDate, PushNudgeKind kind, CancellationToken ct = default)
    {
        return Context.PushNudgeLogs.AnyAsync(l => l.UserId == userId && l.LocalDate == localDate && l.Kind == kind, ct);
    }

    public async Task AddNudgeAsync(PushNudgeLog log, CancellationToken ct = default)
    {
        await Context.PushNudgeLogs.AddAsync(log, ct);
    }
}
