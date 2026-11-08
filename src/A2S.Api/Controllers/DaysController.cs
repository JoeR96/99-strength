using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.UndoCompletion;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetDayBudget;
using A2S.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Day completion and undo operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class DaysController : ControllerBase
{
    private readonly IMediator _mediator;

    public DaysController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Completes a training day with exercise performance data.
    /// </summary>
    [HttpPost("{id:guid}/days/{day:int}/complete")]
    [ProducesResponseType(typeof(CompleteDayResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteDay(
        [FromRoute] Guid id,
        [FromRoute] int day,
        [FromBody] CompleteDayRequest request,
        CancellationToken cancellationToken)
    {
        if (day < 1 || day > 6)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "Day must be between 1 and 6."
            });
        }

        var command = new CompleteDayCommand(id, (DayNumber)day, request.Performances);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Weekly hard sets per muscle group, cap warnings and the session time estimate for a day.
    /// Defaults to the workout's current week.
    /// </summary>
    [HttpGet("{id:guid}/days/{day:int}/budget")]
    [ProducesResponseType(typeof(DayBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDayBudget(
        [FromRoute] Guid id,
        [FromRoute] int day,
        [FromQuery] int? week,
        CancellationToken cancellationToken)
    {
        if (day < 1 || day > 6)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "Day must be between 1 and 6."
            });
        }

        var result = await _mediator.Send(new GetDayBudgetQuery(id, (DayNumber)day, week), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Undoes the last completed workout day, restoring progression state.
    /// </summary>
    [HttpPost("{id:guid}/undo-last-completion")]
    [ProducesResponseType(typeof(UndoCompletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UndoLastCompletion(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UndoCompletionCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }
}
