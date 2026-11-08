namespace A2S.Application.Push;

/// <summary>Where a notification goes: the browser's endpoint and the keys to encrypt for it.</summary>
public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

/// <summary>What the service worker shows. <paramref name="Tag"/> collapses repeats of the same nudge.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string Tag);

public enum PushSendStatus
{
    Sent = 0,

    /// <summary>404 / 410 from the push service: the subscription is dead and should be removed.</summary>
    Gone = 1,

    /// <summary>Any other failure; the nudge is still logged so it is not retried every minute.</summary>
    Failed = 2
}

/// <summary>
/// Sends one Web Push message. The production implementation wraps <c>Lib.Net.Http.WebPush</c> with
/// VAPID authentication; tests substitute it.
/// </summary>
public interface IPushSender
{
    Task<PushSendStatus> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default);
}
