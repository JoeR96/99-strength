using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Commands.UndoCompletion;

/// <summary>
/// Command to undo the last completed workout day.
/// Restores progression state and makes the day available for re-submission.
/// </summary>
public sealed record UndoCompletionCommand(Guid WorkoutId)
    : ICommand<Result<UndoCompletionResult>>;

/// <summary>
/// Result of undoing a workout completion.
/// </summary>
public sealed record UndoCompletionResult
{
    public required DayNumber Day { get; init; }
    public required int WeekNumber { get; init; }
    public required bool WeekRolledBack { get; init; }
    public required int NewCurrentWeek { get; init; }
    public required int NewCurrentDay { get; init; }
    public string? Message { get; init; }
}
