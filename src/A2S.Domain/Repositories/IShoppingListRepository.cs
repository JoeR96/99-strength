using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>One shopping list per user per week.</summary>
public interface IShoppingListRepository : IRepository<ShoppingList, ShoppingListId>
{
    Task<ShoppingList?> GetForWeekAsync(UserId userId, DateOnly weekStart, CancellationToken ct = default);
}
