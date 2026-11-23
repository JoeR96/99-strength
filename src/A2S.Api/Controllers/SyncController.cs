using System.Text;
using System.Text.Json;
using A2S.Api.Authentication;
using A2S.Api.Extensions;
using A2S.Api.Infrastructure;
using A2S.Application.Common;
using A2S.Application.Sync;
using A2S.Domain.Aggregates.Sync;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace A2S.Api.Controllers;

/// <summary>Settings for the change stream.</summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Seconds between <c>:keepalive</c> comments on an idle stream (default 25, under most proxies' 30–60 s idle limit).</summary>
    public int KeepAliveSeconds { get; set; } = 25;
}

/// <summary>
/// Keeping clients (Terran CC, the web app) in sync: the caller's change stamp, and a Server-Sent Events stream
/// that says when it changes. Both work with a Clerk session or a personal API token.
/// </summary>
[ApiController]
[Route("api/v1/sync")]
[Authorize]
[AllowApiToken]
public class SyncController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMediator _mediator;

    public SyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// <c>{ "version": 123, "areas": { "habits": 40, "plan": 12, "workout": 70, "cardio": 3, "mobility": 5 } }</c>,
    /// with <c>ETag: "123"</c>; <c>If-None-Match: "123"</c> answers 304 while nothing changed.
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(typeof(SyncVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetVersion(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSyncVersionQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        var etag = $"\"{result.Value.Version}\"";
        SyncETags.Write(Response, etag);
        return SyncETags.Matches(Request, etag) ? StatusCode(StatusCodes.Status304NotModified) : Ok(result.Value);
    }

    /// <summary>
    /// <c>text/event-stream</c> that stays open. On connect: <c>event: hello</c> with the current version (and, when
    /// <c>Last-Event-ID</c> is older, a <c>changed</c> event for what was missed). Then <c>event: changed</c>,
    /// <c>data: {"version":124,"areas":["habits"]}</c> on every bump, and a <c>:keepalive</c> comment every 25 s.
    /// Each event's <c>id</c> is its version.
    /// </summary>
    [HttpGet("events")]
    [Produces("text/event-stream")]
    public async Task Events(
        [FromServices] ISyncNotifier notifier,
        [FromServices] IUserChangeLog changeLog,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IOptions<SyncOptions> options,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Unbuffered and uncached all the way out: no response buffering here, and proxies (nginx, Traefik) told not to.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers[HeaderNames.CacheControl] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        // Subscribe before reading the stamp, so a bump in between is not lost (at worst it is sent twice).
        using var subscription = notifier.Subscribe(userId);
        var stamp = await changeLog.GetAsync(userId, cancellationToken);
        var sent = stamp.Version;

        try
        {
            await Response.WriteAsync("retry: 5000\n\n", cancellationToken);
            await WriteEventAsync("hello", stamp.Version, new { version = stamp.Version, areas = stamp.Areas() }, cancellationToken);

            if (LastEventId() is { } seen && seen < stamp.Version)
            {
                await WriteEventAsync("changed", stamp.Version,
                    new { version = stamp.Version, areas = SyncAreas.Names(stamp.ChangedSince(seen)) }, cancellationToken);
            }

            var keepAlive = TimeSpan.FromSeconds(Math.Max(1, options.Value.KeepAliveSeconds));
            while (!cancellationToken.IsCancellationRequested)
            {
                var change = await subscription.WaitAsync(keepAlive, cancellationToken);
                if (change is null)
                {
                    await Response.WriteAsync(":keepalive\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    continue;
                }

                if (change.Version <= sent)
                {
                    continue;
                }

                sent = change.Version;
                await WriteEventAsync("changed", change.Version,
                    new { version = change.Version, areas = SyncAreas.Names(change.Areas) }, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away: nothing to answer.
        }
    }

    private long? LastEventId()
    {
        var header = Request.Headers["Last-Event-ID"].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            header = Request.Query["lastEventId"].ToString();
        }

        return long.TryParse(header, out var id) && id >= 0 ? id : null;
    }

    private async Task WriteEventAsync(string name, long id, object data, CancellationToken ct)
    {
        var text = new StringBuilder()
            .Append("id: ").Append(id).Append('\n')
            .Append("event: ").Append(name).Append('\n')
            .Append("data: ").Append(JsonSerializer.Serialize(data, Json)).Append("\n\n")
            .ToString();
        await Response.WriteAsync(text, ct);
        await Response.Body.FlushAsync(ct);
    }
}
