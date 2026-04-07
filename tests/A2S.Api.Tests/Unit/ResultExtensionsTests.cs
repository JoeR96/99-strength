using A2S.Api.Extensions;
using A2S.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Tests.Unit;

/// <summary>
/// Tests for Result extension methods — verifies ErrorCode-to-HTTP status mapping.
/// </summary>
public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorCode.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCode.Unauthenticated, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorCode.Unauthorized, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCode.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCode.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCode.ConcurrencyConflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCode.DomainRuleViolation, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorCode.ExternalServiceError, StatusCodes.Status502BadGateway)]
    [InlineData(ErrorCode.None, StatusCodes.Status400BadRequest)]
    public void ToProblemResult_WhenErrorCode_ThenReturnsCorrectStatusCode(ErrorCode errorCode, int expectedStatus)
    {
        var result = Result.Failure("Test error", errorCode);

        var actionResult = result.ToProblemResult();

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Detail.Should().Be("Test error");
        problemDetails.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void ToProblemResult_WhenNotFoundError_ThenTitleIsNotFound()
    {
        var result = Result.Failure("Workout not found", ErrorCode.NotFound);

        var actionResult = result.ToProblemResult();

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Not Found");
    }
}
