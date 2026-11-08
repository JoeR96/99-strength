using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for habit definitions and their per-day logs.</summary>
public class HabitRepository : Repository<HabitDefinition, HabitId>, IHabitRepository
{
    public HabitRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<HabitDefinition>> ListForUserAsync(UserId userId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Context.Habits.Where(h => h.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(h => !h.Archived);
        }

        return await query
            .OrderBy(h => h.Order).ThenBy(h => h.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HabitLog>> GetLogsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await Context.HabitLogs
            .Where(l => l.UserId == userId && l.Date >= from && l.Date <= to)
            .OrderBy(l => l.Date)
            .ToListAsync(ct);
    }

    public async Task<HabitLog?> GetLogAsync(HabitId habitId, DateOnly date, CancellationToken ct = default)
    {
        return await Context.HabitLogs
            .FirstOrDefaultAsync(l => l.HabitId == habitId && l.Date == date, ct);
    }

    public async Task AddLogAsync(HabitLog log, CancellationToken ct = default)
    {
        await Context.HabitLogs.AddAsync(log, ct);
    }

    public void UpdateLog(HabitLog log)
    {
        Context.HabitLogs.Update(log);
    }
}
