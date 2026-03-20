using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.SubstituteExercise;

/// <summary>
/// Command to permanently substitute an exercise with another.
/// Can optionally change the progression type and configuration.
/// </summary>
public sealed record SubstituteExerciseCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    string NewExerciseName,
    string? NewHevyExerciseTemplateId = null,
    string? Reason = null,
    ProgressionConfigDto? NewProgressionConfig = null
) : ICommand<Result<SubstituteExerciseResult>>;

/// <summary>
/// Configuration for a new progression type when substituting an exercise.
/// </summary>
public sealed record ProgressionConfigDto
{
    /// <summary>
    /// The progression type: "Linear", "RepsPerSet", or "MinimalSets"
    /// </summary>
    public required string Type { get; init; }

    // Linear progression options
    public decimal? TrainingMaxValue { get; init; }
    public WeightUnit? TrainingMaxUnit { get; init; }
    public bool? UseAmrap { get; init; }
    public int? BaseSetsPerExercise { get; init; }

    // RepsPerSet progression options
    public int? RepRangeMinimum { get; init; }
    public int? RepRangeTarget { get; init; }
    public int? RepRangeMaximum { get; init; }
    public int? TargetSets { get; init; }
    public int? StartingSets { get; init; }
    public int? CurrentSets { get; init; }
    public decimal? StartingWeight { get; init; }
    public WeightUnit? WeightUnit { get; init; }
    public bool? IsUnilateral { get; init; }

    // MinimalSets progression options
    public int? TargetTotalReps { get; init; }
    public int? MinimumSets { get; init; }
    public int? MaximumSets { get; init; }
}

/// <summary>
/// Result of substituting an exercise.
/// </summary>
public sealed record SubstituteExerciseResult
{
    public required Guid ExerciseId { get; init; }
    public required string OriginalName { get; init; }
    public required string NewName { get; init; }
    public required bool Success { get; init; }
    public bool ProgressionTypeChanged { get; init; }
    public string? NewProgressionType { get; init; }
    public string? Message { get; init; }
}
