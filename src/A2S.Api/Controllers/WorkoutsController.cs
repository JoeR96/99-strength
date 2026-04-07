using System.ComponentModel.DataAnnotations;
using A2S.Api.Extensions;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.DeleteWorkout;
using A2S.Application.Commands.SetActiveWorkout;
using A2S.Application.Queries.GetAllWorkouts;
using A2S.Application.Queries.GetWorkout;
using A2S.Application.Queries.SimulateWorkout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
}
