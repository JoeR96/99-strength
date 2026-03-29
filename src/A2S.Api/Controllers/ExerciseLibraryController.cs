using A2S.Api.Extensions;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Exercise library browsing operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts/exercises/library")]
[Authorize]
public class ExerciseLibraryController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExerciseLibraryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets the exercise library with optional filtering by equipment type, muscle group, or search term.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExerciseLibrary(
        [FromQuery] EquipmentType? equipmentType,
        [FromQuery] string? muscleGroup,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        var query = new GetExerciseLibraryQuery
        {
            EquipmentType = equipmentType,
            MuscleGroup = muscleGroup,
            SearchTerm = searchTerm
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        return Ok(result.Value);
    }
}
