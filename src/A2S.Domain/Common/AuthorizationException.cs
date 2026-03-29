namespace A2S.Domain.Common;

/// <summary>
/// Thrown when an operation is not authorized (e.g., accessing another user's data).
/// </summary>
public class AuthorizationException : DomainException
{
    public AuthorizationException(string message) : base(message)
    {
    }
}
