using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the TrainingPlan aggregate. Every read loads the completions child table.</summary>
public class TrainingPlanRepository : Repository<TrainingPlan, TrainingPlanId>, ITrainingPlanRepository
{
    public TrainingPlanRepository(A2SDbContext context) : base(context)
    {
    }

    public override async Task<TrainingPlan?> GetByIdAsync(TrainingPlanId id, CancellationToken ct = default)
    {
        return await Context.TrainingPlans
            .Include(p => p.Completions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<TrainingPlan?> GetActiveForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.TrainingPlans
            .Include(p => p.Completions)
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TrainingPlan>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.TrainingPlans
            .AsNoTracking()
            .Include(p => p.Completions)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);
    }
}
