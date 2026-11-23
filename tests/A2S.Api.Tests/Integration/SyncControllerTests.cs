using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using A2S.Api.Services;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Sync;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Keeping clients in sync over HTTP: <c>/sync/version</c> with ETag/304, ETag 304 on habits, and the SSE stream
/// that reports a habit increment as <c>changed</c> with area <c>habits</c>. Runs with the real
/// <c>CurrentUserService</c> (each request's own credentials), because the stream and the write overlap.
/// </summary>
[Collection("Integration")]
public class SyncControllerTests
{
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StreamTimeout = TimeSpan.FromSeconds(15);

    private static readonly ConditionalWeakTable<TestWebApplicationFactory<Program>, WebApplicationFactory<Program>> RealUserHosts = new();

    private readonly WebApplicationFactory<Program> _host;

    public SyncControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _host = RealUserHosts.GetValue(factory, parent => parent.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Sync:KeepAliveSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentUserService>();
                services.AddScoped<ICurrentUserService, CurrentUserService>();
            });
        }));
    }

    private HttpClient Client(string userId)
    {
        var client = _host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory<Program>.GenerateJwtToken(userId, $"{userId}@example.com"));
        return client;
    }

    private static string NewUser() => $"user_{Guid.NewGuid():N}";

    private static async Task<HabitDto> CreateWater(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Water", type = "Counter", target = 6, minimum = 4, unit = "bottles", order = 0, icon = "droplet"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<HabitDto>())!;
    }

    private static Task<HttpResponseMessage> Increment(HttpClient client, HabitDto habit, int minute = 0) =>
        client.PostAsJsonAsync($"/api/v1/habits/{habit.Id}/logs/{Day:yyyy-MM-dd}/increment", new { delta = 1, clientTimestamp = T0.AddMinutes(minute) });

    private static async Task<HttpResponseMessage> GetWithETag(HttpClient client, string url, string? etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Version_StartsAtZero_BumpsOnHabitWrites_AndAnswers304WhileUnchanged()
    {
        var client = Client(NewUser());

        var empty = await client.GetFromJsonAsync<SyncVersionDto>("/api/v1/sync/version");
        empty!.Version.Should().Be(0);
        empty.Areas.Keys.Should().Equal("habits", "plan", "workout", "cardio", "mobility");

        var water = await CreateWater(client);
        var first = await client.GetAsync("/api/v1/sync/version");
        first.Headers.ETag!.Tag.Should().Be("\"1\"");
        var body = (await first.Content.ReadFromJsonAsync<SyncVersionDto>())!;
        body.Version.Should().Be(1);
        body.Areas["habits"].Should().Be(1);
        body.Areas["plan"].Should().Be(0);
        (await first.Content.ReadAsStringAsync()).Should().Contain("\"version\":1").And.Contain("\"areas\":{\"habits\":1");

        (await GetWithETag(client, "/api/v1/sync/version", "\"1\"")).StatusCode.Should().Be(HttpStatusCode.NotModified);

        (await Increment(client, water)).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await GetWithETag(client, "/api/v1/sync/version", "\"1\"");
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        (await after.Content.ReadFromJsonAsync<SyncVersionDto>())!.Areas["habits"].Should().Be(2);
    }

    [Fact]
    public async Task AFailedWrite_DoesNotBump()
    {
        var client = Client(NewUser());

        var missing = await client.PostAsJsonAsync($"/api/v1/habits/{Guid.NewGuid()}/logs/{Day:yyyy-MM-dd}/increment", new { delta = 1, clientTimestamp = T0 });

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<SyncVersionDto>("/api/v1/sync/version"))!.Version.Should().Be(0);
    }

    [Fact]
    public async Task HabitsToday_AnswersNotModified_UntilAHabitChanges()
    {
        var client = Client(NewUser());
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/today?date={Day:yyyy-MM-dd}";

        var first = await client.GetAsync(url);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = first.Headers.ETag!.Tag;

        var again = await GetWithETag(client, url, etag);
        again.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await again.Content.ReadAsStringAsync()).Should().BeEmpty();

        // A different query is a different answer.
        (await GetWithETag(client, $"/api/v1/habits/today?date={Day.AddDays(1):yyyy-MM-dd}", etag)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Increment(client, water);
        var changed = await GetWithETag(client, url, etag);
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        changed.Headers.ETag!.Tag.Should().NotBe(etag);
        (await changed.Content.ReadFromJsonAsync<HabitsTodayDto>())!.Habits.Single().Value.Should().Be(1);

        var list = await client.GetAsync("/api/v1/habits");
        (await GetWithETag(client, "/api/v1/habits", list.Headers.ETag!.Tag)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task PlansToday_AnswersNotModified_WhileNothingChanged()
    {
        var client = Client(NewUser());

        var first = await client.GetAsync("/api/v1/plans/today");
        if (first.StatusCode != HttpStatusCode.OK)
        {
            // No plan: whatever the endpoint answers, it carries no tag and stays as it was.
            first.Headers.ETag.Should().BeNull();
            return;
        }

        (await GetWithETag(client, "/api/v1/plans/today", first.Headers.ETag!.Tag)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Events_SaysHelloThenReportsAHabitIncrementAsChangedHabits()
    {
        var userId = NewUser();
        var writer = Client(userId);
        var water = await CreateWater(writer);

        using var cts = new CancellationTokenSource(StreamTimeout);
        var reader = Client(userId);
        using var response = await reader.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/sync/events"),
            HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.GetValues("X-Accel-Buffering").Should().Equal("no");

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var lines = new StreamReader(stream);

        var hello = await NextEventAsync(lines, cts.Token);
        hello.Name.Should().Be("hello");
        hello.Id.Should().Be("1");
        hello.Data.GetProperty("version").GetInt64().Should().Be(1);

        (await Increment(writer, water)).StatusCode.Should().Be(HttpStatusCode.OK);

        var changed = await NextEventAsync(lines, cts.Token);
        changed.Name.Should().Be("changed");
        changed.Id.Should().Be("2");
        changed.Data.GetProperty("version").GetInt64().Should().Be(2);
        changed.Data.GetProperty("areas").EnumerateArray().Select(a => a.GetString()).Should().Equal("habits");
    }

    [Fact]
    public async Task Events_WithAnOlderLastEventId_SendsWhatWasMissedStraightAway()
    {
        var userId = NewUser();
        var client = Client(userId);
        var water = await CreateWater(client);
        await Increment(client, water);

        using var cts = new CancellationTokenSource(StreamTimeout);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sync/events");
        request.Headers.TryAddWithoutValidation("Last-Event-ID", "1");
        using var response = await Client(userId).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var lines = new StreamReader(stream);

        (await NextEventAsync(lines, cts.Token)).Name.Should().Be("hello");
        var missed = await NextEventAsync(lines, cts.Token);
        missed.Name.Should().Be("changed");
        missed.Data.GetProperty("version").GetInt64().Should().Be(2);
        missed.Data.GetProperty("areas").EnumerateArray().Select(a => a.GetString()).Should().Equal("habits");
    }

    [Fact]
    public async Task Events_SendsAKeepAliveCommentWhenIdle()
    {
        using var cts = new CancellationTokenSource(StreamTimeout);
        using var response = await Client(NewUser()).SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/sync/events"),
            HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var lines = new StreamReader(stream);

        (await NextEventAsync(lines, cts.Token)).Name.Should().Be("hello");
        string? line;
        do
        {
            line = await lines.ReadLineAsync(cts.Token);
        }
        while (line is not null && line != ":keepalive");

        line.Should().Be(":keepalive");
    }

    [Fact]
    public async Task Events_WithoutCredentials_Is401()
    {
        var response = await _host.CreateClient().GetAsync("/api/v1/sync/events");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record SseEvent(string? Id, string? Name, JsonElement Data);

    /// <summary>The next event (comments and the retry hint skipped).</summary>
    private static async Task<SseEvent> NextEventAsync(StreamReader lines, CancellationToken ct)
    {
        string? id = null, name = null, data = null;
        while (true)
        {
            var line = await lines.ReadLineAsync(ct) ?? throw new EndOfStreamException("The stream closed");
            if (line.Length == 0)
            {
                if (data is not null)
                {
                    return new SseEvent(id, name, JsonDocument.Parse(data).RootElement.Clone());
                }

                continue;
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..].TrimStart();
            switch (field)
            {
                case "id": id = value; break;
                case "event": name = value; break;
                case "data": data = value; break;
            }
        }
    }
}
