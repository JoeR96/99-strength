using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.DeleteWorkout;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.Commands.SetActiveWorkout;
using A2S.Application.Queries.GetAllWorkouts;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Application.Queries.GetWorkout;
using A2S.Domain.Enums;
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

    /// <summary>
    /// Completes a training day with exercise performance data.
    /// This applies progression rules to each exercise based on actual performance.
    /// </summary>
    /// <param name="id">The workout ID</param>
    /// <param name="day">The day number to complete (1-6)</param>
    /// <param name="request">The exercise performance data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of completing the day</returns>
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

        // Validate day number
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
    /// This advances the current week, updates the block if necessary,
    /// and handles deload week transitions.
    /// </summary>
    /// <param name="id">The workout ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of week progression</returns>
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
}

/// <summary>
/// Request body for completing a training day.
/// </summary>
public sealed record CompleteDayRequest
{
    /// <summary>
    /// The exercise performances for this day.
    /// </summary>
    public required IReadOnlyList<ExercisePerformanceRequest> Performances { get; init; }
}
