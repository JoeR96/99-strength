using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.SetHevyFolderId;
using A2S.Application.Commands.SetHevySyncedRoutine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for workout Hevy sync operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class WorkoutSyncController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkoutSyncController> _logger;

    public WorkoutSyncController(IMediator mediator, ILogger<WorkoutSyncController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the Hevy routine folder ID for a workout.
    /// </summary>
    [HttpPut("{id:guid}/hevy-folder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetHevyFolderId(
        [FromRoute] Guid id,
        [FromBody] SetHevyFolderIdRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting Hevy folder ID for workout {WorkoutId}", id);

        var result = await _mediator.Send(new SetHevyFolderIdCommand(id, request.FolderId), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to set Hevy folder ID: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Records that a routine was synced to Hevy for a specific week/day.
    /// </summary>
    [HttpPost("{id:guid}/hevy-synced-routine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetHevySyncedRoutine(
        [FromRoute] Guid id,
        [FromBody] SetHevySyncedRoutineRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Setting Hevy synced routine for workout {WorkoutId}, week {Week}, day {Day}",
            id, request.WeekNumber, request.DayNumber);

        var result = await _mediator.Send(
            new SetHevySyncedRoutineCommand(id, request.WeekNumber, request.DayNumber, request.RoutineId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to set Hevy synced routine: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return Ok(new { success = true });
    }
}
