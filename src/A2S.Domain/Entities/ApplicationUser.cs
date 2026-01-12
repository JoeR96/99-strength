using Microsoft.AspNetCore.Identity;

namespace A2S.Domain.Entities;

/// <summary>
/// Application user entity extending ASP.NET Core Identity user.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the user profile was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
