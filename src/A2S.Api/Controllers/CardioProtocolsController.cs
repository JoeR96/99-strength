using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Cardio;
using A2S.Application.DTOs;
using A2S.Application.Queries.Plans;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>Cardio protocols: Norwegian 4×4, Couch to 5K weeks and LISS blocks.</summary>
[ApiController]
[Route("api/v1/cardio-protocols")]
[Authorize]
public class CardioProtocolsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CardioProtocolsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CardioProtocolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCardioProtocolsQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CardioProtocolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCardioProtocolCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : CreatedAtAction(nameof(List), null, result.Value);
    }

    /// <summary>Moves a Couch to 5K protocol to its next week (honouring a pending repeat; capped at week 9).</summary>
    [HttpPost("{id:guid}/advance")]
    [ProducesResponseType(typeof(CardioProtocolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Advance([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AdvanceCouchToFiveKCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpPut("{id:guid}/repeat-week")]
    [ProducesResponseType(typeof(CardioProtocolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRepeatWeek([FromRoute] Guid id, [FromBody] SetRepeatWeekRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetRepeatWeekCommand(id, request.Repeat), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
