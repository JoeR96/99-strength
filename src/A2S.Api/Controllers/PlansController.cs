using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Plans;
using A2S.Application.DTOs;
using A2S.Application.Queries.Plans;
using A2S.Domain.Aggregates.Plan;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// The Whole Plan beside the A2S2 workout: schedule, phases, checkpoints, completions, allowances,
/// and the today / calendar / weekly-goals views. Every date is a Europe/London local date (yyyy-MM-dd).
/// </summary>
[ApiController]
[Route("api/v1/plans")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The current user's active plan.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(TrainingPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetActivePlanQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Creates a plan (start snaps to the coming Monday) and makes it active.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TrainingPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTrainingPlanCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : CreatedAtAction(nameof(GetActive), null, result.Value);
    }

    [HttpPut("{id:guid}/schedule")]
    [ProducesResponseType(typeof(TrainingPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule([FromRoute] Guid id, [FromBody] UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateScheduleCommand(id, request.Schedule), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpPost("{id:guid}/phases")]
    [ProducesResponseType(typeof(TrainingPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddPhase([FromRoute] Guid id, [FromBody] AddPhaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AddPhaseCommand(id, request.Type, request.From, request.To, request.Note), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpDelete("{id:guid}/phases/{from}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePhase([FromRoute] Guid id, [FromRoute] DateOnly from, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RemovePhaseCommand(id, from), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    [HttpPost("{id:guid}/checkpoints")]
    [ProducesResponseType(typeof(TrainingPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCheckpoint([FromRoute] Guid id, [FromBody] AddCheckpointRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AddCheckpointCommand(id, request.Date, request.TargetKg, request.Note), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpDelete("{id:guid}/checkpoints/{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveCheckpoint([FromRoute] Guid id, [FromRoute] DateOnly date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RemoveCheckpointCommand(id, date), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    [HttpPost("{id:guid}/link-workout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkWorkout([FromRoute] Guid id, [FromBody] LinkWorkoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LinkWorkoutCommand(id, request.WorkoutId), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>Records a session as done. Idempotent on the Hevy workout id, or on the slot for manual completions.</summary>
    [HttpPost("{id:guid}/completions")]
    [ProducesResponseType(typeof(SessionCompletionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteSession([FromRoute] Guid id, [FromBody] CompleteSessionRequest request, CancellationToken cancellationToken)
    {
        var command = new CompleteSessionCommand(
            id,
            request.Date,
            request.Kind,
            request.LiftDay,
            request.CardioProtocolId,
            request.MobilityRoutineId,
            request.Cardio,
            request.DrillTicks,
            request.HevyWorkoutId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpDelete("{id:guid}/completions/{completionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UndoCompletion([FromRoute] Guid id, [FromRoute] Guid completionId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UndoSessionCompletionCommand(id, completionId), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    [HttpPost("{id:guid}/allowances")]
    [ProducesResponseType(typeof(AllowanceUseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UseAllowance([FromRoute] Guid id, [FromBody] UseAllowanceRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UseAllowanceCommand(id, request.Date, request.Kind), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Sets the weekly zone 4–5 minutes band (default 15–30).</summary>
    [HttpPut("{id:guid}/high-intensity-target")]
    [ProducesResponseType(typeof(HighIntensityTargetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetHighIntensityTarget([FromRoute] Guid id, [FromBody] SetHighIntensityTargetRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetHighIntensityTargetCommand(id, request.MinMinutes, request.MaxMinutes), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Takes back one allowance use: <c>?date=yyyy-MM-dd&amp;kind=FlexibleMeal|Alcohol</c>.</summary>
    [HttpDelete("{id:guid}/allowances")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAllowanceUse([FromRoute] Guid id, [FromQuery] DateOnly date, [FromQuery] AllowanceKind kind, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RemoveAllowanceUseCommand(id, date, kind), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>Today's (or the given date's) sessions, phase and the week's goals.</summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(TodayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetToday([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTodayQuery(date), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpGet("calendar")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDayDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendar([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPlanCalendarQuery(year, month), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    [HttpGet("weekly-goals")]
    [ProducesResponseType(typeof(WeeklyGoalsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeeklyGoals([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeeklyGoalsQuery(date), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
