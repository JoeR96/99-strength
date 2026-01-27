using A2S.Application.Common;

namespace A2S.Application.Commands.SubstituteExercise;

/// <summary>
/// Command to permanently substitute an exercise with another.
/// This replaces the exercise name while preserving all progression data.
/// </summary>
public sealed record SubstituteExerciseCommand(
    Guid WorkoutId,
    Guid ExerciseId,
    string NewExerciseName,
    string? NewHevyExerciseTemplateId = null,
    string? Reason = null
) : ICommand<Result<SubstituteExerciseResult>>;

/// <summary>
/// Result of substituting an exercise.
/// </summary>
public sealed record SubstituteExerciseResult
{
    public required Guid ExerciseId { get; init; }
    public required string OriginalName { get; init; }
    public required string NewName { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
}
