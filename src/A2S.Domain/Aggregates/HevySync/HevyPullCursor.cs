using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.HevySync;

/// <summary>
/// Where a user's pull of Hevy workout events got to: the newest event seen (the next <c>since=</c>)
/// and when the last pull ran. One row per user.
/// </summary>
public sealed class HevyPullCursor : AggregateRoot<UserId>
{
    private HevyPullCursor(UserId userId, DateTimeOffset lastEventAtUtc, DateTimeOffset lastPulledAtUtc)
        : base(userId)
    {
        LastEventAtUtc = lastEventAtUtc;
        LastPulledAtUtc = lastPulledAtUtc;
    }

    // EF Core constructor
    private HevyPullCursor()
    {
    }

    public UserId UserId => Id;
    public DateTimeOffset LastEventAtUtc { get; private set; }
    public DateTimeOffset LastPulledAtUtc { get; private set; }

    /// <summary>Starts a cursor at <paramref name="since"/>, so the first pull walks everything after it.</summary>
    public static HevyPullCursor Start(UserId userId, DateTimeOffset since, DateTimeOffset now) =>
        new(userId, since.ToUniversalTime(), now.ToUniversalTime());

    /// <summary>Moves forward to the newest event seen; never backwards. Always stamps the pull time.</summary>
    public void Advance(DateTimeOffset? newestEventAt, DateTimeOffset now)
    {
        if (newestEventAt is { } newest && newest.ToUniversalTime() > LastEventAtUtc)
        {
            LastEventAtUtc = newest.ToUniversalTime();
        }

        LastPulledAtUtc = now.ToUniversalTime();
    }

    /// <summary>Rewinds so the next pull re-walks from <paramref name="since"/> (a forced full pull).</summary>
    public void Rewind(DateTimeOffset since) => LastEventAtUtc = since.ToUniversalTime();
}
