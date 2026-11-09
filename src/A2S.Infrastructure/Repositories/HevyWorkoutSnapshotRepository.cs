using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for pulled Hevy workouts.</summary>
public class HevyWorkoutSnapshotRepository : Repository<HevyWorkoutSnapshot, HevyWorkoutId>, IHevyWorkoutSnapshotRepository
{
    public HevyWorkoutSnapshotRepository(A2SDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAsync(UserId userId, SnapshotStatus status, CancellationToken ct = default)
    {
        return await Context.HevyWorkoutSnapshots
            .Where(s => s.UserId == userId && s.Status == status)
            .OrderByDescending(s => s.StartUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HevyWorkoutSnapshot>> ListAwaitingMatchAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.HevyWorkoutSnapshots
            .Where(s => s.UserId == userId && (s.Status == SnapshotStatus.Pending || s.Status == SnapshotStatus.Unmatched))
            .OrderBy(s => s.StartUtc)
            .ToListAsync(ct);
    }
}
