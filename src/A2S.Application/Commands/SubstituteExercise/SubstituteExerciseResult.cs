using A2S.Application.DTOs;

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

    /// <summary>Weekly volume caps exceeded after the substitution. Informational; the change still applied.</summary>
    public IReadOnlyList<VolumeWarningDto> Warnings { get; init; } = [];

    /// <summary>True when the substituted exercise's day is now estimated over the session time budget.</summary>
    public bool OverTimeBudget { get; init; }
}
