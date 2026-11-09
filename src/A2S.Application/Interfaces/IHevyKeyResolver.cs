using A2S.Domain.Common;

namespace A2S.Application.Interfaces;

/// <summary>
/// Resolves a user's Hevy API key from secure storage. The only sanctioned way to obtain the
/// plaintext key; it is used immediately for an outbound Hevy call and never returned to clients.
/// </summary>
public interface IHevyKeyResolver
{
    /// <summary>Returns the plaintext key for <paramref name="userId"/>, or null when none is stored.</summary>
    Task<string?> ForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the plaintext key for the current authenticated user, or null.</summary>
    Task<string?> ForCurrentUserAsync(CancellationToken cancellationToken = default);
}
