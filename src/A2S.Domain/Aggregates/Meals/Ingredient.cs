using System.Text.RegularExpressions;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// One line of a recipe, already structured: a name, and either a quantity with a unit from the
/// closed set or neither ("fresh parsley"). Unquantified ingredients are de-duplicated on the
/// shopping list, never summed. Immutable value object.
/// </summary>
public sealed record Ingredient
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public string Name { get; }
    public decimal? Quantity { get; }
    public string? Unit { get; }

    public Ingredient(string name, decimal? quantity, string? unit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Ingredient name is required.");
        }

        var cleanUnit = IngredientUnits.Normalise(unit);
        if (quantity.HasValue != (cleanUnit is not null))
        {
            throw new BusinessRuleViolationException("An ingredient has both a quantity and a unit, or neither.");
        }

        if (quantity is <= 0)
        {
            throw new BusinessRuleViolationException("Ingredient quantity must be greater than 0.");
        }

        if (cleanUnit is not null && !IngredientUnits.IsKnown(cleanUnit))
        {
            throw new BusinessRuleViolationException($"Unknown ingredient unit '{unit}'.");
        }

        Name = Spaces.Replace(name.Trim(), " ");
        Quantity = quantity;
        Unit = cleanUnit;
    }

    /// <summary>Whether this ingredient can be summed with others of the same name and unit.</summary>
    public bool IsQuantified => Quantity.HasValue;

    /// <summary>A free-text ingredient with no quantity ("salt", "fresh parsley").</summary>
    public static Ingredient Unquantified(string name) => new(name, null, null);
}
