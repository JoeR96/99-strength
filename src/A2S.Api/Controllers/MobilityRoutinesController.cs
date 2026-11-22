using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Mobility;
using A2S.Application.DTOs;
using A2S.Application.Queries.Plans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>Mobility routines: ordered drills with doses, side bias and standing alternatives.</summary>
[ApiController]
[Route("api/v1/mobility-routines")]
[Authorize]
public class MobilityRoutinesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MobilityRoutinesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MobilityRoutineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListMobilityRoutinesQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMobilityRoutineCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : CreatedAtAction(nameof(List), null, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateMobilityRoutineRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateMobilityRoutineCommand(id, request.Name, request.TargetMinutes, request.Kind, request.FollowAlongUrl), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteMobilityRoutineCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    [HttpPut("{id:guid}/drills")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceDrills([FromRoute] Guid id, [FromBody] ReplaceDrillsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReplaceDrillsCommand(id, request.Drills), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>How each drill felt in a finished session; runs the light progression. Idempotent on the session key.</summary>
    [HttpPost("{id:guid}/feedback")]
    [ProducesResponseType(typeof(DrillFeedbackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordFeedback([FromRoute] Guid id, [FromBody] DrillFeedbackRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecordDrillFeedbackCommand(id, request.SessionKey, request.Feedback), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Accepts or declines a drill's "move up a level" suggestion.</summary>
    [HttpPost("{id:guid}/suggestion")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RespondToSuggestion([FromRoute] Guid id, [FromBody] DrillSuggestionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RespondToDrillSuggestionCommand(id, request.DrillName, request.Accept), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Reverts the most recent progression change.</summary>
    [HttpPost("{id:guid}/progression/undo")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UndoProgression([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UndoDrillProgressionCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Phase bump: every timed hold gets longer by the given seconds.</summary>
    [HttpPost("{id:guid}/hold-increase")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApplyHoldIncrease([FromRoute] Guid id, [FromBody] HoldIncreaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApplyHoldIncreaseCommand(id, request.Seconds), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
