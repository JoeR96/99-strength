using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.Commands.UpdateBlockSequence;
using A2S.Application.Queries.GetWeekPlan;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Week progression and plan query operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class WeeksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeeksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Progresses the workout to the next week.
    /// </summary>
    [HttpPost("{id:guid}/progress-week")]
    [ProducesResponseType(typeof(ProgressWeekResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProgressToNextWeek(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ProgressWeekCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates the block sequence for a workout.
    /// </summary>
    [HttpPut("{id:guid}/block-sequence")]
    [ProducesResponseType(typeof(UpdateBlockSequenceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBlockSequence(
        [FromRoute] Guid id,
        [FromBody] UpdateBlockSequenceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBlockSequenceCommand(id, request.BlockSequence);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the planned workout for a specific week and day.
    /// </summary>
    [HttpGet("weeks/{week:int}/days/{day:int}/plan")]
    [ProducesResponseType(typeof(WeekPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeekPlan(
        [FromQuery] Guid? id,
        [FromRoute] int week,
        [FromRoute] int day,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeekPlanQuery(id, week, day), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the planned workout for a specific workout, week, and day.
    /// </summary>
    [HttpGet("{id:guid}/weeks/{week:int}/days/{day:int}/plan")]
    [ProducesResponseType(typeof(WeekPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeekPlanById(
        [FromRoute] Guid id,
        [FromRoute] int week,
        [FromRoute] int day,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeekPlanQuery(id, week, day), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }
}
