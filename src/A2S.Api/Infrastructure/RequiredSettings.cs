namespace A2S.Api.Infrastructure;

/// <summary>
/// Settings the API cannot run without. Read once at startup so a deployment that forgot one
/// fails to boot with a clear message, instead of starting "healthy" and refusing every request.
/// appsettings.json carries empty placeholders for both, so "present but empty" counts as missing.
/// </summary>
public static class RequiredSettings
{
    /// <summary>The Clerk frontend API origin that issues session JWTs (<c>Clerk:Domain</c>).</summary>
    public static string ClerkDomain(IConfiguration configuration)
    {
        var value = configuration["Clerk:Domain"]?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("Clerk:Domain configuration is required. Set 'Clerk:Domain' in appsettings or the Clerk__Domain environment variable.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException($"Clerk:Domain must be an http(s) URL such as https://your-instance.clerk.accounts.dev, but was '{value}'.");
        }

        // Clerk's issuer claim has no trailing slash; one in the setting would fail every token.
        return value.TrimEnd('/');
    }

    /// <summary>The web origins allowed to call the API from a browser (<c>Cors:Origins</c>).</summary>
    public static string[] CorsOrigins(IConfiguration configuration)
    {
        var origins = (configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:Origins configuration is required. Set 'Cors:Origins' in appsettings or the Cors__Origins__0 environment variable.");
        }

        return origins;
    }
}
