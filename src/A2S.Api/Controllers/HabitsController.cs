using A2S.Api.Authentication;
using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Habits;
using A2S.Application.DTOs;
using A2S.Application.Queries.Habits;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Daily habits: definitions, per-day logs and the streak. Dates are local (Europe/London) <c>yyyy-MM-dd</c>.
/// Every action here can also be called with a personal API token (the desktop habit widget).
/// </summary>
[ApiController]
[Route("api/v1/habits")]
[Authorize]
[AllowApiToken]
public class HabitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HabitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists the current user's habits in display order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HabitDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetHabitsQuery(includeArchived), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Creates a habit.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(HabitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateHabitRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateHabitCommand(
            request.Name, request.Type, request.Target, request.Minimum, request.Unit, request.Order, request.Icon);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Created($"/api/v1/habits/{result.Value.Id}", result.Value);
    }

    /// <summary>Updates a habit's name, targets, unit, order and icon.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(HabitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateHabitRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateHabitCommand(id, request.Name, request.Target, request.Minimum, request.Unit, request.Order, request.Icon);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Hides a habit from the daily list and the streak; history is kept.</summary>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ArchiveHabitCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>Brings an archived habit back.</summary>
    [HttpPost("{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unarchive([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UnarchiveHabitCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>The habit screen for a date (today when omitted): values, minimum/target flags and the streak.</summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(HabitsTodayDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Today([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetHabitsTodayQuery(date), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Per-habit, per-day values between two dates (inclusive).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(HabitHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> History([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetHabitHistoryQuery(from, to), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Sets a day's value. An older <c>clientTimestamp</c> than the stored one is ignored and the stored log returned.</summary>
    [HttpPut("{id:guid}/logs/{date}")]
    [ProducesResponseType(typeof(HabitLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetLog(
        [FromRoute] Guid id,
        [FromRoute] DateOnly date,
        [FromBody] SetHabitLogRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertHabitLogCommand(id, date, request.Value, request.ClientTimestamp), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>The +1 / -1 tap for a day. Creates the log when there is none; never goes below 0.</summary>
    [HttpPost("{id:guid}/logs/{date}/increment")]
    [ProducesResponseType(typeof(HabitLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Increment(
        [FromRoute] Guid id,
        [FromRoute] DateOnly date,
        [FromBody] IncrementHabitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new IncrementHabitCommand(id, date, request.Delta, request.ClientTimestamp), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Imports habits and history from another system. Safe to repeat: matched by external key, logs written only when changed.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import([FromBody] ImportHabitsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ImportHabitsCommand(request.Habits), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
