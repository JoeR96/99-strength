using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Requests;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// The routine push endpoints over HTTP, up to the point where Hevy would be called: anonymous calls
/// are refused and a user without a stored key gets the "add it under Settings" 400. Nothing here
/// reaches Hevy or needs the Phase 6 tables.
/// </summary>
[Collection("Integration")]
public class HevyPushEndpointsTests
{
    private static readonly Guid SomePlan = Guid.Parse("a3333333-3333-3333-3333-333333333333");

    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyPushEndpointsTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PushSession_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/plans/{SomePlan}/sessions/push",
            new PushSessionRequest(new DateOnly(2030, 1, 7), SessionKind.Lift, DayNumber.Day1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PushSession_WithoutAStoredKey_ReturnsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync($"/api/v1/plans/{SomePlan}/sessions/push",
            new PushSessionRequest(new DateOnly(2030, 1, 7), SessionKind.Lift, DayNumber.Day1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hevy API key is required");
    }

    [Fact]
    public async Task PushSession_ForARestDay_IsRejectedByValidation()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync($"/api/v1/plans/{SomePlan}/sessions/push",
            new PushSessionRequest(new DateOnly(2030, 1, 6), SessionKind.Rest));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PushWeek_WithoutAStoredKey_ReturnsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsync($"/api/v1/plans/{SomePlan}/sessions/push-week?date=2030-01-07", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hevy API key is required");
    }
}
