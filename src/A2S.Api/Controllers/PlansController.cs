using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Hevy;
using A2S.Application.Commands.Plans;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Application.Queries.Plans;
using A2S.Application.Queries.SuggestedPlans;
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

    /// <summary>The ready-made plans a person can start with one tap, previewed for today.</summary>
    [HttpGet("suggested")]
    [ProducesResponseType(typeof(IReadOnlyList<SuggestedPlanPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggested(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSuggestedPlansQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>
    /// Starts "Cut to 85 — Nov 2026" from four training maxes and a start weight (default 95 kg): workout,
    /// cardio protocols, mobility routines, habits and the plan, starting the coming Monday.
    /// 201 on creation; 200 with the existing plan when it is already the active one.
    /// </summary>
    [HttpPost("suggested/cut-to-85")]
    [ProducesResponseType(typeof(StartSuggestedPlanResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(StartSuggestedPlanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartCutTo85([FromBody] StartSuggestedPlanRequest request, CancellationToken cancellationToken)
    {
        var command = new StartSuggestedPlanCommand(
            request.BenchTm,
            request.SquatTm,
            request.OhpTm,
            request.DeadliftTm,
            request.StartWeightKg ?? Application.SuggestedPlans.CutTo85Nov2026.DefaultStartWeightKg);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return result.Value.AlreadyStarted
            ? Ok(result.Value)
            : CreatedAtAction(nameof(GetActive), null, result.Value);
    }

    /// <summary>Replaces the weekly schedule. Soft-rule warnings (4×4 the day before Lower A/B) come back alongside the plan.</summary>
    [HttpPut("{id:guid}/schedule")]
    [ProducesResponseType(typeof(UpdateScheduleResult), StatusCodes.Status200OK)]
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

    /// <summary>
    /// Pushes one scheduled session to Hevy as a routine (Mobility A first for lifts, interval sets for
    /// cardio). Idempotent: pushing again updates the same routine. 400 without a stored Hevy key.
    /// </summary>
    [HttpPost("{id:guid}/sessions/push")]
    [ProducesResponseType(typeof(PushRoutineResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PushSession([FromRoute] Guid id, [FromBody] PushSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PushSessionRoutineCommand(id, request.Date, request.Kind, request.LiftDay), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Pushes every non-rest session of the Mon–Sun week containing <c>?date=</c> (today when omitted).</summary>
    [HttpPost("{id:guid}/sessions/push-week")]
    [ProducesResponseType(typeof(PushWeekResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PushWeek([FromRoute] Guid id, [FromQuery] DateOnly? date, [FromServices] Domain.Common.ILocalClock clock, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PushWeekRoutinesCommand(id, date ?? clock.Today()), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
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
