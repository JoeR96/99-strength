using System.Security.Cryptography;
using System.Text;
using A2S.Domain.Common;

namespace A2S.Application.Common;

/// <summary>
/// Extensions for ICurrentUserService to provide strongly-typed user ID.
/// </summary>
public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// Gets the current user's ID as a strongly-typed UserId.
    /// If the claim is already a GUID it is used directly; otherwise a deterministic
    /// UUID v5 is derived from the Clerk user-ID string.
    /// Returns null if user is not authenticated.
    /// </summary>
    public static UserId? GetUserId(this ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (Guid.TryParse(userId, out var guid))
        {
            return new UserId(guid);
        }

        // Derive a deterministic UUID from the Clerk user ID string
        guid = DeterministicGuid(userId);
        return new UserId(guid);
    }

    private static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Take the first 16 bytes and stamp as UUID v5-like
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant 1
        return new Guid(bytes);
    }
}
