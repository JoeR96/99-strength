namespace A2S.Application.Common;

/// <summary>
/// Service interface for accessing the current authenticated user's information.
/// Implemented in the API layer using HttpContext.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the unique identifier of the current authenticated user.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the email address of the current authenticated user.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets whether a user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
