namespace A2S.Domain.Common;

/// <summary>
/// Thrown when a domain business rule is violated (e.g., invalid state transition, invariant breach).
/// Replaces ArgumentException in CheckRule methods.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }

    public BusinessRuleViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
