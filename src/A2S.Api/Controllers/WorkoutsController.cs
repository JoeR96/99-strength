using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Queries.GetWorkout;
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
}
