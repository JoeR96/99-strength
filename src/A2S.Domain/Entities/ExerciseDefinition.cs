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

    // EF Core constructor
    private ExerciseDefinition()
    {
        Name = string.Empty;
        MuscleGroup = string.Empty;
        Description = string.Empty;
    }

    public ExerciseDefinition(
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
}
