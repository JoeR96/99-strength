using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Push;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// The live finisher wizard's phase-change notifications. The phone posts the whole schedule (absolute
/// UTC times) on Start and on every pause, resume, skip or ±30 s; finishing or abandoning deletes it.
/// The finisher nudge loop sends due ones over Web Push.
/// </summary>
[ApiController]
[Route("api/v1/finisher-sessions/{clientSessionId}/schedule")]
[Authorize]
public class FinisherSessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinisherSessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Stores or replaces the schedule for this gym session.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FinisherScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Replace(string clientSessionId, [FromBody] ReplaceFinisherScheduleRequest request, CancellationToken cancellationToken)
    {
        var nudges = (request.Nudges ?? []).Select(n => new FinisherNudgeInput(n.At, n.Body)).ToList();
        var result = await _mediator.Send(new ReplaceFinisherScheduleCommand(clientSessionId, request.Day, request.Title, nudges), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Forgets the schedule (finish or abandon). Unknown sessions are fine.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string clientSessionId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteFinisherScheduleCommand(clientSessionId), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }
}
