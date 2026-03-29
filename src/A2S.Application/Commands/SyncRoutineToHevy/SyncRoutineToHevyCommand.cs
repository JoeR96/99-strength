using A2S.Application.Common;

namespace A2S.Application.Commands.SyncRoutineToHevy;

/// <summary>
/// Command to sync a workout day's routine to Hevy.
/// Uses the domain's calculated planned sets.
/// Implements IWorkoutCommand so AuthorizedWorkoutBehavior validates access.
/// </summary>
public sealed record SyncRoutineToHevyCommand(
    Guid WorkoutId,
    int WeekNumber,
    int DayNumber,
    string HevyApiKey) : IWorkoutCommand<Result<SyncRoutineResult>>, IFailureFactory<Result<SyncRoutineResult>>
{
    public static Result<SyncRoutineResult> CreateFailure(string error, ErrorCode code) =>
        Result.Failure<SyncRoutineResult>(error, code);
}

