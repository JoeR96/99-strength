using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Services;

/// <summary>
/// Exercise template containing only metadata about an exercise.
/// Users configure category, progression, day, and order when adding to a workout.
/// <see cref="MuscleGroup"/> and <see cref="SecondaryMuscleGroups"/> are Hevy group names
/// (lower-case) used by the weekly volume budget; an empty or unknown group is simply not counted.
/// </summary>
public record ExerciseTemplate(
    string Name,
    EquipmentType Equipment,
    RepRange? DefaultRepRange = null,
    int? DefaultSets = null,
    string Description = "",
    string MuscleGroup = "",
    IReadOnlyList<string>? SecondaryMuscleGroups = null)
{
    public IReadOnlyList<string> SecondaryMuscleGroups { get; init; } = SecondaryMuscleGroups ?? [];
}

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

    /// <summary>
    /// Asynchronously gets all available exercise templates.
    /// Prefer this over AllTemplates to avoid sync-over-async.
    /// </summary>
    Task<IReadOnlyList<ExerciseTemplate>> GetAllTemplatesAsync(CancellationToken ct = default);
}
