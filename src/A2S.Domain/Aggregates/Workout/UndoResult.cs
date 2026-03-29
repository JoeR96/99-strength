using A2S.Domain.Enums;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Result of an undo operation, indicating what was undone.
/// </summary>
public sealed record UndoResult(
    DayNumber Day,
    int WeekNumber,
    bool WeekRolledBack);
