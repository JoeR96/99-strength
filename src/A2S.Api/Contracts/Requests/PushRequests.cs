namespace A2S.Api.Contracts.Requests;

/// <summary>
/// Request body for <c>POST /api/v1/push/subscribe</c>: the browser's <c>PushSubscription.toJSON()</c>
/// flattened, plus the local time the "time to train" nudge is wanted (default 17:30).
/// </summary>
public sealed record SubscribePushRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent,
    TimeOnly? SessionStartTime);

/// <summary>Request body for <c>DELETE /api/v1/push/subscribe</c>.</summary>
public sealed record UnsubscribePushRequest(string Endpoint);
