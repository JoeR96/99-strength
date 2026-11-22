using A2S.Api.Extensions;
using A2S.Application.Commands.GymSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Gym mode (brief § 3a): finish a session logged on the phone (completes it in the app and posts it to
/// Hevy from the server), and retry a post that has not reached Hevy yet.
/// </summary>
[ApiController]
[Route("api/v1/gym-sessions")]
[Authorize]
public class GymSessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GymSessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Finish a gym session. Idempotent on <c>clientSessionId</c>: sending the same session again (an offline
    /// replay) returns the first result with <c>replayed: true</c> and completes nothing twice.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FinishGymSessionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Finish([FromBody] FinishGymSessionCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Try the session's Hevy post again now ("Not in Hevy yet" → Retry).</summary>
    [HttpPost("{clientSessionId:guid}/hevy-retry")]
    [ProducesResponseType(typeof(HevyPostStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryHevy(Guid clientSessionId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RetryHevyPostCommand(clientSessionId), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
