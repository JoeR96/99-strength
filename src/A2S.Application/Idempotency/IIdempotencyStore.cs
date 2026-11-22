namespace A2S.Application.Idempotency;

/// <summary>
/// The answer a write gave the first time it ran under an <c>Idempotency-Key</c>. A replay with the same
/// key gets this back instead of running again; <see cref="RequestHash"/> catches a key reused for a
/// different request.
/// </summary>
public sealed record IdempotentResponse(string RequestHash, int StatusCode, string? Body);

/// <summary>
/// Stored answers to keyed writes, scoped to a user. Entries older than <see cref="Lifetime"/> are ignored
/// and purged, which comfortably outlives any offline queue on the phone.
/// </summary>
public interface IIdempotencyStore
{
    static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    Task<IdempotentResponse?> FindAsync(string userId, string key, CancellationToken cancellationToken);

    /// <summary>Stores the answer. A concurrent first write with the same key wins; the loser is dropped.</summary>
    Task SaveAsync(string userId, string key, string route, IdempotentResponse response, CancellationToken cancellationToken);
}
