namespace A2S.Domain.Common;

/// <summary>
/// Reversible protection for secrets the domain must hold but never expose in plaintext at rest
/// (for example a user's Hevy API key). Implemented in Infrastructure.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Returns an opaque protected form of <paramref name="plaintext"/>.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Returns the plaintext for a value produced by <see cref="Protect"/>, or null when the
    /// value cannot be unprotected (corrupt, or produced with a key that no longer exists).
    /// </summary>
    string? Unprotect(string protectedValue);
}
