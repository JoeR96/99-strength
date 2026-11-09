using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Push;
using A2S.Application.DTOs;
using A2S.Application.Push;
using A2S.Application.Queries.Push;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace A2S.Api.Controllers;

/// <summary>
/// Web Push: the server's VAPID public key and the caller's subscriptions. Nudges themselves are sent
/// by the background nudge service, not through this controller.
/// </summary>
[ApiController]
[Route("api/v1/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PushOptions _options;

    public PushController(IMediator mediator, IOptions<PushOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    /// <summary>The VAPID public key browsers subscribe with; <c>null</c> when push is not configured on this server.</summary>
    [HttpGet("vapid-public-key")]
    [ProducesResponseType(typeof(VapidPublicKeyDto), StatusCodes.Status200OK)]
    public IActionResult VapidPublicKey()
    {
        return Ok(new VapidPublicKeyDto(_options.IsConfigured ? _options.VapidPublicKey : null));
    }

    /// <summary>The caller's subscriptions, one per device.</summary>
    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<PushSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListPushSubscriptionsQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Stores or refreshes this browser's subscription (matched on endpoint).</summary>
    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(PushSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribePushRequest request, CancellationToken cancellationToken)
    {
        var command = new SubscribePushCommand(request.Endpoint, request.P256dh, request.Auth, request.UserAgent, request.SessionStartTime);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Forgets this browser's subscription. Unknown endpoints are fine.</summary>
    [HttpDelete("subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribePushRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UnsubscribePushCommand(request.Endpoint), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }
}
