using A2S.Api.Contracts.Requests;
using A2S.Api.Contracts.Responses;
using A2S.Api.Extensions;
using A2S.Application.Commands.Users;
using A2S.Application.Queries.Users;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
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
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UsersController(IMediator mediator, IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _mediator = mediator;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
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

    /// <summary>
    /// Saves the current user's Hevy API key.
    /// </summary>
    [HttpPut("me/hevy-api-key")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetHevyApiKey(
        [FromBody] SetHevyApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not string userId)
        {
            return NotFound();
        }

        var user = await _userRepository.GetByIdAsync(new UserId(userId), cancellationToken);
        if (user is null) return NotFound();

        user.SetHevyApiKey(request.ApiKey);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Gets the current user's Hevy API key.
    /// </summary>
    [HttpGet("me/hevy-api-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHevyApiKey(CancellationToken cancellationToken)
    {
        if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not string userId)
        {
            return NotFound();
        }

        var user = await _userRepository.GetByIdAsync(new UserId(userId), cancellationToken);
        if (user is null) return NotFound();

        return Ok(new { apiKey = user.HevyApiKey });
    }
}

public sealed record SetHevyApiKeyRequest(string? ApiKey);
