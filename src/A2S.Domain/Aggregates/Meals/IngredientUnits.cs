namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// The closed set of units an ingredient quantity may carry. Quantities are only ever summed
/// within one unit; nothing converts between them (200 g and 1 handful stay two lines).
/// </summary>
public static class IngredientUnits
{
    public const string Gram = "g";
    public const string Kilogram = "kg";
    public const string Millilitre = "ml";
    public const string Litre = "l";
    public const string Tablespoon = "tbsp";
    public const string Teaspoon = "tsp";
    public const string Piece = "piece";
    public const string Clove = "clove";
    public const string Slice = "slice";
    public const string Handful = "handful";
    public const string Pinch = "pinch";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Gram, Kilogram, Millilitre, Litre, Tablespoon, Teaspoon, Piece, Clove, Slice, Handful, Pinch
    };

    public static bool IsKnown(string? unit) => unit is not null && All.Contains(unit);

    /// <summary>Lower-cases and trims a unit so "G" and " g " are the gram unit; blank becomes null.</summary>
    public static string? Normalise(string? unit) =>
        string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToLowerInvariant();
}
