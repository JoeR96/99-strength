using A2S.Api.Authentication;
using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Readiness;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Daily readiness (Green / Yellow / Red, the WHOOP recovery colour) per local date. Suggestions only:
/// it never changes A2S2, RPS, minimal-sets or cardio-track maths. The read can use a personal API token.
/// </summary>
[ApiController]
[Route("api/v1/readiness")]
[Authorize]
public class ReadinessController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReadinessController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The readiness and its suggestions for a date (today when omitted). Colour is null until tapped.</summary>
    [HttpGet]
    [AllowApiToken]
    [ProducesResponseType(typeof(ReadinessDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReadinessQuery(date), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Sets the colour for a date.</summary>
    [HttpPut("{date}")]
    [ProducesResponseType(typeof(ReadinessDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Set([FromRoute] DateOnly date, [FromBody] SetReadinessRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetReadinessCommand(date, request.Colour), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>On a red day, keeps (true) or skips (false) the finisher.</summary>
    [HttpPut("{date}/finisher")]
    [ProducesResponseType(typeof(ReadinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetFinisher([FromRoute] DateOnly date, [FromBody] SetReadinessFinisherRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetReadinessFinisherCommand(date, request.Keep), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
