using System.Text.Json;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Seeds ExerciseDefinition data from the embedded exercise-library.json.
/// Upserts by name: rows that already exist (including ones inserted by data migrations such as
/// the Cable Core Pallof Press) are left untouched and only missing rows are inserted, so a fresh
/// database always ends up with the full library. Never deletes.
/// </summary>
public static class ExerciseDefinitionSeeder
{
    public const string ResourceName = "A2S.Infrastructure.SeedData.exercise-library.json";

    /// <summary>Returns the number of definitions inserted in this run.</summary>
    public static async Task<int> SeedAsync(A2SDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        var entries = await LoadLibraryAsync(cancellationToken);

        var existingNames = (await dbContext.ExerciseDefinitions
                .Select(d => d.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = entries.Where(e => !existingNames.Contains(e.Name)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Seeding {Count} missing exercise definitions from exercise-library.json...", missing.Count);

        var definitions = new List<ExerciseDefinition>(missing.Count);
        foreach (var entry in missing)
        {
            var equipmentType = Enum.Parse<EquipmentType>(entry.Equipment);
            var deterministicId = GenerateDeterministicId(entry.Name);
            definitions.Add(new ExerciseDefinition(
                new ExerciseDefinitionId(deterministicId),
                entry.Name,
                equipmentType,
                entry.MuscleGroup ?? "General",
                entry.IsCompound ?? true,
                entry.Description ?? "",
                entry.DefaultRepRange?.Minimum,
                entry.DefaultRepRange?.Maximum,
                entry.DefaultSets));
        }

        await dbContext.ExerciseDefinitions.AddRangeAsync(definitions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} exercise definitions.", definitions.Count);
        return definitions.Count;
    }

    /// <summary>Reads the embedded library. Exposed so tests can assert against the source of truth.</summary>
    public static async Task<IReadOnlyList<ExerciseEntry>> LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        var assembly = typeof(ExerciseDefinitionSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Embedded resource 'exercise-library.json' not found.");

        return await JsonSerializer.DeserializeAsync<List<ExerciseEntry>>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize exercise library JSON.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record ExerciseEntry(
        string Name,
        string Equipment,
        string? MuscleGroup,
        bool? IsCompound,
        RepRangeEntry? DefaultRepRange,
        int? DefaultSets,
        string? Description);

    public sealed record RepRangeEntry(int Minimum, int Maximum);

    private static Guid GenerateDeterministicId(string name)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(name));
        return new Guid(bytes);
    }
}
