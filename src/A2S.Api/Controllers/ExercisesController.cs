using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.ConfirmStartingWeight;
using A2S.Application.Commands.ConfirmWorkingWeight;
using A2S.Application.Commands.RemoveExercise;
using A2S.Application.Commands.RetrofixLinearTm;
using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.Commands.UpdateExercises;
using A2S.Application.Commands.UpdateWorkingWeight;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Exercise CRUD, substitution, and weight management operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class ExercisesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExercisesController(IMediator mediator)
    {
        _mediator = mediator;
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
        var command = new UpdateExercisesCommand(id, request.Updates);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

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
        var command = new SubstituteExerciseCommand(
            id,
            exerciseId,
            request.NewExerciseName,
            request.NewExternalTemplateId,
            request.Reason,
            request.NewProgressionConfig);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

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
        var result = await _mediator.Send(new RemoveExerciseCommand(id, exerciseId), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Updates the working weight for an accessory exercise.
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
        var command = new UpdateWorkingWeightCommand(
            id, exerciseId, request.NewWeight, request.Unit, request.Reason);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

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
        var command = new ConfirmStartingWeightCommand(id, exerciseId, request.Weight, request.Unit);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return NoContent();
    }

    /// <summary>
    /// Confirms the new working weight for a Cable/Machine exercise after progression.
    /// </summary>
    [HttpPost("{id:guid}/exercises/{exerciseId:guid}/confirm-working-weight")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmWorkingWeight(
        [FromRoute] Guid id,
        [FromRoute] Guid exerciseId,
        [FromBody] ConfirmWorkingWeightRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmWorkingWeightCommand(id, exerciseId, request.Weight, request.Unit);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
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
        var command = new RetrofixLinearTmCommand(id, exerciseId, request.OriginalStartingTm);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }
}
