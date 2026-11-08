using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>Repository for the one-per-week shopping lists.</summary>
public class ShoppingListRepository : Repository<ShoppingList, ShoppingListId>, IShoppingListRepository
{
    public ShoppingListRepository(A2SDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<ShoppingList?> GetForWeekAsync(UserId userId, DateOnly weekStart, CancellationToken ct = default)
    {
        return await Context.ShoppingLists
            .FirstOrDefaultAsync(l => l.UserId == userId && l.WeekStart == weekStart, ct);
    }
}
