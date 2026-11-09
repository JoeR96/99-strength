using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.SyncRoutineToHevy;
using A2S.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Domain-driven Hevy sync operations.
/// </summary>
[ApiController]
[Route("api/v1/hevy/sync")]
[Authorize]
public class HevySyncController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHevyKeyResolver _keys;

    public HevySyncController(IMediator mediator, IHevyKeyResolver keys)
    {
        _mediator = mediator;
        _keys = keys;
    }

    /// <summary>
    /// Sync a workout day's routine to Hevy using domain-calculated planned sets.
    /// </summary>
    [HttpPost("routine")]
    [ProducesResponseType(typeof(SyncRoutineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncRoutineToHevy(
        [FromBody] SyncRoutineRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(await _keys.ForCurrentUserAsync(cancellationToken)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "Hevy API key is required. Add it under Settings."
            });
        }

        var command = new SyncRoutineToHevyCommand(
            request.WorkoutId, request.WeekNumber, request.DayNumber);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }
}
