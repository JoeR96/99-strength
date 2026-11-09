using System.Net;
using System.Net.Http.Json;
using A2S.Application.DTOs;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Weigh-ins over HTTP. Needs the BodyWeightEntries table (the integrator's AddHabitsAndBodyWeight migration).
/// Pace is null here because the TrainingPlan-backed checkpoint source is not wired in yet.
/// </summary>
[Collection("Integration")]
public class BodyWeightControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public BodyWeightControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Put_TwiceSameDate_UpdatesTheOneEntry()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var first = await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-09", new { kg = 95.0 });
        var second = await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-09", new { kg = 94.6 });
        var trend = await client.GetFromJsonAsync<WeightTrendDto>("/api/v1/body-weight/trend?from=2026-11-01&to=2026-11-30");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<BodyWeightEntryDto>())!.Kg.Should().Be(94.6m);
        trend!.Points.Should().ContainSingle();
        trend.Points[0].Date.Should().Be(new DateOnly(2026, 11, 9));
        trend.Points[0].Kg.Should().Be(94.6m);
        trend.Points[0].Ewma.Should().Be(94.6m);
        trend.Points[0].MovingAverage7.Should().BeNull();
        trend.Pace.Should().BeNull();
    }

    [Fact]
    public async Task Trend_OrdersPointsByDate()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-11", new { kg = 96 });
        await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-09", new { kg = 95 });
        await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-10", new { kg = 94 });

        var trend = await client.GetFromJsonAsync<WeightTrendDto>("/api/v1/body-weight/trend?from=2026-11-09&to=2026-11-11");

        trend!.Points.Select(p => p.Kg).Should().Equal(95m, 94m, 96m);
        trend.Points.Select(p => p.Ewma).Should().Equal(95m, 94.9m, 95.01m);
    }

    [Fact]
    public async Task Put_OutOfRange_IsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PutAsJsonAsync("/api/v1/body-weight/2026-11-09", new { kg = 12 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithoutAuth_IsUnauthorized()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/body-weight/trend");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
