using A2S.Application.Common;
using MediatR;

namespace A2S.Application.Commands.SyncRoutineToHevy;

/// <summary>
/// Command to sync a workout day's routine to Hevy.
/// Uses the domain's calculated planned sets.
/// </summary>
public sealed record SyncRoutineToHevyCommand(
    Guid WorkoutId,
    int WeekNumber,
    int DayNumber,
    string HevyApiKey) : IRequest<Result<SyncRoutineResult>>;

/// <summary>
/// Result of syncing a routine to Hevy.
/// </summary>
public sealed record SyncRoutineResult
{
    public bool Success { get; init; }
    public string? RoutineId { get; init; }
    public string? RoutineTitle { get; init; }
    public string? Message { get; init; }
    public bool AlreadyExists { get; init; }
}
