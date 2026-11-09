using System.Security.Claims;
using System.Text.Encodings.Web;
using A2S.Application.ApiTokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace A2S.Api.Authentication;

/// <summary>
/// Authenticates <c>Authorization: Bearer nns_pat_…</c>. The principal it builds carries the same
/// <c>sub</c> and email claims a Clerk session for that user would, so user provisioning and
/// <c>ICurrentUserService</c> resolve the same user and no handler needs to know about tokens.
/// The token value is never logged.
/// </summary>
public sealed class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiTokenAuthenticator _authenticator;

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenAuthenticator authenticator)
        : base(options, logger, encoder)
    {
        _authenticator = authenticator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!ApiTokenAuthenticationDefaults.TryReadToken(Request, out var presented))
        {
            return AuthenticateResult.NoResult();
        }

        var owner = await _authenticator.AuthenticateAsync(presented, Context.RequestAborted);
        if (owner is null)
        {
            return AuthenticateResult.Fail("The API token is malformed, unknown or revoked.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, owner.UserId.Value),
            new Claim("sub", owner.UserId.Value),
            new Claim(ClaimTypes.Email, owner.Email),
            new Claim("email", owner.Email),
            new Claim(ApiTokenAuthenticationDefaults.TokenIdClaim, owner.TokenId.Value.ToString()),
            new Claim(ApiTokenAuthenticationDefaults.TokenNameClaim, owner.TokenName)
        };
        var identity = new ClaimsIdentity(claims, ApiTokenAuthenticationDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer error=\"invalid_token\"";
        return Task.CompletedTask;
    }
}
