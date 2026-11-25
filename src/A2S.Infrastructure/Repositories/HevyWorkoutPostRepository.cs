using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for gym-mode sessions queued for Hevy.</summary>
public class HevyWorkoutPostRepository : Repository<HevyWorkoutPost, HevyWorkoutPostId>, IHevyWorkoutPostRepository
{
    public HevyWorkoutPostRepository(A2SDbContext context) : base(context)
    {
    }

    public Task<HevyWorkoutPost?> FindByClientSessionAsync(UserId userId, Guid clientSessionId, CancellationToken ct = default) =>
        Context.HevyWorkoutPosts.FirstOrDefaultAsync(p => p.UserId == userId && p.ClientSessionId == clientSessionId, ct);

    public async Task<IReadOnlyList<HevyWorkoutPost>> ListDueAsync(DateTimeOffset now, int take, CancellationToken ct = default)
    {
        var utc = now.ToUniversalTime();
        return await Context.HevyWorkoutPosts
            .Where(p => (p.Status == HevyPostStatus.Pending || p.Status == HevyPostStatus.UpdatePending) && p.NextAttemptAtUtc <= utc)
            .OrderBy(p => p.NextAttemptAtUtc)
            .Take(Math.Max(take, 1))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HevyWorkoutPost>> ListForUserAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await Context.HevyWorkoutPosts
            .Where(p => p.UserId == userId && p.LocalDate >= from && p.LocalDate <= to)
            .OrderBy(p => p.StartedAtUtc)
            .ToListAsync(ct);
    }
}
