namespace A2S.Domain.Common;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected.
/// </summary>
public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
