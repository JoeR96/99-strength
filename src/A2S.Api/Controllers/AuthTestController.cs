using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace A2S.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class AuthTestController : ControllerBase
{
    /// <summary>
    /// Test endpoint that requires authentication.
    /// Returns the authenticated user's claims.
    /// </summary>
    [HttpGet("me")]
    public ActionResult<UserClaimsResponse> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var claims = User.Claims.Select(c => new ClaimInfo
        {
            Type = c.Type,
            Value = c.Value
        }).ToList();

        return Ok(new UserClaimsResponse
        {
            UserId = userId,
            Email = email,
            Claims = claims
        });
    }
}

public record UserClaimsResponse
{
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public List<ClaimInfo> Claims { get; init; } = new();
}

public record ClaimInfo
{
    public required string Type { get; init; }
    public required string Value { get; init; }
}
