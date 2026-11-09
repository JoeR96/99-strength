using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using A2S.Api.Extensions;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.DeleteWorkout;
using A2S.Application.Commands.SetActiveWorkout;
using A2S.Application.Commands.SimulateAndCompleteDay;
using A2S.Application.Queries.GetAllWorkouts;
using A2S.Application.Queries.GetWorkout;
using A2S.Application.Queries.SimulateWorkout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Workout CRUD and activation operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class WorkoutsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkoutsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new workout program.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWorkout(
        [FromBody] CreateWorkoutCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return CreatedAtAction(
            nameof(GetCurrentWorkout),
            new { id = result.Value },
            new { id = result.Value });
    }

    /// <summary>
    /// Gets the currently active workout.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentWorkout(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurrentWorkoutQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        if (result.Value == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "No active workout found"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets all workouts for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWorkouts(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllWorkoutsQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sets a workout as the active program.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActiveWorkout(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetActiveWorkoutCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a workout.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkout(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteWorkoutCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Simulates workout progression over a specified number of sessions.
    /// Returns projected TM/weight/set data for each exercise.
    /// </summary>
    [HttpGet("{id:guid}/simulate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SimulateWorkout(
        [FromRoute] Guid id,
        [FromQuery][Range(1, 500)] int sessions = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new SimulateWorkoutQuery(id, sessions),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Dev-only: streams persistent day-by-day simulation over NDJSON.
    /// Each line is one JSON event: started | day | completed | error.
    /// Completes real days via CompleteDayCommand so workout state persists.
    /// </summary>
    [HttpGet("{id:guid}/simulate/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task SimulateStream(
        [FromRoute] Guid id,
        [FromQuery][Range(1, 200)] int days = 84,
        [FromQuery][Range(0.0, 1.0)] double successRate = 0.65,
        [FromQuery][Range(0.0, 1.0)] double maintainRate = 0.25,
        [FromQuery] int? seed = null,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "application/x-ndjson";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        async Task WriteEvent(object payload)
        {
            var line = JsonSerializer.Serialize(payload, jsonOptions) + "\n";
            await Response.WriteAsync(line, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await WriteEvent(new { type = "started", totalDays = days });

        for (int i = 0; i < days; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var perDaySeed = seed.HasValue ? seed.Value + i : (int?)null;
            var result = await _mediator.Send(
                new SimulateAndCompleteDayCommand(id, successRate, maintainRate, perDaySeed),
                cancellationToken);

            if (result.IsFailure)
            {
                await WriteEvent(new { type = "error", message = result.Error });
                return;
            }

            var inner = result.Value.InnerResult;
            await WriteEvent(new
            {
                type = "day",
                outcome = result.Value.OutcomeLabel,
                day = (int)inner.Day,
                weekNumber = inner.WeekNumber,
                blockNumber = inner.BlockNumber,
                exercisesCompleted = inner.ExercisesCompleted,
                progressionChanges = inner.ProgressionChanges,
                newCurrentWeek = inner.NewCurrentWeek,
                newCurrentDay = inner.NewCurrentDay,
                weekProgressed = inner.WeekProgressed,
                programComplete = inner.ProgramComplete,
                isDeloadWeek = inner.IsDeloadWeek
            });

            if (inner.ProgramComplete) break;
        }

        await WriteEvent(new { type = "completed" });
    }
}
