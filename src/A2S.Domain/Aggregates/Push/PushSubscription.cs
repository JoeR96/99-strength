using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Push;

/// <summary>
/// One browser's Web Push subscription for one user: the push service endpoint plus the client keys
/// the payload is encrypted with. A user has one row per device; re-subscribing from the same
/// endpoint refreshes the keys rather than adding a row. Each subscription carries the local time
/// the "time to train" nudge should arrive (default 17:30), editable from Settings.
/// </summary>
public sealed class PushSubscription : AggregateRoot<PushSubscriptionId>
{
    public const int MaxEndpointLength = 2048;
    public const int MaxKeyLength = 256;
    public const int MaxUserAgentLength = 512;

    public static readonly TimeOnly DefaultSessionStartTime = new(17, 30);

    private PushSubscription(
        PushSubscriptionId id,
        UserId userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        TimeOnly sessionStartTime,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        UserId = userId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        UserAgent = userAgent;
        SessionStartTime = sessionStartTime;
        CreatedAtUtc = createdAtUtc;
    }

    // EF Core constructor
    private PushSubscription()
    {
        Endpoint = string.Empty;
        P256dh = string.Empty;
        Auth = string.Empty;
    }

    public UserId UserId { get; private set; }

    /// <summary>The push service URL the browser handed out; unique per user.</summary>
    public string Endpoint { get; private set; }

    /// <summary>Client public key (base64url) for payload encryption.</summary>
    public string P256dh { get; private set; }

    /// <summary>Client auth secret (base64url) for payload encryption.</summary>
    public string Auth { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>Local (Europe/London) time the session nudge is wanted.</summary>
    public TimeOnly SessionStartTime { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static PushSubscription Create(
        UserId userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        DateTimeOffset createdAt,
        TimeOnly? sessionStartTime = null)
    {
        return new PushSubscription(
            new PushSubscriptionId(Guid.NewGuid()),
            userId,
            CheckEndpoint(endpoint),
            CheckKey(p256dh, nameof(p256dh)),
            CheckKey(auth, nameof(auth)),
            Trim(userAgent, MaxUserAgentLength),
            sessionStartTime ?? DefaultSessionStartTime,
            createdAt.ToUniversalTime());
    }

    /// <summary>The browser rotated its keys (or the app re-subscribed): keep the row, take the new keys.</summary>
    public void RefreshKeys(string p256dh, string auth, string? userAgent)
    {
        P256dh = CheckKey(p256dh, nameof(p256dh));
        Auth = CheckKey(auth, nameof(auth));
        UserAgent = Trim(userAgent, MaxUserAgentLength);
    }

    public void SetSessionStartTime(TimeOnly time) => SessionStartTime = time;

    private static string CheckEndpoint(string endpoint)
    {
        var trimmed = (endpoint ?? string.Empty).Trim();
        CheckRule(trimmed.Length > 0, "A push subscription needs an endpoint.");
        CheckRule(trimmed.Length <= MaxEndpointLength, $"Push endpoint cannot exceed {MaxEndpointLength} characters.");
        CheckRule(
            Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
            "Push endpoint must be an https URL.");
        return trimmed;
    }

    private static string CheckKey(string value, string name)
    {
        var trimmed = (value ?? string.Empty).Trim();
        CheckRule(trimmed.Length > 0, $"A push subscription needs its {name} key.");
        CheckRule(trimmed.Length <= MaxKeyLength, $"Push key {name} cannot exceed {MaxKeyLength} characters.");
        return trimmed;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
