using System.Net;
using System.Net.Http.Json;
using A2S.Application.Hevy;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Pull and inbox over HTTP. The no-key pull returns before touching the Phase 6 tables; the inbox
/// listing and ignore/assign round trips need the HevyWorkoutSnapshots table (the integrator's
/// migration) and are marked as such in their names.
/// </summary>
[Collection("Integration")]
public class HevyInboxControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyInboxControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Pull_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/hevy/pull", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pull_WithoutAStoredKey_IsSkipped_NotAnError()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsync("/api/v1/hevy/pull", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<PullSummary>();
        summary!.WasSkipped.Should().BeTrue();
        summary.SkippedReason.Should().Be("no Hevy key");
        summary.Pulled.Should().Be(0);
    }

    [Fact]
    public async Task Inbox_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/inbox");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Inbox is empty for a new user (needs the HevyWorkoutSnapshots migration)")]
    public async Task Inbox_ForANewUser_IsEmpty_NeedsMigration()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/v1/hevy/inbox");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<HevyInboxItemDto>>()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Ignoring an unknown inbox workout is 404 (needs the HevyWorkoutSnapshots migration)")]
    public async Task Ignore_UnknownWorkout_IsNotFound_NeedsMigration()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsync("/api/v1/hevy/inbox/not-a-workout/ignore", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
