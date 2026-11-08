using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Web Push subscriptions per user, and the log of nudges already sent.</summary>
public interface IPushSubscriptionRepository : IRepository<PushSubscription, PushSubscriptionId>
{
    Task<IReadOnlyList<PushSubscription>> ListForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>The user's subscription for one endpoint, or null.</summary>
    Task<PushSubscription?> GetByEndpointAsync(UserId userId, string endpoint, CancellationToken ct = default);

    /// <summary>Every user with at least one subscription (the nudge service's work list).</summary>
    Task<IReadOnlyList<UserId>> ListSubscribedUsersAsync(CancellationToken ct = default);

    Task<bool> HasNudgeAsync(UserId userId, DateOnly localDate, PushNudgeKind kind, CancellationToken ct = default);

    Task AddNudgeAsync(PushNudgeLog log, CancellationToken ct = default);
}
