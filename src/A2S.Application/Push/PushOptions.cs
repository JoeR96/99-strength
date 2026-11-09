namespace A2S.Application.Push;

/// <summary>
/// VAPID settings for Web Push, bound from the <c>Push</c> configuration section. The keys come only
/// from configuration: user-secrets locally (<c>dotnet user-secrets set "Push:VapidPublicKey" …</c>)
/// and the Dokploy environment in production. With no keys the nudge service stays idle and the
/// subscribe endpoints report push as unavailable.
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Base64url-encoded uncompressed P-256 public key (65 bytes → 87 characters).</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>Base64url-encoded P-256 private scalar (32 bytes → 43 characters).</summary>
    public string? VapidPrivateKey { get; set; }

    /// <summary>A <c>mailto:</c> or https URL push services can contact about abuse.</summary>
    public string? Subject { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VapidPublicKey)
        && !string.IsNullOrWhiteSpace(VapidPrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
