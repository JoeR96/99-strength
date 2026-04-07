using System.Net;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class CorrelationIdMiddlewareTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public CorrelationIdMiddlewareTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task Request_WithoutCorrelationId_GeneratesOneInResponse()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/current");

        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        var correlationId = values!.First();
        correlationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(correlationId, out _).Should().BeTrue("generated correlation ID should be a GUID");
    }

    [Fact]
    public async Task Request_WithCorrelationId_EchoesItInResponse()
    {
        var client = CreateClient();
        var expectedCorrelationId = "test-correlation-id-12345";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", expectedCorrelationId);

        var response = await client.GetAsync("/api/v1/workouts/current");

        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        values!.First().Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task UnauthenticatedRequest_StillGetsCorrelationId()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/current");

        // Even though auth fails (401), the middleware runs first and sets the header
        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        values!.First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MultipleRequests_GenerateDifferentCorrelationIds()
    {
        var client = CreateClient();

        var response1 = await client.GetAsync("/api/v1/workouts/current");
        // Remove any correlation ID that might have been sent back in default headers
        client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        var response2 = await client.GetAsync("/api/v1/workouts/current");

        response1.Headers.TryGetValues("X-Correlation-ID", out var values1).Should().BeTrue();
        response2.Headers.TryGetValues("X-Correlation-ID", out var values2).Should().BeTrue();

        values1!.First().Should().NotBe(values2!.First());
    }
}
