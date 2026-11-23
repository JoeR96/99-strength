using A2S.Domain.Aggregates.Sync;

namespace A2S.Application.Sync;

/// <summary>
/// Marks a command as a write to one or more sync areas: <c>[ChangesSyncArea(SyncArea.Habits)]</c>. After the
/// command succeeds (its <c>Result</c> is a success, or it returns something else without throwing),
/// <see cref="Behaviors.ChangeVersionBehavior{TRequest,TResponse}"/> bumps the user's change stamp for these areas
/// and tells connected clients. An attribute rather than an interface member so it stays out of the command
/// records' JSON and equality; <c>ChangeVersionCoverageTests</c> pins which commands carry it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ChangesSyncAreaAttribute : Attribute
{
    public ChangesSyncAreaAttribute(SyncArea areas)
    {
        Areas = areas;
    }

    public SyncArea Areas { get; }
}

/// <summary>Per-user change stamps (the <c>UserChangeLogs</c> table).</summary>
public interface IUserChangeLog
{
    /// <summary>The user's stamp; <see cref="ChangeStamp.Empty"/> before their first write.</summary>
    Task<ChangeStamp> GetAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Bumps the stamp atomically (concurrent writes each get their own version) and returns the new one.
    /// Same rule as <see cref="ChangeStamp.Bump"/>.
    /// </summary>
    Task<ChangeStamp> BumpAsync(string userId, SyncArea areas, CancellationToken ct);
}

/// <summary>One bump as a connected client hears it.</summary>
public sealed record SyncChange(long Version, SyncArea Areas);

/// <summary>A connected client's feed of its user's changes. Dispose to unsubscribe.</summary>
public interface ISyncSubscription : IDisposable
{
    IAsyncEnumerable<SyncChange> ReadAllAsync(CancellationToken ct);

    /// <summary>Waits up to <paramref name="timeout"/> for the next change; null when none came.</summary>
    Task<SyncChange?> WaitAsync(TimeSpan timeout, CancellationToken ct);
}

/// <summary>
/// Fan-out of change stamps to connected clients (Server-Sent Events), keyed by user. The shipped
/// implementation is in-process, which is right for the single API instance; see docs/development.md for
/// the Postgres LISTEN/NOTIFY upgrade when there is more than one.
/// </summary>
public interface ISyncNotifier
{
    ISyncSubscription Subscribe(string userId);

    void Publish(string userId, SyncChange change);
}

/// <summary>The answer of <c>GET /api/v1/sync/version</c>: <c>{ "version": 123, "areas": { "habits": 40, … } }</c>.</summary>
public sealed record SyncVersionDto(long Version, IReadOnlyDictionary<string, long> Areas)
{
    public static SyncVersionDto From(ChangeStamp stamp) => new(stamp.Version, stamp.Areas());
}
