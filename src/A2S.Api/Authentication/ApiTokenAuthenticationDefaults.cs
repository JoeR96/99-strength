using System.Security.Claims;
using A2S.Domain.Aggregates.ApiTokens;

namespace A2S.Api.Authentication;

/// <summary>Scheme names, claim types and header parsing for personal API token authentication.</summary>
public static class ApiTokenAuthenticationDefaults
{
    /// <summary>The scheme that authenticates <c>Authorization: Bearer nns_pat_…</c>.</summary>
    public const string AuthenticationScheme = "ApiToken";

    /// <summary>
    /// The default scheme: looks at the bearer value and forwards to <see cref="AuthenticationScheme"/>
    /// for a personal API token, or to the Clerk JWT bearer scheme for everything else.
    /// </summary>
    public const string SelectorScheme = "ClerkOrApiToken";

    /// <summary>Claim carrying the id of the token a request was authenticated with.</summary>
    public const string TokenIdClaim = "api_token_id";

    /// <summary>Claim carrying the user-chosen name of the token a request was authenticated with.</summary>
    public const string TokenNameClaim = "api_token_name";

    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Reads the bearer value when it is meant as a personal API token (it starts with
    /// <c>nns_pat_</c>), whether or not it turns out to be a valid one.
    /// </summary>
    public static bool TryReadToken(HttpRequest request, out string token)
    {
        token = string.Empty;
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = header[BearerPrefix.Length..].Trim();
        if (!value.StartsWith(ApiToken.TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        token = value;
        return true;
    }

    /// <summary>Whether the request was authenticated with a personal API token rather than a Clerk session.</summary>
    public static bool IsApiToken(this ClaimsPrincipal user) =>
        user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == AuthenticationScheme);
}
