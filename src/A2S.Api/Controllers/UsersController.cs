using A2S.Api.Contracts.Requests;
using A2S.Api.Contracts.Responses;
using A2S.Api.Extensions;
using A2S.Application.Commands.Users;
using A2S.Application.Queries.Users;
using A2S.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// User management operations.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.Email, request.Name);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        var response = new UserResponse(result.Value.Id, result.Value.Email, result.Value.Name, result.Value.CreatedAt);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, response);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(new UserId(id));
        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "User not found"
            });
        }

        return Ok(new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt));
    }

    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not string userId)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "User not found"
            });
        }

        var query = new GetUserByIdQuery(new UserId(userId));
        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "User not found"
            });
        }

        return Ok(new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt));
    }
}
