using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.BodyWeight;
using A2S.Application.DTOs;
using A2S.Application.Queries.BodyWeight;
using A2S.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Morning weigh-ins and the smoothed trend against the plan's checkpoints. Dates are local <c>yyyy-MM-dd</c>.
/// </summary>
[ApiController]
[Route("api/v1/body-weight")]
[Authorize]
public class BodyWeightController : ControllerBase
{
    private const int DefaultTrendDays = 90;

    private readonly IMediator _mediator;
    private readonly ILocalClock _clock;

    public BodyWeightController(IMediator mediator, ILocalClock clock)
    {
        _mediator = mediator;
        _clock = clock;
    }

    /// <summary>Records or corrects the weigh-in for a date.</summary>
    [HttpPut("{date}")]
    [ProducesResponseType(typeof(BodyWeightEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Record([FromRoute] DateOnly date, [FromBody] RecordBodyWeightRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecordBodyWeightCommand(date, request.Kg), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>The trend between two dates (the last 90 days when omitted) and today's pace when a plan has checkpoints.</summary>
    [HttpGet("trend")]
    [ProducesResponseType(typeof(WeightTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Trend([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var end = to ?? _clock.Today();
        var start = from ?? end.AddDays(-DefaultTrendDays);
        var result = await _mediator.Send(new GetWeightTrendQuery(start, end), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
