using A2S.Application.Common;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.Commands.Meals;

/// <summary>
/// The ownership checks every meal and shopping-list handler makes: an authenticated caller, an
/// existing aggregate, and that aggregate belonging to the caller. Someone else's meal or list is
/// reported as not found, never as forbidden, so ids cannot be probed.
/// </summary>
internal static class MealAccess
{
    public static Result<UserId> RequireUser(ICurrentUserService currentUser)
    {
        var userId = currentUser.GetUserId();
        return userId is null
            ? Result.Failure<UserId>("User must be authenticated.", ErrorCode.Unauthenticated)
            : Result.Success(userId.Value);
    }

    public static async Task<Result<Meal>> LoadOwnedMealAsync(IMealRepository meals, UserId userId, Guid mealId, CancellationToken ct)
    {
        var meal = await meals.GetByIdAsync(new MealId(mealId), ct);
        return meal is null || meal.UserId != userId
            ? Result.Failure<Meal>("Meal not found.", ErrorCode.NotFound)
            : Result.Success(meal);
    }

    public static async Task<Result<ShoppingList>> LoadOwnedListAsync(IShoppingListRepository lists, UserId userId, Guid listId, CancellationToken ct)
    {
        var list = await lists.GetByIdAsync(new ShoppingListId(listId), ct);
        return list is null || list.UserId != userId
            ? Result.Failure<ShoppingList>("Shopping list not found.", ErrorCode.NotFound)
            : Result.Success(list);
    }

    /// <summary>The week's list, creating an empty one (not yet saved) when the week has none.</summary>
    public static async Task<(ShoppingList List, bool Created)> LoadOrCreateListAsync(
        IShoppingListRepository lists,
        UserId userId,
        DateOnly weekStart,
        CancellationToken ct)
    {
        var existing = await lists.GetForWeekAsync(userId, weekStart, ct);
        return existing is null
            ? (ShoppingList.Create(userId, weekStart, Array.Empty<MealId>()), true)
            : (existing, false);
    }

    public static Result<T> Fail<T>(Result failed) => Result.Failure<T>(failed.Error, failed.ErrorCode);

    public static Result<T> FromDomain<T>(DomainException exception) =>
        Result.Failure<T>(exception.Message, ErrorCode.DomainRuleViolation);
}
