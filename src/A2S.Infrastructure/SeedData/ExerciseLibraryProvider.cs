using System.Reflection;
using System.Text.Json;
using A2S.Application.Services;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Provides exercise templates from embedded JSON data.
/// </summary>
public sealed class ExerciseLibraryProvider : IExerciseLibraryProvider
{
    private readonly Lazy<IReadOnlyList<ExerciseTemplate>> _templates = new(LoadTemplates);

    public IReadOnlyList<ExerciseTemplate> AllTemplates => _templates.Value;

    public ExerciseTemplate? GetByName(string name) =>
        AllTemplates.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ExerciseTemplate> LoadTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("A2S.Infrastructure.SeedData.exercise-library.json")
            ?? throw new InvalidOperationException("Embedded resource 'exercise-library.json' not found.");

        var entries = JsonSerializer.Deserialize<List<ExerciseEntry>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize exercise library JSON.");

        return entries.Select(e => new ExerciseTemplate(
            Name: e.Name,
            Equipment: Enum.Parse<EquipmentType>(e.Equipment),
            DefaultRepRange: e.DefaultRepRange is { } rr ? RepRange.Create(rr.Minimum, rr.Maximum) : null,
            DefaultSets: e.DefaultSets,
            Description: e.Description ?? ""
        )).ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ExerciseEntry(
        string Name,
        string Equipment,
        RepRangeEntry? DefaultRepRange,
        int? DefaultSets,
        string? Description);

    private sealed record RepRangeEntry(int Minimum, int Maximum);
}
