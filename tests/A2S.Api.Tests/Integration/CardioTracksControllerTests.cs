using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.CardioTracks;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>Cardio tracks end to end over HTTP and a real database: start, log, step up, undo, scoping.</summary>
[Collection("Integration")]
public class CardioTracksControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public CardioTracksControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<CardioTrackDto> StartAsync(HttpClient client, string template)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cardio-tracks", new CreateCardioTrackCommand(template));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CardioTrackDto>())!;
    }

    private static LogCardioTrackSessionRequest Hard(int minutes, string key) =>
        new(new CardioLogDto(minutes * 60, 165, null, null, null, minutes, Effort: EffortRating.Brutal, Floors: 40), ClientKey: key);

    [Fact]
    public async Task StartLogStepUpAndUndo()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var templates = await client.GetFromJsonAsync<List<CardioTrackTemplateDto>>("/api/v1/cardio-tracks/templates");
        templates!.Select(t => t.Key).Should().Contain(CardioTrackTemplates.StairFinisher);

        var track = await StartAsync(client, CardioTrackTemplates.StairFinisher);
        track.Prescription.Title.Should().Be("5 min continuous @ level 6");

        var first = await client.PostAsJsonAsync($"/api/v1/cardio-tracks/{track.Id}/sessions", Hard(5, "a"));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var retry = await (await client.PostAsJsonAsync($"/api/v1/cardio-tracks/{track.Id}/sessions", Hard(5, "a")))
            .Content.ReadFromJsonAsync<TrackEvaluationDto>();
        retry!.AlreadyRecorded.Should().BeTrue();

        var second = await (await client.PostAsJsonAsync($"/api/v1/cardio-tracks/{track.Id}/sessions", Hard(5, "b")))
            .Content.ReadFromJsonAsync<TrackEvaluationDto>();
        second!.Outcome.Should().Be(TrackOutcome.SteppedUp);
        second.Track.Prescription.Title.Should().Be("6 min continuous @ level 6");

        var detail = await client.GetFromJsonAsync<CardioTrackDetailDto>($"/api/v1/cardio-tracks/{track.Id}");
        detail!.Sessions.Should().HaveCount(2);
        detail.Audit.Should().HaveCount(2);
        detail.Audit[^1].After.Should().Be("6 min continuous @ level 6");
        detail.Chart[^1].Step.Should().Be(1);

        var undone = await (await client.PostAsync($"/api/v1/cardio-tracks/{track.Id}/undo", null)).Content.ReadFromJsonAsync<CardioTrackDto>();
        undone!.Prescription.Title.Should().Be("5 min continuous @ level 6");

        var today = await client.GetFromJsonAsync<TodayCardioDto>("/api/v1/cardio-tracks/today");
        today!.Tracks.Should().ContainSingle(t => t.Id == track.Id);
        today.HighIntensityMinutesThisWeek.Should().Be(5);
    }

    [Fact]
    public async Task PauseResumeBenchmarkAndZones()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var track = await StartAsync(client, CardioTrackTemplates.WalkBuilder);

        var paused = await (await client.PostAsync($"/api/v1/cardio-tracks/{track.Id}/pause", null)).Content.ReadFromJsonAsync<CardioTrackDto>();
        paused!.Status.Should().Be(CardioTrackStatus.Paused);
        var resumed = await (await client.PostAsync($"/api/v1/cardio-tracks/{track.Id}/resume", null)).Content.ReadFromJsonAsync<CardioTrackDto>();
        resumed!.Status.Should().Be(CardioTrackStatus.Active);

        var benchmark = await (await client.PostAsJsonAsync($"/api/v1/cardio-tracks/{track.Id}/benchmarks", new RecordBenchmarkRequest(128)))
            .Content.ReadFromJsonAsync<BenchmarkResultDto>();
        benchmark!.IsPersonalBest.Should().BeTrue();

        var zones = await (await client.PutAsJsonAsync("/api/v1/cardio-tracks/heart-rate-zones", new SetHeartRateZonesRequest(190, null)))
            .Content.ReadFromJsonAsync<HeartRateZonesDto>();
        zones!.Bands[1].UpperBpm.Should().Be(133);
        (await client.GetFromJsonAsync<HeartRateZonesDto>("/api/v1/cardio-tracks/heart-rate-zones"))!.MaxHeartRate.Should().Be(190);
    }

    [Fact]
    public async Task AnotherUsersTrack_IsNotFound_AndAnonymousIsRefused()
    {
        var owner = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var track = await StartAsync(owner, CardioTrackTemplates.WalkBuilder);
        // The test current-user service is ambient: create the stranger after the owner's calls.
        var stranger = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        (await stranger.GetAsync($"/api/v1/cardio-tracks/{track.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetFromJsonAsync<List<CardioTrackDto>>("/api/v1/cardio-tracks"))!.Should().BeEmpty();
        (await _factory.CreateClient().GetAsync("/api/v1/cardio-tracks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithUnknownTemplate_IsABadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/cardio-tracks", new CreateCardioTrackCommand("nope"));

        ((int)response.StatusCode).Should().BeOneOf(400, 422);
    }

    [Fact]
    public async Task Preferences_PushRoutinesToHevy_DefaultsOn_AndCanBeTurnedOff()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        await client.GetAsync("/api/v1/users/me");

        var before = await client.GetFromJsonAsync<A2S.Application.Commands.Users.UserPreferencesDto>("/api/v1/users/me/preferences");
        var put = await client.PutAsJsonAsync("/api/v1/users/me/preferences", new A2S.Application.Commands.Users.SetUserPreferencesCommand(false));
        var after = await client.GetFromJsonAsync<A2S.Application.Commands.Users.UserPreferencesDto>("/api/v1/users/me/preferences");

        before!.PushRoutinesToHevy.Should().BeTrue();
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        after!.PushRoutinesToHevy.Should().BeFalse();
    }
}
