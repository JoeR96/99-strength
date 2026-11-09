using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.Hevy;

/// <summary>
/// The user's pushed routines by local key, loaded once per request so the Today and calendar views
/// can say which sessions already have a Hevy routine. Uses the same <see cref="HevyLocalKeys"/>
/// scheme as <see cref="HevyRoutinePusher"/>, so a session resolves to exactly the routine its push left.
/// </summary>
public sealed class HevyRoutineLookup
{
    public static readonly HevyRoutineLookup Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, string> _byLocalKey;

    private HevyRoutineLookup(IReadOnlyDictionary<string, string> byLocalKey)
    {
        _byLocalKey = byLocalKey;
    }

    public static async Task<HevyRoutineLookup> LoadAsync(IHevyLinkRepository links, UserId userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(links);
        var routines = await links.ListAsync(userId, HevyLinkKind.Routine, ct);
        if (routines.Count == 0)
        {
            return Empty;
        }

        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var link in routines)
        {
            byKey[link.LocalKey] = link.HevyId;
        }

        return new HevyRoutineLookup(byKey);
    }

    /// <summary>The Hevy routine id pushed for this session on this date, or null when it was never pushed.</summary>
    public string? For(TrainingPlanId planId, DateOnly date, ScheduledSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Kind == SessionKind.Rest || _byLocalKey.Count == 0)
        {
            return null;
        }

        return _byLocalKey.TryGetValue(HevyLocalKeys.Routine(planId, date, session), out var id) ? id : null;
    }
}
