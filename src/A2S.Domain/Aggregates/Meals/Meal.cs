using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// A recipe a person can put on this week's menu: structured ingredients (so a shopping list can be
/// summed from several meals), steps and tags. Seeded meals carry an <see cref="ExternalKey"/>
/// (<c>cooks:&lt;slug&gt;</c>) so seeding is idempotent per user.
/// </summary>
public sealed class Meal : AggregateRoot<MealId>
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxExternalKeyLength = 100;

    private readonly List<Ingredient> _ingredients = new();
    private readonly List<string> _steps = new();
    private readonly List<string> _tags = new();

    private Meal(
        MealId id,
        UserId userId,
        string title,
        MealType type,
        string description,
        List<Ingredient> ingredients,
        List<string> steps,
        List<string> tags,
        bool cutFriendly,
        string? externalKey)
        : base(id)
    {
        UserId = userId;
        Title = title;
        Type = type;
        Description = description;
        _ingredients = ingredients;
        _steps = steps;
        _tags = tags;
        CutFriendly = cutFriendly;
        ExternalKey = externalKey;
    }

    // EF Core constructor
    private Meal()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public UserId UserId { get; private set; }
    public string Title { get; private set; }
    public MealType Type { get; private set; }
    public string Description { get; private set; }
    public IReadOnlyList<Ingredient> Ingredients => _ingredients.AsReadOnly();
    public IReadOnlyList<string> Steps => _steps.AsReadOnly();
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    /// <summary>Whether the meal suits a cutting phase. All seeded meals are.</summary>
    public bool CutFriendly { get; private set; }

    /// <summary>Stable key from the seed source (<c>cooks:creamy-lemon-orzo</c>), null for a user's own meal.</summary>
    public string? ExternalKey { get; private set; }

    public static Meal Create(
        UserId userId,
        string title,
        MealType type,
        string? description,
        IEnumerable<Ingredient> ingredients,
        IEnumerable<string>? steps,
        IEnumerable<string>? tags,
        bool cutFriendly,
        string? externalKey = null)
    {
        var cleanKey = string.IsNullOrWhiteSpace(externalKey) ? null : externalKey.Trim();
        CheckRule(cleanKey is null || cleanKey.Length <= MaxExternalKeyLength, $"Meal external key cannot be longer than {MaxExternalKeyLength} characters.");

        return new Meal(
            new MealId(Guid.NewGuid()),
            userId,
            CleanTitle(title),
            type,
            CleanDescription(description),
            CleanIngredients(ingredients),
            CleanLines(steps),
            CleanTags(tags),
            cutFriendly,
            cleanKey);
    }

    /// <summary>Replaces everything but the owner and the external key. Nothing changes if any part is invalid.</summary>
    public void Update(
        string title,
        MealType type,
        string? description,
        IEnumerable<Ingredient> ingredients,
        IEnumerable<string>? steps,
        IEnumerable<string>? tags,
        bool cutFriendly)
    {
        var cleanTitle = CleanTitle(title);
        var cleanDescription = CleanDescription(description);
        var cleanIngredients = CleanIngredients(ingredients);
        var cleanSteps = CleanLines(steps);
        var cleanTags = CleanTags(tags);

        Title = cleanTitle;
        Type = type;
        Description = cleanDescription;
        _ingredients.Clear();
        _ingredients.AddRange(cleanIngredients);
        _steps.Clear();
        _steps.AddRange(cleanSteps);
        _tags.Clear();
        _tags.AddRange(cleanTags);
        CutFriendly = cutFriendly;
    }

    private static string CleanTitle(string title)
    {
        CheckRule(!string.IsNullOrWhiteSpace(title), "Meal title cannot be empty.");
        var trimmed = title.Trim();
        CheckRule(trimmed.Length <= MaxTitleLength, $"Meal title cannot be longer than {MaxTitleLength} characters.");
        return trimmed;
    }

    private static string CleanDescription(string? description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        CheckRule(trimmed.Length <= MaxDescriptionLength, $"Meal description cannot be longer than {MaxDescriptionLength} characters.");
        return trimmed;
    }

    private static List<Ingredient> CleanIngredients(IEnumerable<Ingredient> ingredients)
    {
        var list = ingredients?.ToList() ?? new List<Ingredient>();
        CheckRule(list.Count > 0, "A meal needs at least one ingredient.");
        return list;
    }

    private static List<string> CleanLines(IEnumerable<string>? lines) =>
        (lines ?? Array.Empty<string>())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToList();

    private static List<string> CleanTags(IEnumerable<string>? tags) =>
        (tags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
}
