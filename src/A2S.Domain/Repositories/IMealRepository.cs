using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>A user's meals (recipes).</summary>
public interface IMealRepository : IRepository<Meal, MealId>
{
    /// <summary>All of the user's meals, ordered by type then title.</summary>
    Task<IReadOnlyList<Meal>> ListForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>The user's meals among <paramref name="ids"/>; ids that are missing or belong to someone else are skipped.</summary>
    Task<IReadOnlyList<Meal>> GetManyAsync(UserId userId, IEnumerable<MealId> ids, CancellationToken ct = default);
}
