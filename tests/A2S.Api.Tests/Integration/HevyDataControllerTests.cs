using System.Net;
using System.Net.Http.Json;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class HevyDataControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyDataControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task GetWorkouts_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/data/workouts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWorkouts_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/data/workouts");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("API key is required");
    }

    [Fact]
    public async Task GetExerciseHistory_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/data/exercises/test-id/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExerciseHistory_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/data/exercises/test-id/history");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("API key is required");
    }

    [Fact]
    public async Task GetExerciseHistory_WithEmptyTemplateId_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.GetAsync("/api/v1/hevy/data/exercises/%20/history");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
