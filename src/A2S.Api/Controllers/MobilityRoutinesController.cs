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

    [HttpPut("{id:guid}/drills")]
    [ProducesResponseType(typeof(MobilityRoutineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceDrills([FromRoute] Guid id, [FromBody] ReplaceDrillsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReplaceDrillsCommand(id, request.Drills), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
