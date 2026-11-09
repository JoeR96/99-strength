using System.Text.Json.Serialization;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;

namespace A2S.Api.Contracts.Requests;

/// <summary>Body for creating or replacing a meal.</summary>
public sealed record SaveMealRequest(
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType Type,
    string? Description,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string>? Steps,
    IReadOnlyList<string>? Tags,
    bool CutFriendly = true);

/// <summary>Body for setting a week's menu. Any date in the week is accepted.</summary>
public sealed record SetWeekMealsRequest(DateOnly WeekStart, IReadOnlyList<Guid> MealIds);

/// <summary>Body for generating (or regenerating) a week's shopping list.</summary>
public sealed record GenerateShoppingListRequest(DateOnly WeekStart);

/// <summary>Body for ticking one shopping-list line.</summary>
public sealed record ToggleShoppingItemRequest(Guid ListId, string Name, string? Unit);
