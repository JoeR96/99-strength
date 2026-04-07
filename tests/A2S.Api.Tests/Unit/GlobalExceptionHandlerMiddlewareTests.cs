using A2S.Api.Middleware;
using A2S.Domain.Common;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace A2S.Api.Tests.Unit;

/// <summary>
/// Tests for GlobalExceptionHandlerMiddleware — verifies RFC 7807 Problem Details responses.
/// </summary>
public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly GlobalExceptionHandlerMiddleware _middleware;
    private readonly RequestDelegate _next;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        var logger = Substitute.For<ILogger<GlobalExceptionHandlerMiddleware>>();
        _middleware = new GlobalExceptionHandlerMiddleware(_next, logger);
    }

    [Fact]
    public async Task WhenBusinessRuleViolationException_ThenReturns422WithProblemDetails()
    {
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new BusinessRuleViolationException("Cannot complete day out of order"));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Business Rule Violation");
        problemDetails.Detail.Should().Be("Cannot complete day out of order");
        problemDetails.Status.Should().Be(422);
    }

    [Fact]
    public async Task WhenEntityNotFoundException_ThenReturns404WithProblemDetails()
    {
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new EntityNotFoundException("Workout", Guid.Empty));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Not Found");
        problemDetails.Status.Should().Be(404);
    }

    [Fact]
    public async Task WhenAuthorizationException_ThenReturns403WithProblemDetails()
    {
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new AuthorizationException("You can only modify your own workouts"));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Forbidden");
        problemDetails.Detail.Should().Be("You can only modify your own workouts");
    }

    [Fact]
    public async Task WhenConcurrencyException_ThenReturns409WithProblemDetails()
    {
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new ConcurrencyException("Workout was modified"));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Concurrency Conflict");
    }

    [Fact]
    public async Task WhenValidationException_ThenReturns400WithProblemDetails()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new ValidationException(failures));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Validation Failed");
    }

    [Fact]
    public async Task WhenUnhandledException_ThenReturns500WithGenericMessage()
    {
        _next.When(x => x.Invoke(Arg.Any<HttpContext>()))
            .Do(_ => throw new InvalidOperationException("Something went wrong internally"));

        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var problemDetails = await ReadProblemDetails(context);
        problemDetails.Title.Should().Be("Internal Server Error");
        problemDetails.Detail.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task WhenNoException_ThenCallsNext()
    {
        var context = CreateHttpContext();

        await _middleware.InvokeAsync(context);

        await _next.Received(1).Invoke(context);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/workouts";
        context.Request.Method = "POST";
        return context;
    }

    private static async Task<ProblemDetails> ReadProblemDetails(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var result = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result!;
    }
}
