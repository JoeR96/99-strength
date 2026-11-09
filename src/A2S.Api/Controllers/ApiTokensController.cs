using System.Security.Claims;
using A2S.Api.Authentication;
using A2S.Api.Contracts.Requests;
using A2S.Api.Extensions;
using A2S.Application.Commands.ApiTokens;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.ApiTokens;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace A2S.Api.Controllers;

/// <summary>
/// Personal API tokens for programs that cannot hold a Clerk session (the desktop habit widget).
/// Creating, listing and revoking need a Clerk session: a token cannot mint or revoke tokens.
/// Only <c>whoami</c> accepts a token, so a client can test its connection.
/// </summary>
[ApiController]
[Route("api/v1/users/me/api-tokens")]
[Authorize]
public class ApiTokensController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ApiTokensController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>Creates a token. The response is the only time the token itself is returned.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedApiTokenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateApiTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateApiTokenCommand(request.Name), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblemResult();
        }

        Response.Headers[HeaderNames.CacheControl] = "no-store";
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Lists the caller's tokens, revoked ones included, newest first. No secrets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListApiTokensQuery(), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : Ok(result.Value);
    }

    /// <summary>Revokes a token. It stops working immediately.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RevokeApiTokenCommand(id), cancellationToken);
        return result.IsFailure ? result.ToProblemResult() : NoContent();
    }

    /// <summary>
    /// Who the caller is. With a token, <c>tokenName</c> is that token's name; with a Clerk session it is null.
    /// </summary>
    [HttpGet("whoami")]
    [AllowApiToken]
    [ProducesResponseType(typeof(WhoAmIResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult WhoAmI()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var tokenName = User.IsApiToken()
            ? User.FindFirstValue(ApiTokenAuthenticationDefaults.TokenNameClaim)
            : null;
        return Ok(new WhoAmIResponse(userId, _currentUser.Email, tokenName));
    }
}

/// <summary>The caller's identity, and the name of the token used when there was one.</summary>
public sealed record WhoAmIResponse(string UserId, string? Email, string? TokenName);
