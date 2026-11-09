using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Entities;

/// <summary>
/// Reference data entity representing an exercise definition in the library.
/// Not an aggregate root — exercise definitions are read-only reference data.
/// </summary>
public sealed class ExerciseDefinition : Entity<ExerciseDefinitionId>
{
    public string Name { get; private set; }
    public EquipmentType EquipmentType { get; private set; }
    public string MuscleGroup { get; private set; }
    public bool IsCompound { get; private set; }
    public string Description { get; private set; }
    public int? DefaultRepRangeMin { get; private set; }
    public int? DefaultRepRangeMax { get; private set; }
    public int? DefaultSets { get; private set; }

    private List<string> _secondaryMuscleGroups = [];

    /// <summary>
    /// Hevy muscle groups the exercise also trains (lower-case, distinct, never contains
    /// <see cref="MuscleGroup"/>). Each counts half a hard set in the volume budget.
    /// </summary>
    public IReadOnlyList<string> SecondaryMuscleGroups => _secondaryMuscleGroups.AsReadOnly();

    // EF Core constructor
    private ExerciseDefinition()
    {
        Name = string.Empty;
        MuscleGroup = string.Empty;
        Description = string.Empty;
    }

    internal ExerciseDefinition(
        ExerciseDefinitionId id,
        string name,
        EquipmentType equipmentType,
        string muscleGroup,
        bool isCompound,
        string description,
        int? defaultRepRangeMin = null,
        int? defaultRepRangeMax = null,
        int? defaultSets = null)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Exercise name cannot be empty");

        Name = name;
        EquipmentType = equipmentType;
        MuscleGroup = muscleGroup;
        IsCompound = isCompound;
        Description = description;
        DefaultRepRangeMin = defaultRepRangeMin;
        DefaultRepRangeMax = defaultRepRangeMax;
        DefaultSets = defaultSets;
    }

    /// <summary>
    /// Creates a new ExerciseDefinition with validation.
    /// </summary>
    public static ExerciseDefinition Create(
        string name,
        EquipmentType equipmentType,
        string muscleGroup,
        bool isCompound,
        string description,
        int? defaultRepRangeMin = null,
        int? defaultRepRangeMax = null,
        int? defaultSets = null)
    {
        return new ExerciseDefinition(
            new ExerciseDefinitionId(Guid.NewGuid()),
            name,
            equipmentType,
            muscleGroup,
            isCompound,
            description,
            defaultRepRangeMin,
            defaultRepRangeMax,
            defaultSets);
    }

    /// <summary>
    /// Sets the primary and secondary muscle groups. Values are trimmed and lower-cased,
    /// blanks and duplicates are dropped, and the primary group is excluded from the secondaries.
    /// </summary>
    public void SetMuscleGroups(string primary, IEnumerable<string> secondary)
    {
        var normalisedPrimary = Normalise(primary);
        CheckRule(normalisedPrimary.Length > 0, "Primary muscle group cannot be empty");

        MuscleGroup = normalisedPrimary;
        _secondaryMuscleGroups = secondary
            .Select(Normalise)
            .Where(group => group.Length > 0 && group != normalisedPrimary)
            .Distinct()
            .ToList();
    }

    private static string Normalise(string? group) =>
        (group ?? string.Empty).Trim().ToLowerInvariant();
}
