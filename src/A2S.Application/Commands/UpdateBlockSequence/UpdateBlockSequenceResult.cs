namespace A2S.Application.Commands.UpdateBlockSequence;

/// <summary>
/// Result of updating the block sequence.
/// </summary>
public sealed record UpdateBlockSequenceResult
{
    public required Guid WorkoutId { get; init; }
    public required IReadOnlyList<int> BlockSequence { get; init; }
    public required int TotalWeeks { get; init; }
    public required int CurrentWeek { get; init; }
    public required int CurrentBlock { get; init; }
}
