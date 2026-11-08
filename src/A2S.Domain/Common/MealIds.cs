namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for the Meal aggregate root.</summary>
public readonly record struct MealId(Guid Value);

/// <summary>Strongly-typed ID for the ShoppingList aggregate root (one per user per week).</summary>
public readonly record struct ShoppingListId(Guid Value);
