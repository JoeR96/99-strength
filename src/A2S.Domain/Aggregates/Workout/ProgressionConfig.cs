using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Bundles all parameters needed to create an ExerciseProgression strategy.
/// Each strategy type uses a subset of these properties.
/// </summary>
public sealed record ProgressionConfig
{
    public required string ProgressionType { get; init; }
    public required EquipmentType EquipmentType { get; init; }

    // Linear
    public TrainingMax? TrainingMax { get; init; }
    public bool? UseAmrap { get; init; }
    public int? BaseSetsPerExercise { get; init; }

    // RepsPerSet
    public RepRange? RepRange { get; init; }
    public int? TargetSets { get; init; }
    public bool? IsUnilateral { get; init; }

    // Shared (RepsPerSet + MinimalSets)
    public int? StartingSets { get; init; }
    public Weight? StartingWeight { get; init; }

    // MinimalSets
    public int? TargetTotalReps { get; init; }
    public int? MinimumSets { get; init; }
    public int? MaximumSets { get; init; }
}
