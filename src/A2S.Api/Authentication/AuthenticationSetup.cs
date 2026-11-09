using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace A2S.Api.Authentication;

/// <summary>
/// Wires the API's two ways of proving who is calling: a Clerk session JWT (the web app) and a
/// personal API token (programs that cannot hold a session). Both arrive as a bearer value; a
/// selector scheme forwards <c>nns_pat_…</c> values to the token scheme and the rest to Clerk.
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddA2SAuthentication(
        this IServiceCollection services,
        string clerkDomain,
        bool requireHttpsMetadata)
    {
        services
            .AddAuthentication(ApiTokenAuthenticationDefaults.SelectorScheme)
            .AddPolicyScheme(ApiTokenAuthenticationDefaults.SelectorScheme, displayName: null, options =>
            {
                options.ForwardDefaultSelector = context =>
                    ApiTokenAuthenticationDefaults.TryReadToken(context.Request, out _)
                        ? ApiTokenAuthenticationDefaults.AuthenticationScheme
                        : JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = clerkDomain;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = clerkDomain,
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.RequireHttpsMetadata = requireHttpsMetadata;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
                ApiTokenAuthenticationDefaults.AuthenticationScheme,
                configureOptions: null);

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Enforces the personal API token allow-list. Place it between <c>UseAuthentication</c> and
    /// <c>UseAuthorization</c>.
    /// </summary>
    public static IApplicationBuilder UseApiTokenScope(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiTokenScopeMiddleware>();
}
