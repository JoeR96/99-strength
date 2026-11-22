using System.Net;
using System.Net.Http.Json;
using A2S.Application.DTOs;
using A2S.Application.Readiness;
using A2S.Domain.Aggregates.Cardio;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>Readiness, cardio start gates and the phase heads-up over HTTP against Postgres.</summary>
[Collection("Integration")]
public class CoachSignalsControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public CoachSignalsControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Readiness_RedThenKeepFinisher_ThenRead()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var red = await client.PutAsJsonAsync("/api/v1/readiness/2026-11-22", new { colour = "Red" });
        var keep = await client.PutAsJsonAsync("/api/v1/readiness/2026-11-22/finisher", new { keep = true });
        var read = await client.GetFromJsonAsync<ReadinessDto>("/api/v1/readiness?date=2026-11-22");
        var other = await client.GetFromJsonAsync<ReadinessDto>("/api/v1/readiness?date=2026-11-21");

        red.StatusCode.Should().Be(HttpStatusCode.OK);
        (await red.Content.ReadFromJsonAsync<ReadinessDto>())!.FinisherSkipped.Should().BeTrue();
        keep.StatusCode.Should().Be(HttpStatusCode.OK);
        read!.FinisherSkipped.Should().BeFalse();
        read.SkipHardCardio.Should().BeTrue();
        other!.Colour.Should().BeNull();
    }

    [Fact]
    public async Task Readiness_KeepFinisherOnAGreenDay_IsUnprocessable()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        await client.PutAsJsonAsync("/api/v1/readiness/2026-11-22", new { colour = "Green" });

        var keep = await client.PutAsJsonAsync("/api/v1/readiness/2026-11-22/finisher", new { keep = true });

        keep.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GatedWalkToRun_ShowsGateProgress_AndStarts()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var created = await client.PostAsJsonAsync("/api/v1/cardio-tracks", new { templateKey = CardioTrackTemplates.WalkToRun, withGate = true });
        var track = (await created.Content.ReadFromJsonAsync<CardioTrackDto>())!;
        var list = await client.GetFromJsonAsync<List<CardioTrackDto>>("/api/v1/cardio-tracks");
        var today = await client.GetFromJsonAsync<TodayCardioDto>("/api/v1/cardio-tracks/today");
        var start = await client.PostAsync($"/api/v1/cardio-tracks/{track.Id}/start", null);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        track.Status.Should().Be(CardioTrackStatus.Gated);
        list!.Single().Gate!.Conditions.Should().HaveCount(3);
        today!.Tracks.Should().BeEmpty();
        today.Gated!.Single().Gate!.AllMet.Should().BeFalse();
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        (await start.Content.ReadFromJsonAsync<CardioTrackDto>())!.Status.Should().Be(CardioTrackStatus.Active);
    }

    [Fact]
    public async Task PhaseHeadsUp_WithoutAPlan_IsNull()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/v1/plans/phase-heads-up");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("{\"headsUp\":null}");
    }
}
