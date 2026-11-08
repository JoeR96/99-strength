using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the MobilityRoutine aggregate.</summary>
public class MobilityRoutineRepository : Repository<MobilityRoutine, MobilityRoutineId>, IMobilityRoutineRepository
{
    public MobilityRoutineRepository(A2SDbContext context) : base(context)
    {
    }

    public override async Task<MobilityRoutine?> GetByIdAsync(MobilityRoutineId id, CancellationToken ct = default)
    {
        return await Context.MobilityRoutines.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<MobilityRoutine>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.MobilityRoutines
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }
}
