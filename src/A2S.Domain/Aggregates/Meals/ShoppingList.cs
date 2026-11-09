using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// One week's shopping list: which meals are on the menu (Monday to Sunday) and the de-duplicated,
/// summed items built from them. Regenerating after the menu changes keeps the ticks on lines that
/// are still needed.
/// </summary>
public sealed class ShoppingList : AggregateRoot<ShoppingListId>
{
    private readonly List<MealId> _sourceMealIds = new();
    private readonly List<ShoppingListItem> _items = new();

    private ShoppingList(ShoppingListId id, UserId userId, DateOnly weekStart, List<MealId> sourceMealIds, List<ShoppingListItem> items)
        : base(id)
    {
        UserId = userId;
        WeekStart = weekStart;
        _sourceMealIds = sourceMealIds;
        _items = items;
    }

    // EF Core constructor
    private ShoppingList() { }

    public UserId UserId { get; private set; }

    /// <summary>Always a Monday.</summary>
    public DateOnly WeekStart { get; private set; }

    /// <summary>The meals on this week's menu, in the order they were chosen.</summary>
    public IReadOnlyList<MealId> SourceMealIds => _sourceMealIds.AsReadOnly();

    /// <summary>Sorted by name, then unit.</summary>
    public IReadOnlyList<ShoppingListItem> Items => _items.AsReadOnly();

    public int CheckedCount => _items.Count(i => i.Checked);

    public static ShoppingList Create(UserId userId, DateOnly weekStart, IEnumerable<MealId> sourceMealIds, IEnumerable<ShoppingListItem>? items = null)
    {
        CheckRule(weekStart.DayOfWeek == DayOfWeek.Monday, "A shopping list week starts on a Monday.");
        return new ShoppingList(
            new ShoppingListId(Guid.NewGuid()),
            userId,
            weekStart,
            DistinctIds(sourceMealIds),
            Ordered(items ?? Array.Empty<ShoppingListItem>()));
    }

    /// <summary>The Monday of the week <paramref name="date"/> falls in.</summary>
    public static DateOnly WeekStartOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Replaces the week's menu. Items are untouched until the next <see cref="Regenerate"/>.</summary>
    public void SetSourceMeals(IEnumerable<MealId> mealIds)
    {
        var ids = DistinctIds(mealIds);
        _sourceMealIds.Clear();
        _sourceMealIds.AddRange(ids);
    }

    /// <summary>
    /// Replaces the items with a freshly built set, carrying <c>Checked</c> over to any new item whose
    /// (name, unit) key was ticked before. Lines no longer needed are dropped.
    /// </summary>
    public void Regenerate(IEnumerable<ShoppingListItem> items)
    {
        var ticked = _items.Where(i => i.Checked).Select(i => i.Key).ToHashSet();
        var next = Ordered(items ?? Array.Empty<ShoppingListItem>())
            .Select(i => i.WithChecked(ticked.Contains(i.Key)))
            .ToList();

        _items.Clear();
        _items.AddRange(next);
    }

    /// <summary>Flips one line's tick. Returns false when no line has that name and unit.</summary>
    public bool Toggle(string name, string? unit)
    {
        var index = _items.FindIndex(i => i.Matches(name, unit));
        if (index < 0)
        {
            return false;
        }

        _items[index] = _items[index].WithChecked(!_items[index].Checked);
        return true;
    }

    private static List<MealId> DistinctIds(IEnumerable<MealId> ids) => (ids ?? Array.Empty<MealId>()).Distinct().ToList();

    private static List<ShoppingListItem> Ordered(IEnumerable<ShoppingListItem> items)
    {
        var list = items.ToList();
        var keys = list.Select(i => i.Key).ToList();
        CheckRule(keys.Distinct().Count() == keys.Count, "Shopping list items must have distinct name and unit.");
        return list
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ThenBy(i => i.Unit, StringComparer.Ordinal)
            .ToList();
    }
}
