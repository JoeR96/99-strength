using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.RemoveExercise;
using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.Commands.UpdateExercises;
using A2S.Application.Commands.UpdateWorkingWeight;
using A2S.Application.Commands.RetrofixLinearTm;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Application.Queries.GetExerciseHistory;
using A2S.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for workout exercise management operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class WorkoutExercisesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkoutExercisesController> _logger;

    public WorkoutExercisesController(IMediator mediator, ILogger<WorkoutExercisesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the exercise library with all available exercises.
    /// </summary>
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
    /// Updates one or more exercises in a workout.
    /// </summary>
    [HttpPut("{id:guid}/exercises")]
    [ProducesResponseType(typeof(UpdateExercisesResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExercises(
        [FromRoute] Guid id,
        [FromBody] UpdateExercisesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating {Count} exercises in workout {WorkoutId}", request.Updates.Count, id);

        var command = new UpdateExercisesCommand(id, request.Updates);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to update exercises: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Updated {Count} exercises in workout {WorkoutId}", result.Value!.UpdatedCount, id);

        return Ok(result.Value);
    }

    /// <summary>
    /// Substitutes an exercise with another exercise permanently.
    /// </summary>
    [HttpPut("{id:guid}/exercises/{exerciseId:guid}/substitute")]
    [ProducesResponseType(typeof(SubstituteExerciseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubstituteExercise(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        [FromBody] SubstituteExerciseRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Substituting exercise {ExerciseId} with '{NewName}' in workout {WorkoutId}",
            exerciseId, request.NewExerciseName, id);

        var command = new SubstituteExerciseCommand(
            id,
            exerciseId,
            request.NewExerciseName,
            request.NewHevyExerciseTemplateId,
            request.Reason,
            request.NewProgressionConfig);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to substitute exercise: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation(
            "Substituted exercise '{OriginalName}' with '{NewName}' in workout {WorkoutId}",
            result.Value!.OriginalName, result.Value.NewName, id);

        return Ok(result.Value);
    }

    /// <summary>
    /// Removes an exercise from a workout.
    /// </summary>
    [HttpDelete("{id:guid}/exercises/{exerciseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveExercise(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing exercise {ExerciseId} from workout {WorkoutId}", exerciseId, id);

        var result = await _mediator.Send(
            new RemoveExerciseCommand(id, exerciseId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to remove exercise: {Error}", result.Error);

            if (result.Error?.Contains("not found") == true)
            {
                return NotFound(new { error = result.Error });
            }

            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Updates the working weight for an accessory exercise (RepsPerSet or MinimalSets).
    /// </summary>
    [HttpPut("{id:guid}/exercises/{exerciseId:guid}/working-weight")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkingWeight(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        [FromBody] UpdateWorkingWeightRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating working weight for exercise {ExerciseId} to {Weight}{Unit} in workout {WorkoutId}",
            exerciseId, request.NewWeight, request.Unit, id);

        var command = new UpdateWorkingWeightCommand(
            id,
            exerciseId,
            request.NewWeight,
            request.Unit,
            request.Reason);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to update working weight: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation(
            "Successfully updated working weight for exercise {ExerciseId} in workout {WorkoutId}",
            exerciseId, id);

        return NoContent();
    }

    /// <summary>
    /// Confirms the starting weight for a RepsPerSet exercise after the first session.
    /// </summary>
    [HttpPost("{id:guid}/exercises/{exerciseId:guid}/confirm-weight")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmStartingWeight(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        [FromBody] ConfirmStartingWeightRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Confirming starting weight for exercise {ExerciseId} to {Weight}{Unit} in workout {WorkoutId}",
            exerciseId, request.Weight, request.Unit, id);

        var command = new Application.Commands.ConfirmStartingWeight.ConfirmStartingWeightCommand(
            id,
            exerciseId,
            request.Weight,
            request.Unit);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to confirm starting weight: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Retrofixes the Training Max history for a Linear progression exercise.
    /// </summary>
    [HttpPost("{id:guid}/exercises/{exerciseId:guid}/retrofix-tm")]
    [ProducesResponseType(typeof(List<RetrofixLinearTmResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetrofixLinearTm(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        [FromBody] RetrofixLinearTmRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrofixing Linear TM history for exercise {ExerciseId} in workout {WorkoutId} from starting TM {StartingTm}",
            exerciseId, id, request.OriginalStartingTm);

        var command = new RetrofixLinearTmCommand(id, exerciseId, request.OriginalStartingTm);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to retrofix TM: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets aggregated history for a specific exercise by name across all workouts.
    /// </summary>
    [HttpGet("exercises/{name}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExerciseHistory(
        [FromRoute] string name,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching exercise history for: {ExerciseName}", name);

        var result = await _mediator.Send(new GetExerciseHistoryQuery(name), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch exercise history: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        if (result.Value == null)
        {
            return NotFound(new { message = "No history found for this exercise" });
        }

        return Ok(result.Value);
    }
}
