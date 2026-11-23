using System.Security.Cryptography;
using System.Text;
using A2S.Application.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using A2S.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace A2S.Api.Infrastructure;

/// <summary>
/// Conditional GET for a read whose answer only changes when the caller's <see cref="Areas"/> change (or the
/// query string, or the local date). The ETag is <c>"{area versions}-{hash}"</c>, where the hash covers the query
/// string and today's Europe/London date: "today" with no <c>?date=</c> is a different answer after midnight
/// although no data changed. A matching <c>If-None-Match</c> answers 304 without running the action; otherwise
/// the action runs exactly as before and the 200 carries the ETag.
/// </summary>
/// <remarks>
/// The tag is computed before the action, so a write landing in between gives a newer body under the older tag;
/// the next request then sees a different tag and refetches, so a client is never stuck on stale data.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SyncETagAttribute : Attribute, IFilterFactory
{
    public SyncETagAttribute(SyncArea areas)
    {
        Areas = areas;
    }

    public SyncArea Areas { get; }

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new SyncETagFilter(
        Areas,
        serviceProvider.GetRequiredService<IUserChangeLog>(),
        serviceProvider.GetRequiredService<ICurrentUserService>(),
        serviceProvider.GetRequiredService<ILocalClock>());
}

internal sealed class SyncETagFilter : IAsyncActionFilter
{
    private readonly SyncArea _areas;
    private readonly IUserChangeLog _log;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SyncETagFilter(SyncArea areas, IUserChangeLog log, ICurrentUserService currentUser, ILocalClock clock)
    {
        _areas = areas;
        _log = log;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            await next();
            return;
        }

        var http = context.HttpContext;
        var stamp = await _log.GetAsync(userId, http.RequestAborted);
        var versions = string.Join(".", SyncAreas.Each.Where(e => _areas.HasFlag(e.Area)).Select(e => stamp.Of(e.Area)));
        var hashInput = $"{http.Request.QueryString.Value}|{_clock.Today():yyyy-MM-dd}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)))[..12].ToLowerInvariant();
        var etag = $"\"{versions}-{hash}\"";

        if (SyncETags.Matches(http.Request, etag))
        {
            SyncETags.Write(http.Response, etag);
            context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            return;
        }

        var executed = await next();
        if (executed.Exception is null && executed.Result is ObjectResult { StatusCode: null or 200 } or OkObjectResult)
        {
            SyncETags.Write(http.Response, etag);
        }
    }
}

/// <summary>ETag helpers shared by the filter and <c>GET /sync/version</c>.</summary>
public static class SyncETags
{
    /// <summary>Whether any tag in <c>If-None-Match</c> equals <paramref name="etag"/> (weak or strong; <c>*</c> matches).</summary>
    public static bool Matches(HttpRequest request, string etag)
    {
        var header = request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        foreach (var raw in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = raw.StartsWith("W/", StringComparison.Ordinal) ? raw[2..] : raw;
            if (tag == "*" || tag == etag)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The tag, and <c>Cache-Control: private, no-cache</c> so a cache always revalidates (per user).</summary>
    public static void Write(HttpResponse response, string etag)
    {
        response.Headers[HeaderNames.ETag] = etag;
        response.Headers[HeaderNames.CacheControl] = "private, no-cache";
    }
}
