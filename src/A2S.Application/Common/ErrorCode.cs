namespace A2S.Application.Common;

/// <summary>
/// Typed error codes for Result pattern. Controllers switch on this instead of string matching.
/// </summary>
public enum ErrorCode
{
    None = 0,
    NotFound,
    Unauthenticated,
    Unauthorized,
    ValidationFailed,
    Conflict,
    DomainRuleViolation,
    ConcurrencyConflict,
    ExternalServiceError
}
