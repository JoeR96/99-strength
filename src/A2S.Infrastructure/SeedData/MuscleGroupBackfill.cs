using System.Text.Json;
using System.Text.RegularExpressions;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Tags every ExerciseDefinition with its Hevy primary muscle group (parsed from the
/// "Hevy: &lt;group&gt;" token in Description) and secondary groups (from the embedded
/// hevy-exercises.json, matched by title). Idempotent: only rows whose values differ are written.
/// </summary>
public static partial class MuscleGroupBackfill
{
    public const string ResourceName = "A2S.Infrastructure.SeedData.hevy-exercises.json";

    [GeneratedRegex(@"Hevy:\s*([a-z_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HevyToken();

    /// <summary>Returns the number of definitions updated in this run.</summary>
    public static async Task<int> RunAsync(A2SDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        var hevyByTitle = await LoadHevyExercisesAsync(cancellationToken);
        var definitions = await dbContext.ExerciseDefinitions.ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var definition in definitions)
        {
            hevyByTitle.TryGetValue(definition.Name, out var hevy);

            var primary = ParsePrimary(definition.Description) ?? hevy?.PrimaryMuscleGroup;
            if (string.IsNullOrWhiteSpace(primary))
            {
                continue;
            }

            var normalisedPrimary = primary.Trim().ToLowerInvariant();
            var secondary = Normalise(hevy?.SecondaryMuscleGroups ?? [], normalisedPrimary);
            if (definition.MuscleGroup == normalisedPrimary
                && definition.SecondaryMuscleGroups.SequenceEqual(secondary))
            {
                continue;
            }

            definition.SetMuscleGroups(normalisedPrimary, secondary);
            updated++;
        }

        if (updated == 0)
        {
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Backfilled muscle groups on {Count} exercise definitions.", updated);
        return updated;
    }

    private static string? ParsePrimary(string? description)
    {
        var match = HevyToken().Match(description ?? string.Empty);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static List<string> Normalise(IEnumerable<string> groups, string normalisedPrimary)
    {
        return groups
            .Select(g => (g ?? string.Empty).Trim().ToLowerInvariant())
            .Where(g => g.Length > 0 && g != normalisedPrimary)
            .Distinct()
            .ToList();
    }

    private static async Task<IReadOnlyDictionary<string, HevyExercise>> LoadHevyExercisesAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(MuscleGroupBackfill).Assembly;
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Embedded resource 'hevy-exercises.json' not found.");

        var exercises = await JsonSerializer.DeserializeAsync<List<HevyExercise>>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize hevy-exercises.json.");

        var byTitle = new Dictionary<string, HevyExercise>(StringComparer.OrdinalIgnoreCase);
        foreach (var exercise in exercises)
        {
            byTitle.TryAdd(exercise.Title, exercise);
        }

        return byTitle;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed record HevyExercise(
        string Title,
        string? PrimaryMuscleGroup,
        List<string>? SecondaryMuscleGroups);
}
