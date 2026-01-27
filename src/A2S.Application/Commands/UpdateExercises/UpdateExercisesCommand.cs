using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.UpdateExercises;

/// <summary>
/// Command to update one or more exercises in a workout.
/// Supports updating Training Max, Weight, and other exercise properties.
/// </summary>
public sealed record UpdateExercisesCommand(
    Guid WorkoutId,
    IReadOnlyList<ExerciseUpdateRequest> Updates
) : ICommand<Result<UpdateExercisesResult>>;

/// <summary>
/// Request to update a single exercise.
/// </summary>
public sealed record ExerciseUpdateRequest
{
    public required Guid ExerciseId { get; init; }

    /// <summary>
    /// New Training Max value for Linear progression exercises.
    /// </summary>
    public decimal? TrainingMaxValue { get; init; }

    /// <summary>
    /// Unit for the Training Max value.
    /// </summary>
    public WeightUnit? TrainingMaxUnit { get; init; }

    /// <summary>
    /// New weight value for RepsPerSet or MinimalSets progression exercises.
    /// </summary>
    public decimal? WeightValue { get; init; }

    /// <summary>
    /// Unit for the weight value.
    /// </summary>
    public WeightUnit? WeightUnit { get; init; }

    /// <summary>
    /// Optional reason for the update (for audit purposes).
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Result of updating exercises.
/// </summary>
public sealed record UpdateExercisesResult
{
    public required Guid WorkoutId { get; init; }
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<ExerciseUpdateResult> Results { get; init; }
}

/// <summary>
/// Result of updating a single exercise.
/// </summary>
public sealed record ExerciseUpdateResult
{
    public required Guid ExerciseId { get; init; }
    public required string ExerciseName { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? PreviousValue { get; init; }
    public string? NewValue { get; init; }
}
