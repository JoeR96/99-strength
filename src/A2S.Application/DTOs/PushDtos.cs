using A2S.Domain.Aggregates.Push;

namespace A2S.Application.DTOs;

/// <summary>A stored push subscription as the Settings screen sees it (keys are never returned).</summary>
public sealed record PushSubscriptionDto(
    Guid Id,
    string Endpoint,
    string? UserAgent,
    TimeOnly SessionStartTime,
    DateTimeOffset CreatedAtUtc)
{
    public static PushSubscriptionDto From(PushSubscription subscription) => new(
        subscription.Id.Value,
        subscription.Endpoint,
        subscription.UserAgent,
        subscription.SessionStartTime,
        subscription.CreatedAtUtc);
}

/// <summary>The server's VAPID public key, or null when push is not configured.</summary>
public sealed record VapidPublicKeyDto(string? PublicKey);
