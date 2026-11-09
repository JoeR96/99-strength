using A2S.Domain.Common;

namespace A2S.Application.Interfaces;

/// <summary>
/// Where the body-weight pace reading gets its target line from: the active training plan's start weight
/// and checkpoints. Null when the user has no active plan, in which case no pace is reported.
/// </summary>
public interface IPlanCheckpointSource
{
    Task<PlanCheckpoints?> ForUserAsync(UserId userId, CancellationToken ct = default);
}

/// <summary>The start of the line and the checkpoints after it, in any order.</summary>
public sealed record PlanCheckpoints(DateOnly StartDate, decimal StartKg, IReadOnlyList<(DateOnly Date, decimal Kg)> Checkpoints);
