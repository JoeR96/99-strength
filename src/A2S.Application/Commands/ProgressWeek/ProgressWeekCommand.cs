using A2S.Application.Common;

namespace A2S.Application.Commands.ProgressWeek;

/// <summary>
/// Command to progress a workout to the next week.
/// This advances the current week, updates the block if necessary,
/// and handles deload week transitions.
/// </summary>
public sealed record ProgressWeekCommand(Guid WorkoutId) : ICommand<Result<ProgressWeekResult>>;

/// <summary>
/// Result of progressing to the next week.
/// </summary>
public sealed record ProgressWeekResult
{
    /// <summary>
    /// The week number before progression.
    /// </summary>
    public required int PreviousWeek { get; init; }

    /// <summary>
    /// The new week number after progression.
    /// </summary>
    public required int NewWeek { get; init; }

    /// <summary>
    /// The block number after progression.
    /// </summary>
    public required int NewBlock { get; init; }

    /// <summary>
    /// Whether the new week is a deload week.
    /// </summary>
    public required bool IsDeloadWeek { get; init; }

    /// <summary>
    /// Whether the program has completed after this progression.
    /// </summary>
    public required bool IsProgramComplete { get; init; }
}
