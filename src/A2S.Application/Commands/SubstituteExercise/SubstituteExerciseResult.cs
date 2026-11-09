namespace A2S.Application.Commands.SubstituteExercise;

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
