using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// One line of the shopping list: a normalised ingredient name, the summed quantity in one unit
/// (or neither for free-text ingredients), whether it has been ticked, and the meals that need it.
/// Immutable value object; ticking produces a new item.
/// </summary>
public sealed record ShoppingListItem
{
    public string Name { get; }
    public decimal? Quantity { get; }
    public string? Unit { get; }
    public bool Checked { get; }
    public IReadOnlyList<MealId> FromMeals { get; }

    public ShoppingListItem(string name, decimal? quantity, string? unit, bool @checked, IReadOnlyList<MealId> fromMeals)
    {
        var normalised = IngredientName.Normalise(name);
        if (normalised.Length == 0)
        {
            throw new BusinessRuleViolationException("Shopping list item name is required.");
        }

        var cleanUnit = IngredientUnits.Normalise(unit);
        if (quantity.HasValue != (cleanUnit is not null))
        {
            throw new BusinessRuleViolationException("A shopping list item has both a quantity and a unit, or neither.");
        }

        Name = normalised;
        Quantity = quantity;
        Unit = cleanUnit;
        Checked = @checked;
        FromMeals = (fromMeals ?? Array.Empty<MealId>()).Distinct().ToList();
    }

    /// <summary>The identity items are matched on across regenerations: normalised name plus unit.</summary>
    public ShoppingItemKey Key => new(Name, Unit);

    public bool Matches(string name, string? unit) => Key == ShoppingItemKey.Of(name, unit);

    public ShoppingListItem WithChecked(bool value) => value == Checked ? this : new(Name, Quantity, Unit, value, FromMeals);

    public bool Equals(ShoppingListItem? other) =>
        other is not null
        && Name == other.Name
        && Quantity == other.Quantity
        && Unit == other.Unit
        && Checked == other.Checked
        && FromMeals.SequenceEqual(other.FromMeals);

    public override int GetHashCode() => HashCode.Combine(Name, Quantity, Unit, Checked, FromMeals.Count);
}

/// <summary>What makes two shopping lines the same line: the normalised name and the unit.</summary>
public readonly record struct ShoppingItemKey(string Name, string? Unit)
{
    public static ShoppingItemKey Of(string name, string? unit) =>
        new(IngredientName.Normalise(name), IngredientUnits.Normalise(unit));
}
