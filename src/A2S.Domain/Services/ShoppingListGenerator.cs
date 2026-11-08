using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;

namespace A2S.Domain.Services;

/// <summary>
/// Turns the week's meals into one shopping list: ingredients with the same normalised name and the
/// same unit become one line with the quantities summed; unquantified ingredients are de-duplicated
/// by name; units are never converted. Pure, no I/O.
/// </summary>
public static class ShoppingListGenerator
{
    public static IReadOnlyList<ShoppingListItem> Build(IEnumerable<Meal> meals)
    {
        var lines = new Dictionary<ShoppingItemKey, Line>();

        foreach (var meal in meals ?? Array.Empty<Meal>())
        {
            foreach (var ingredient in meal.Ingredients)
            {
                var key = ShoppingItemKey.Of(ingredient.Name, ingredient.Unit);
                if (!lines.TryGetValue(key, out var line))
                {
                    line = new Line(ingredient.Quantity);
                    lines[key] = line;
                }
                else if (ingredient.IsQuantified)
                {
                    line.Quantity = (line.Quantity ?? 0) + ingredient.Quantity!.Value;
                }

                if (!line.From.Contains(meal.Id))
                {
                    line.From.Add(meal.Id);
                }
            }
        }

        return lines
            .Select(kv => new ShoppingListItem(kv.Key.Name, kv.Value.Quantity, kv.Key.Unit, false, kv.Value.From))
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ThenBy(i => i.Unit, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class Line
    {
        public Line(decimal? quantity) => Quantity = quantity;

        public decimal? Quantity { get; set; }
        public List<MealId> From { get; } = new();
    }
}
