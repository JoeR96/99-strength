using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for a user's meals.</summary>
public class MealRepository : Repository<Meal, MealId>, IMealRepository
{
    public MealRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<Meal>> ListForUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await Context.Meals
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Type).ThenBy(m => m.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Meal>> GetManyAsync(UserId userId, IEnumerable<MealId> ids, CancellationToken ct = default)
    {
        var wanted = ids.Select(id => id.Value).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return Array.Empty<Meal>();
        }

        var meals = await Context.Meals
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        return meals.Where(m => wanted.Contains(m.Id.Value)).ToList();
    }
}
