using A2S.Domain.Entities;

namespace A2S.Application.Services;

/// <summary>
/// Service for generating JWT tokens for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    /// <param name="user">The application user to generate a token for.</param>
    /// <returns>A JWT token string.</returns>
    string GenerateToken(ApplicationUser user);
}
