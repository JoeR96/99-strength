using A2S.Api.Contracts.Responses;
using A2S.Application.Common;
using A2S.Application.Queries.Users;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// User profile and Hevy API key management for the authenticated user.
/// Users are provisioned automatically from Clerk on first request; there is no create endpoint.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUserService _currentUser;

    public UsersController(
        IMediator mediator,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ISecretProtector protector,
        ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _protector = protector;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets a user by ID. Only the authenticated user's own record is accessible.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (!string.Equals(id, _currentUser.UserId, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You can only access your own user record."
            });
        }

        var result = await _mediator.Send(new GetUserByIdQuery(new UserId(id)), cancellationToken);
        if (result is null)
        {
            return UserNotFound();
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
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return UserNotFound();
        }

        var result = await _mediator.Send(new GetUserByIdQuery(new UserId(userId)), cancellationToken);
        if (result is null)
        {
            return UserNotFound();
        }

        return Ok(new UserResponse(result.Id, result.Email, result.Name, result.CreatedAt));
    }

    /// <summary>
    /// Saves (or, with a blank body value, removes) the current user's Hevy API key.
    /// The key is encrypted at rest and is never returned by any endpoint.
    /// </summary>
    [HttpPut("me/hevy-api-key")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetHevyApiKey(
        [FromBody] SetHevyApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null) return UserNotFound();

        user.SetHevyApiKey(request.ApiKey, _protector);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Removes the current user's Hevy API key.
    /// </summary>
    [HttpDelete("me/hevy-api-key")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHevyApiKey(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null) return UserNotFound();

        user.ClearHevyApiKey();
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Reports whether a Hevy API key is stored, plus its last four characters for recognition.
    /// The key itself is never returned.
    /// </summary>
    [HttpGet("me/hevy-api-key")]
    [ProducesResponseType(typeof(HevyApiKeyStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHevyApiKeyStatus(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null) return UserNotFound();

        var key = user.RevealHevyApiKey(_protector);
        if (string.IsNullOrEmpty(key))
        {
            return Ok(new HevyApiKeyStatusResponse(false, null));
        }

        var lastFour = key.Length <= 4 ? key : key[^4..];
        return Ok(new HevyApiKeyStatusResponse(true, lastFour));
    }

    private async Task<Domain.Aggregates.User.User?> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return null;
        return await _userRepository.GetByIdAsync(new UserId(userId), cancellationToken);
    }

    private NotFoundObjectResult UserNotFound() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Not Found",
        Detail = "User not found"
    });
}

public sealed record SetHevyApiKeyRequest(string? ApiKey);

/// <summary>Whether a Hevy key is stored and its last four characters. Never the key itself.</summary>
public sealed record HevyApiKeyStatusResponse(bool HasKey, string? LastFour);
