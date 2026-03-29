using A2S.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Extensions;

/// <summary>
/// Extension methods for converting Result to ActionResult with RFC 7807 Problem Details.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a failed Result to a Problem Details ActionResult based on ErrorCode.
    /// </summary>
    public static ActionResult ToProblemResult(this Result result)
    {
        var (statusCode, title) = result.ErrorCode switch
        {
            ErrorCode.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorCode.Unauthenticated => (StatusCodes.Status401Unauthorized, "Unauthenticated"),
            ErrorCode.Unauthorized => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorCode.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation Failed"),
            ErrorCode.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorCode.ConcurrencyConflict => (StatusCodes.Status409Conflict, "Concurrency Conflict"),
            ErrorCode.DomainRuleViolation => (StatusCodes.Status422UnprocessableEntity, "Business Rule Violation"),
            ErrorCode.ExternalServiceError => (StatusCodes.Status502BadGateway, "External Service Error"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request")
        };

        return new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = result.Error
        })
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
