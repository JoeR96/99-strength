using A2S.Domain.Enums;

namespace A2S.Application.DTOs;

/// <summary>
/// DTO representing an exercise template from the library.
/// Templates only contain exercise metadata.
/// Users configure category, progression, day, and order when adding to workout.
/// </summary>
public sealed record ExerciseTemplateDto
{
    public string Name { get; init; } = string.Empty;
    public EquipmentType Equipment { get; init; }
    public RepRangeDto? DefaultRepRange { get; init; }
    public int? DefaultSets { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>Hevy primary muscle group (lower-case), e.g. "chest".</summary>
    public string MuscleGroup { get; init; } = string.Empty;

    /// <summary>Hevy secondary muscle groups (lower-case); each counts half a hard set in the volume budget.</summary>
    public IReadOnlyList<string> SecondaryMuscleGroups { get; init; } = [];
}

/// <summary>
/// DTO representing a rep range.
/// </summary>
public sealed record RepRangeDto
{
    public int Minimum { get; init; }
    public int Maximum { get; init; }

    public override string ToString() => $"{Minimum}-{Maximum}";
}
