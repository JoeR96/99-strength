using A2S.Api.Extensions;
using A2S.Application.Queries.GetExerciseHistory;
using A2S.Application.Queries.GetWorkoutHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Exercise and workout history query operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class ExerciseHistoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExerciseHistoryController(IMediator mediator)
    {
        _mediator = mediator;
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
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return BadRequest();
        }

        var result = await _mediator.Send(new GetExerciseHistoryQuery(name), cancellationToken);

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
                Detail = "No history found for this exercise"
            });
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
        var result = await _mediator.Send(new GetWorkoutHistoryQuery(id), cancellationToken);

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
                Detail = "No workout found"
            });
        }

        return Ok(result.Value);
    }
}
