using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.DeleteWorkout;
using A2S.Application.Commands.SetActiveWorkout;
using A2S.Application.Queries.GetAllWorkouts;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Application.Queries.GetWorkout;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for workout operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class WorkoutsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkoutsController> _logger;

    public WorkoutsController(IMediator mediator, ILogger<WorkoutsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new workout program.
    /// </summary>
    /// <param name="command">The workout creation command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created workout</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWorkout(
        [FromBody] CreateWorkoutCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating new workout: {Name}", command.Name);

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to create workout: {Error}", result.Error);

                // Check if it's a conflict (active workout exists)
                if (result.Error?.Contains("active workout") == true)
                {
                    return Conflict(new { error = result.Error });
                }

                return BadRequest(new { error = result.Error });
            }

            _logger.LogInformation("Workout created successfully with ID: {WorkoutId}", result.Value);

            return CreatedAtAction(
                nameof(GetCurrentWorkout),
                new { id = result.Value },
                new { id = result.Value });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for workout creation: {Errors}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the currently active workout.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The active workout or null</returns>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentWorkout(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching current workout");

        var result = await _mediator.Send(new GetCurrentWorkoutQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch current workout: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        if (result.Value == null)
        {
            _logger.LogInformation("No active workout found");
            return NotFound(new { message = "No active workout found" });
        }

        _logger.LogInformation("Current workout found: {WorkoutId}", result.Value.Id);

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the exercise library with all available exercises.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The exercise library</returns>
    [HttpGet("exercises/library")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExerciseLibrary(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching exercise library");

        var result = await _mediator.Send(new GetExerciseLibraryQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch exercise library: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets all workouts for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all workouts</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllWorkouts(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching all workouts for user");

        var result = await _mediator.Send(new GetAllWorkoutsQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch workouts: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sets a workout as the active program.
    /// </summary>
    /// <param name="id">The workout ID to activate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or failure</returns>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActiveWorkout(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting workout {WorkoutId} as active", id);

        var result = await _mediator.Send(new SetActiveWorkoutCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to activate workout: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Deletes a workout.
    /// </summary>
    /// <param name="id">The workout ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or failure</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteWorkout(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting workout {WorkoutId}", id);

        var result = await _mediator.Send(new DeleteWorkoutCommand(id), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to delete workout: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}
