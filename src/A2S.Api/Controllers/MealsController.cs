using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.Meals;
using A2S.Application.DTOs;
using A2S.Application.Queries.Meals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// Meals (recipes) and the weekly menu. Weeks run Monday to Sunday; any <c>yyyy-MM-dd</c> date in a
/// week stands for that week.
/// </summary>
[ApiController]
[Route("api/v1/meals")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MealsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists the current user's meals, seeding the starter recipes on a first visit.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MealDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMealsQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Creates a meal.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MealDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] SaveMealRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateMealCommand(
            request.Title, request.Type, request.Description, request.Ingredients, request.Steps, request.Tags, request.CutFriendly);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Created($"/api/v1/meals/{result.Value.Id}", result.Value);
    }

    /// <summary>Replaces a meal's content.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] SaveMealRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateMealCommand(
            id, request.Title, request.Type, request.Description, request.Ingredients, request.Steps, request.Tags, request.CutFriendly);
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Deletes a meal. A week still listing it drops it at the next shopping-list generation.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteMealCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>The menu for a week (this week when omitted).</summary>
    [HttpGet("week")]
    [ProducesResponseType(typeof(WeekMealsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Week([FromQuery] DateOnly? week, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWeekMealsQuery(week), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Sets the menu for a week. Items on an existing shopping list change only on the next generate.</summary>
    [HttpPut("week")]
    [ProducesResponseType(typeof(WeekMealsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWeek([FromBody] SetWeekMealsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetWeekMealsCommand(request.WeekStart, request.MealIds), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }
}
