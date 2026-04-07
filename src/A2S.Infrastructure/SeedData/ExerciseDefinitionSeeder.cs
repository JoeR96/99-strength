using System.Reflection;
using System.Text.Json;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Seeds ExerciseDefinition data from the embedded exercise-library.json
/// into the database if the table is empty.
/// </summary>
public static class ExerciseDefinitionSeeder
{
    public static async Task SeedAsync(A2SDbContext dbContext, ILogger logger)
    {
        if (await dbContext.ExerciseDefinitions.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding exercise definitions from exercise-library.json...");

        var assembly = typeof(ExerciseDefinitionSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream("A2S.Infrastructure.SeedData.exercise-library.json")
            ?? throw new InvalidOperationException("Embedded resource 'exercise-library.json' not found.");

        var entries = await JsonSerializer.DeserializeAsync<List<ExerciseEntry>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize exercise library JSON.");

        var definitions = new List<ExerciseDefinition>(entries.Count);
        foreach (var entry in entries)
        {
            var equipmentType = Enum.Parse<EquipmentType>(entry.Equipment);
            var deterministicId = GenerateDeterministicId(entry.Name);
            var definition = new ExerciseDefinition(
                new ExerciseDefinitionId(deterministicId),
                entry.Name,
                equipmentType,
                entry.MuscleGroup ?? "General",
                entry.IsCompound ?? true,
                entry.Description ?? "",
                entry.DefaultRepRange?.Minimum,
                entry.DefaultRepRange?.Maximum,
                entry.DefaultSets);

            definitions.Add(definition);
        }

        await dbContext.ExerciseDefinitions.AddRangeAsync(definitions);

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} exercise definitions.", entries.Count);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ExerciseEntry(
        string Name,
        string Equipment,
        string? MuscleGroup,
        bool? IsCompound,
        RepRangeEntry? DefaultRepRange,
        int? DefaultSets,
        string? Description);

    private sealed record RepRangeEntry(int Minimum, int Maximum);

    private static Guid GenerateDeterministicId(string name)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(name));
        return new Guid(bytes);
    }
}
