namespace A2S.Application.Commands.UpdateExercises;

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
