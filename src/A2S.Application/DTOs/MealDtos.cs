using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Meals;

namespace A2S.Application.DTOs;

/// <summary>One structured ingredient line. Doubles as the input shape when saving a meal.</summary>
public sealed record IngredientDto(string Name, decimal? Quantity, string? Unit)
{
    public static IngredientDto From(Ingredient ingredient) => new(ingredient.Name, ingredient.Quantity, ingredient.Unit);

    public Ingredient ToDomain() => new(Name, Quantity, Unit);
}

/// <summary>A meal as the client sees it.</summary>
public sealed record MealDto(
    Guid Id,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType Type,
    string Description,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Tags,
    bool CutFriendly,
    string? ExternalKey)
{
    public static MealDto From(Meal meal) => new(
        meal.Id.Value,
        meal.Title,
        meal.Type,
        meal.Description,
        meal.Ingredients.Select(IngredientDto.From).ToList(),
        meal.Steps,
        meal.Tags,
        meal.CutFriendly,
        meal.ExternalKey);
}

/// <summary>Which meals are on the menu for one week (Monday start).</summary>
public sealed record WeekMealsDto(DateOnly WeekStart, IReadOnlyList<Guid> MealIds);

/// <summary>One shopping-list line with the meals that need it.</summary>
public sealed record ShoppingListItemDto(string Name, decimal? Quantity, string? Unit, bool Checked, IReadOnlyList<Guid> FromMealIds)
{
    public static ShoppingListItemDto From(ShoppingListItem item) =>
        new(item.Name, item.Quantity, item.Unit, item.Checked, item.FromMeals.Select(id => id.Value).ToList());
}

/// <summary>
/// The week's shopping list. <see cref="Id"/> is null when the week has no list yet: the client gets an
/// empty list rather than a 404 on first visit.
/// </summary>
public sealed record ShoppingListDto(
    Guid? Id,
    DateOnly WeekStart,
    IReadOnlyList<Guid> SourceMealIds,
    IReadOnlyList<ShoppingListItemDto> Items,
    int CheckedCount,
    int TotalCount)
{
    public static ShoppingListDto From(ShoppingList list) => new(
        list.Id.Value,
        list.WeekStart,
        list.SourceMealIds.Select(id => id.Value).ToList(),
        list.Items.Select(ShoppingListItemDto.From).ToList(),
        list.CheckedCount,
        list.Items.Count);

    public static ShoppingListDto Empty(DateOnly weekStart) =>
        new(null, weekStart, Array.Empty<Guid>(), Array.Empty<ShoppingListItemDto>(), 0, 0);
}

/// <summary>The exported list as text: what to send, as what, and the file name to suggest.</summary>
public sealed record ShoppingListExportDto(string Content, string ContentType, string FileName);
