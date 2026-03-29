using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Services;

/// <summary>
/// Exercise template containing only metadata about an exercise.
/// Users configure category, progression, day, and order when adding to a workout.
/// </summary>
public record ExerciseTemplate(
    string Name,
    EquipmentType Equipment,
    RepRange? DefaultRepRange = null,
    int? DefaultSets = null,
    string Description = "");

/// <summary>
/// Provides access to the exercise template library.
/// </summary>
public interface IExerciseLibraryProvider
{
    /// <summary>
    /// Gets all available exercise templates.
    /// </summary>
    IReadOnlyList<ExerciseTemplate> AllTemplates { get; }

    /// <summary>
    /// Gets an exercise template by name (case-insensitive).
    /// </summary>
    ExerciseTemplate? GetByName(string name);
}
