using System.Net;
using System.Net.Http.Json;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class HevySyncControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevySyncControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task SyncRoutineToHevy_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new { WorkoutId = Guid.Parse("a1111111-1111-1111-1111-111111111111"), WeekNumber = 1, DayNumber = 1 };

        var response = await client.PostAsJsonAsync("/api/v1/hevy/sync/routine", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncRoutineToHevy_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();
        var request = new { WorkoutId = Guid.Parse("a1111111-1111-1111-1111-111111111111"), WeekNumber = 1, DayNumber = 1 };

        var response = await client.PostAsJsonAsync("/api/v1/hevy/sync/routine", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Hevy API key is required");
    }

    [Fact]
    public async Task SyncRoutineToHevy_WithNonexistentWorkout_ReturnsNotFoundOrBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");
        var request = new { WorkoutId = Guid.Parse("a2222222-2222-2222-2222-222222222222"), WeekNumber = 1, DayNumber = 1 };

        var response = await client.PostAsJsonAsync("/api/v1/hevy/sync/routine", request);

        // Should be either NotFound (workout doesn't exist) or BadRequest (validation)
        var validStatuses = new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest };
        validStatuses.Should().Contain(response.StatusCode);
    }
}
