using System.Text.Json;
using System.Text.Json.Serialization;
using A2S.Application.Interfaces;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Seeds the starter recipes (CooksAssistant's set, ingredients parsed by hand once into
/// meals-seed.json) for one user. Upserts by <see cref="Meal.ExternalKey"/>: missing meals are
/// inserted, existing ones are left exactly as the user has them. Never deletes.
/// </summary>
public sealed class MealSeeder : IMealSeeder
{
    public const string ResourceName = "A2S.Infrastructure.SeedData.meals-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly A2SDbContext _dbContext;
    private readonly ILogger<MealSeeder> _logger;

    public MealSeeder(A2SDbContext dbContext, ILogger<MealSeeder> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EnsureForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var entries = await LoadSeedAsync(ct);

        var existingKeys = (await _dbContext.Meals
                .Where(m => m.UserId == userId && m.ExternalKey != null)
                .Select(m => m.ExternalKey!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var missing = entries.Where(e => !existingKeys.Contains(e.Key)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        var meals = missing.Select(e => ToMeal(userId, e)).ToList();
        await _dbContext.Meals.AddRangeAsync(meals, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded {Count} starter meals for user {UserId}.", meals.Count, userId.Value);
        return meals.Count;
    }

    /// <summary>Builds the aggregate for one seed entry. Public so tests can check the parsed set without a database.</summary>
    public static Meal ToMeal(UserId userId, MealSeedEntry entry) => Meal.Create(
        userId,
        entry.Title,
        entry.MealType,
        entry.Description,
        entry.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit)),
        entry.Steps,
        entry.Tags,
        cutFriendly: true,
        externalKey: entry.Key);

    /// <summary>Reads the embedded seed. Exposed so tests can assert against the source of truth.</summary>
    public static async Task<IReadOnlyList<MealSeedEntry>> LoadSeedAsync(CancellationToken ct = default)
    {
        var assembly = typeof(MealSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Embedded resource 'meals-seed.json' not found.");

        return await JsonSerializer.DeserializeAsync<List<MealSeedEntry>>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize meals seed JSON.");
    }

    public sealed record MealSeedEntry(
        string Key,
        string Title,
        MealType MealType,
        string? Description,
        IReadOnlyList<IngredientSeedEntry> Ingredients,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> Tags);

    public sealed record IngredientSeedEntry(string Name, decimal? Quantity, string? Unit);
}
