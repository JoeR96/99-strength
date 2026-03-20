using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.Commands.UndoCompletion;
using A2S.Application.Commands.UpdateBlockSequence;
using A2S.Application.Queries.GetWorkoutHistory;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for workout progression and flow operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class WorkoutProgressionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkoutProgressionController> _logger;

    public WorkoutProgressionController(IMediator mediator, ILogger<WorkoutProgressionController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Completes a training day with exercise performance data.
    /// This applies progression rules to each exercise based on actual performance.
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
        _logger.LogInformation("Completing day {Day} for workout {WorkoutId}", day, id);

        if (day < 1 || day > 6)
        {
            return BadRequest(new { error = "Day must be between 1 and 6." });
        }

        var dayNumber = (DayNumber)day;

        try
        {
            var command = new CompleteDayCommand(id, dayNumber, request.Performances);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to complete day: {Error}", result.Error);

                if (result.Error?.Contains("not found") == true)
                {
                    return NotFound(new { error = result.Error });
                }

                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Day {Day} completed for workout {WorkoutId}", day, id);
            return Ok(result.Value);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for complete day: {Errors}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
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
        _logger.LogInformation("Progressing to next week for workout {WorkoutId}", id);

        var result = await _mediator.Send(new ProgressWeekCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to progress week: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation(
            "Progressed from week {PreviousWeek} to week {NewWeek} for workout {WorkoutId}",
            result.Value!.PreviousWeek,
            result.Value.NewWeek,
            id);

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
        var result = await _mediator.Send(
            new UndoCompletionCommand(id),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error });
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

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found") == true)
                return NotFound(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the workout history including all completed activities.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkoutHistory(
        [FromQuery] Guid? id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching workout history");

        var result = await _mediator.Send(new GetWorkoutHistoryQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch workout history: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        if (result.Value == null)
        {
            return NotFound(new { message = "No workout found" });
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
        _logger.LogInformation("Fetching week plan for week {Week}, day {Day}", week, day);

        var result = await _mediator.Send(
            new GetWeekPlanQuery(id, week, day),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to get week plan: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
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
        _logger.LogInformation("Fetching week plan for workout {WorkoutId}, week {Week}, day {Day}", id, week, day);

        var result = await _mediator.Send(
            new GetWeekPlanQuery(id, week, day),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to get week plan: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
