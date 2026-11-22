using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.CardioTracks;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.CardioTracks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>Cardio progression tracks: start from a template, log sessions, accept suggestions, undo, benchmarks.</summary>
[ApiController]
[Route("api/v1/cardio-tracks")]
[Authorize]
public class CardioTracksController : ControllerBase
{
    private readonly IMediator _mediator;

    public CardioTracksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CardioTrackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new ListCardioTracksQuery(), cancellationToken));

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<CardioTrackTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Templates(CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new ListCardioTrackTemplatesQuery(), cancellationToken));

    /// <summary>Prescriptions for active tracks, this week's zone 4–5 minutes and guardrail warnings.</summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(TodayCardioDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Today(CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new GetTodayCardioQuery(), cancellationToken));

    [HttpGet("heart-rate-zones")]
    [ProducesResponseType(typeof(HeartRateZonesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones(CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new GetHeartRateZonesQuery(), cancellationToken));

    [HttpPut("heart-rate-zones")]
    [ProducesResponseType(typeof(HeartRateZonesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetZones([FromBody] SetHeartRateZonesRequest request, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new SetHeartRateZonesCommand(request.MaxHeartRate, request.LowerPercents), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CardioTrackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new GetCardioTrackQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCardioTrackCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCardioTrackRequest request, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new UpdateCardioTrackCommand(id, request.Name, request.Notes, request.Stages, request.Benchmark), cancellationToken));

    [HttpPost("{id:guid}/sessions")]
    [ProducesResponseType(typeof(TrackEvaluationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LogSession([FromRoute] Guid id, [FromBody] LogCardioTrackSessionRequest request, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(
            new LogCardioTrackSessionCommand(id, request.Log, request.CompletedAsPrescribed, request.Date, request.ClientKey), cancellationToken));

    [HttpPost("{id:guid}/suggestion")]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decide([FromRoute] Guid id, [FromBody] DecideSuggestionRequest request, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new DecideCardioTrackSuggestionCommand(id, request.Accept), cancellationToken));

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Undo([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new UndoCardioTrackCommand(id), cancellationToken));

    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new SetCardioTrackPausedCommand(id, true), cancellationToken));

    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(typeof(CardioTrackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new SetCardioTrackPausedCommand(id, false), cancellationToken));

    [HttpPost("{id:guid}/benchmarks")]
    [ProducesResponseType(typeof(BenchmarkResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordBenchmark([FromRoute] Guid id, [FromBody] RecordBenchmarkRequest request, CancellationToken cancellationToken) =>
        Respond(await _mediator.Send(new RecordCardioBenchmarkCommand(id, request.Value, request.Date, request.Notes), cancellationToken));

    private IActionResult Respond<T>(Result<T> result) =>
        result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
}
