using A2S.Domain.Common;

namespace A2S.Application.Common;

/// <summary>
/// Extensions for ICurrentUserService to provide strongly-typed user ID.
/// </summary>
public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// Gets the current user's ID as a strongly-typed UserId.
    /// Returns null if user is not authenticated or ID is not a valid GUID.
    /// </summary>
    public static UserId? GetUserId(this ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var guid))
        {
            return null;
        }

        return new UserId(guid);
    }
}
