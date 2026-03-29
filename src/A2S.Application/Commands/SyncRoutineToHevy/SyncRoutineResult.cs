namespace A2S.Application.Commands.SyncRoutineToHevy;

/// <summary>
/// Result of syncing a routine to Hevy.
/// </summary>
public sealed record SyncRoutineResult
{
    public required bool Success { get; init; }
    public required string? RoutineId { get; init; }
    public required string? RoutineTitle { get; init; }
    public required string? Message { get; init; }
    public required bool AlreadyExists { get; init; }
}
