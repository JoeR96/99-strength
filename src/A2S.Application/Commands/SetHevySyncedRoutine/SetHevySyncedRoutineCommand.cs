using A2S.Application.Common;

namespace A2S.Application.Commands.SetHevySyncedRoutine;

/// <summary>
/// Command to record that a routine was synced to Hevy.
/// </summary>
public sealed record SetHevySyncedRoutineCommand(
    Guid WorkoutId,
    int WeekNumber,
    int DayNumber,
    string RoutineId) : ICommand<Result<bool>>;
