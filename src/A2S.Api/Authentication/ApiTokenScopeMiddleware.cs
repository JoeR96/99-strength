using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Authentication;

/// <summary>
/// Keeps personal API tokens inside their allow-list. A request that presents a token gets through
/// only when the token is valid and the endpoint is marked <see cref="AllowApiTokenAttribute"/>:
/// a bad token is answered 401, a good token anywhere else 403. Requests without a token (Clerk
/// sessions, anonymous calls) pass straight through to normal authorization.
/// Runs after authentication and before authorization, so the rule holds whatever policy an
/// endpoint uses, including none.
/// </summary>
public sealed class ApiTokenScopeMiddleware
{
    private readonly RequestDelegate _next;

    public ApiTokenScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ApiTokenAuthenticationDefaults.TryReadToken(context.Request, out _))
        {
            await _next(context);
            return;
        }

        if (!context.User.IsApiToken())
        {
            await context.ChallengeAsync(ApiTokenAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (context.GetEndpoint()?.Metadata.GetMetadata<AllowApiTokenAttribute>() is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "This endpoint cannot be called with a personal API token.",
                    Instance = context.Request.Path
                },
                options: null,
                contentType: "application/problem+json");
            return;
        }

        await _next(context);
    }
}
