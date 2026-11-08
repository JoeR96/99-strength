using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Hevy;
using A2S.Application.Hevy;
using A2S.Application.Queries.Hevy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Hevy as the logging source of truth: pull completed workouts and work the inbox of ones the plan
/// could not place. Every Hevy call is server-side with the user's stored key.
/// </summary>
[ApiController]
[Route("api/v1/hevy")]
[Authorize]
public class HevyInboxController : ControllerBase
{
    private readonly IMediator _mediator;

    public HevyInboxController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Pulls workout events since the last pull, stores each workout once and completes the sessions
    /// they match. Without a stored key or an active plan the result says so and nothing happens.
    /// <c>?force=true</c> re-walks from the plan start.
    /// </summary>
    [HttpPost("pull")]
    [ProducesResponseType(typeof(PullSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Pull([FromQuery] bool force, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PullHevyEventsCommand(force), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Pulled workouts no session fitted, newest first, with why.</summary>
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(IReadOnlyList<HevyInboxItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetHevyInboxQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Completes the given session from an inbox workout, as the pull would have (or as an extra session).</summary>
    [HttpPost("inbox/{hevyWorkoutId}/assign")]
    [ProducesResponseType(typeof(AssignHevyWorkoutResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign([FromRoute] string hevyWorkoutId, [FromBody] AssignHevyWorkoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AssignHevyWorkoutCommand(hevyWorkoutId, request.Date, request.Kind, request.LiftDay, request.Extra), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Dismisses an inbox workout.</summary>
    [HttpPost("inbox/{hevyWorkoutId}/ignore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ignore([FromRoute] string hevyWorkoutId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new IgnoreHevyWorkoutCommand(hevyWorkoutId), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }
}
