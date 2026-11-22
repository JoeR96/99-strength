using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace A2S.Api.Infrastructure;

/// <summary>
/// Makes a write safe to send twice. When the request carries an <c>Idempotency-Key</c> header (the phone's
/// offline queue sends its op id), the first successful (2xx) answer is stored for the user and key; a
/// replay with the same key and the same request gets that answer back without the action running again,
/// and the same key on a different request answers 422. Requests without the header run as before.
/// </summary>
/// <remarks>
/// An MVC filter rather than a MediatR behaviour: the key is an HTTP header and the thing replayed is the
/// HTTP answer (status + body), so it belongs in the presentation layer and needs no command changes.
/// Failures are not stored, so a request that failed (or hit a transient error) can be retried with its key.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public const string HeaderName = "Idempotency-Key";

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new IdempotencyFilter(
        serviceProvider.GetRequiredService<IIdempotencyStore>(),
        serviceProvider.GetRequiredService<ICurrentUserService>(),
        serviceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
}

internal sealed class IdempotencyFilter : IAsyncActionFilter
{
    private const int MaxKeyLength = 128;

    private readonly IIdempotencyStore _store;
    private readonly ICurrentUserService _currentUser;
    private readonly JsonSerializerOptions _json;

    public IdempotencyFilter(IIdempotencyStore store, ICurrentUserService currentUser, JsonSerializerOptions json)
    {
        _store = store;
        _currentUser = currentUser;
        _json = json;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[IdempotentAttribute.HeaderName].ToString().Trim();
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(userId))
        {
            await next();
            return;
        }

        if (key.Length > MaxKeyLength)
        {
            context.Result = Problem(StatusCodes.Status400BadRequest, "Invalid Idempotency-Key", $"The key is at most {MaxKeyLength} characters.");
            return;
        }

        var ct = context.HttpContext.RequestAborted;
        var route = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        var hash = Hash(route, context.ActionArguments);

        var stored = await _store.FindAsync(userId, key, ct);
        if (stored is not null)
        {
            context.Result = stored.RequestHash == hash
                ? new ContentResult { StatusCode = stored.StatusCode, Content = stored.Body, ContentType = stored.Body is null ? null : "application/json; charset=utf-8" }
                : Problem(StatusCodes.Status422UnprocessableEntity, "Idempotency-Key reused",
                    "This Idempotency-Key was already used for a different request.");
            return;
        }

        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        var (status, body) = executed.Result switch
        {
            ObjectResult o => (o.StatusCode ?? StatusCodes.Status200OK, o.Value is null ? null : JsonSerializer.Serialize(o.Value, o.Value.GetType(), _json)),
            StatusCodeResult s => (s.StatusCode, (string?)null),
            _ => (0, null)
        };

        if (status is >= 200 and < 300)
        {
            await _store.SaveAsync(userId, key, route, new IdempotentResponse(hash, status, body), ct);
        }
    }

    private string Hash(string route, IDictionary<string, object?> arguments)
    {
        var parts = arguments
            .Where(a => a.Value is not CancellationToken)
            .OrderBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => $"{a.Key}={(a.Value is null ? "null" : JsonSerializer.Serialize(a.Value, a.Value.GetType(), _json))}");
        var text = route + "\n" + string.Join("\n", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
