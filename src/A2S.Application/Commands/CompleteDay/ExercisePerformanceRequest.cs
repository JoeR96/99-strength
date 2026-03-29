using A2S.Domain.Enums;

namespace A2S.Application.Commands.CompleteDay;

/// <summary>
/// Request data for a single exercise's performance.
/// </summary>
public sealed record ExercisePerformanceRequest
{
    /// <summary>
    /// The ID of the exercise that was performed.
    /// </summary>
    public required Guid ExerciseId { get; init; }

    /// <summary>
    /// The sets completed for this exercise.
    /// </summary>
    public required IReadOnlyList<CompletedSetRequest> CompletedSets { get; init; }

    /// <summary>
    /// Whether this exercise was temporarily substituted for this session only.
    /// When true, progression rules are skipped (no TM changes, no set changes).
    /// The performance is still recorded but the original exercise's progression state is preserved.
    /// </summary>
    public bool WasTemporarySubstitution { get; init; }
}

/// <summary>
/// Request data for a single completed set.
/// </summary>
public sealed record CompletedSetRequest
{
    public required int SetNumber { get; init; }
    public required decimal Weight { get; init; }
    public required WeightUnit WeightUnit { get; init; }
    public required int ActualReps { get; init; }
    public bool WasAmrap { get; init; }
}
