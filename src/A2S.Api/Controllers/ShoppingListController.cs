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
/// The weekly shopping list built from the week's meals: read, (re)generate, tick lines, export.
/// Weeks run Monday to Sunday; any <c>yyyy-MM-dd</c> date in a week stands for that week.
/// </summary>
[ApiController]
[Route("api/v1/shopping-list")]
[Authorize]
public class ShoppingListController : ControllerBase
{
    private readonly IMediator _mediator;

    public ShoppingListController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The list for a week (this week when omitted); empty with a null id when none exists yet.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ShoppingListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] DateOnly? week, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShoppingListQuery(week), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Builds or rebuilds the week's list from its menu, keeping ticks on lines still needed.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ShoppingListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromBody] GenerateShoppingListRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateShoppingListCommand(request.WeekStart), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Ticks or unticks one line, matched by name and unit.</summary>
    [HttpPost("items/toggle")]
    [ProducesResponseType(typeof(ShoppingListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle([FromBody] ToggleShoppingItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ToggleShoppingItemCommand(request.ListId, request.Name, request.Unit), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>The list as <c>text</c> (default) or <c>csv</c>, served as a download.</summary>
    [HttpGet("export")]
    [Produces("text/plain", "text/csv")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromQuery] DateOnly? week, [FromQuery] string format = "text", CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ShoppingListExportFormat>(format, ignoreCase: true, out var parsed))
        {
            return BadRequest(new ProblemDetails { Title = "Unknown format", Detail = "format must be 'text' or 'csv'.", Status = StatusCodes.Status400BadRequest });
        }

        var result = await _mediator.Send(new ExportShoppingListQuery(week, parsed), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        Response.Headers.ContentDisposition = $"attachment; filename=\"{result.Value.FileName}\"";
        return Content(result.Value.Content, result.Value.ContentType);
    }
}
