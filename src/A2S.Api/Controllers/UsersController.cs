using A2S.Application.Commands.Users;
using A2S.Application.Queries.Users;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for user management operations.
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
    /// <param name="command">The create user command.</param>
    /// <returns>The created user.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(request.Email, request.Name);

        try
        {
            var result = await _mediator.Send(command);
            var response = new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <returns>The user if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var query = new GetUserByIdQuery(id);
        var result = await _mediator.Send(query);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt));
    }

    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    /// <returns>The current user if found.</returns>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        // Get user ID from HttpContext.Items (set by AutoProvisionUserMiddleware)
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not Guid userId)
        {
            return NotFound(new { error = "User not found" });
        }

        var query = new GetUserByIdQuery(userId);
        var result = await _mediator.Send(query);

        if (result is null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt));
    }
}

/// <summary>
/// Request model for creating a user.
/// </summary>
public record CreateUserRequest(string Email, string Name);

/// <summary>
/// Response model for user data.
/// </summary>
public record UserResponse(Guid Id, string Email, string Name, DateTime CreatedAt);
