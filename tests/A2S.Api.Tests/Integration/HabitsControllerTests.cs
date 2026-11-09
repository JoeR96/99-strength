using System.Net;
using System.Net.Http.Json;
using System.Text;
using A2S.Application.Commands.Habits;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Habits over HTTP: create, tap +1/-1, read the day, last-write-wins, idempotent import, auth.
/// Needs the Habits/HabitLogs tables (the integrator's AddHabitsAndBodyWeight migration).
/// </summary>
[Collection("Integration")]
public class HabitsControllerTests
{
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly TestWebApplicationFactory<Program> _factory;

    public HabitsControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<HabitDto> CreateWater(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Water (532ml bottle)", type = "Counter", target = 6, minimum = 4, unit = "bottles", order = 0, icon = "droplet"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<HabitDto>())!;
    }

    [Fact]
    public async Task Create_IncrementTwice_TodayShowsTwoOfSix_Decrement_OneOfSix()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        await client.PostAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment", new { delta = 1, clientTimestamp = T0 });
        var second = await client.PostAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment", new { delta = 1, clientTimestamp = T0.AddMinutes(1) });
        var today = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(2);
        today!.Date.Should().Be(Day);
        var row = today.Habits.Single();
        row.Id.Should().Be(water.Id);
        row.Type.Should().Be(HabitType.Counter);
        row.Value.Should().Be(2);
        row.Target.Should().Be(6);
        row.MetMinimum.Should().BeFalse();
        today.Streak.TodayCounted.Should().BeFalse();

        var down = await client.PostAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment", new { delta = -1, clientTimestamp = T0.AddMinutes(2) });
        var after = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");

        (await down.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(1);
        after!.Habits.Single().Value.Should().Be(1);
    }

    [Fact]
    public async Task Increment_FirstTapOfTheDay_BehavesLikeALaterOne()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";

        // A first -1 creates the log at 0 (never below), and the +1 after it counts from there.
        var firstDown = await client.PostAsJsonAsync(url, new { delta = -1, clientTimestamp = T0 });
        var thenUp = await client.PostAsJsonAsync(url, new { delta = 1, clientTimestamp = T0.AddMinutes(1) });

        firstDown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await firstDown.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(0);
        (await thenUp.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(1);
    }

    [Fact]
    public async Task Increment_DeltaOtherThanPlusOrMinusOne_IsRejected_OnTheFirstTapAndOnLaterOnes()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";

        // The endpoint is the +1 / -1 tap: a bigger step is refused outright, it is not quietly turned into 1.
        var firstOfTheDay = await client.PostAsJsonAsync(url, new { delta = 3, clientTimestamp = T0 });
        var afterRejection = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");
        await client.PostAsJsonAsync(url, new { delta = 1, clientTimestamp = T0.AddMinutes(1) });
        var later = await client.PostAsJsonAsync(url, new { delta = 3, clientTimestamp = T0.AddMinutes(2) });
        var today = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");

        firstOfTheDay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        afterRejection!.Habits.Single().Value.Should().Be(0, "a rejected tap writes nothing");
        later.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        today!.Habits.Single().Value.Should().Be(1);
    }

    /// <summary>Posts a tap with the timestamp text exactly as given, the way a client puts it on the wire.</summary>
    private static async Task<HabitLogDto> Tap(HttpClient client, string url, int delta, string? clientTimestamp)
    {
        var json = clientTimestamp is null
            ? $"{{\"delta\":{delta}}}"
            : $"{{\"delta\":{delta},\"clientTimestamp\":\"{clientTimestamp}\"}}";
        var response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<HabitLogDto>())!;
    }

    [Fact]
    public async Task Increment_SameTapDeliveredTwice_CountsOnce_AndATapWithAnotherTimestampCountsAgain()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";
        // Milliseconds and a trailing Z: what the PWA's `new Date().toISOString()` sends.
        const string tappedAt = "2026-11-09T08:00:00.123Z";

        var first = await Tap(client, url, 1, tappedAt);
        var redelivered = await Tap(client, url, 1, tappedAt);
        var afterRedelivery = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");
        var nextMillisecond = await Tap(client, url, 1, "2026-11-09T08:00:00.124Z");
        var olderFromAnotherDevice = await Tap(client, url, 1, "2026-11-09T07:30:00.000Z");

        first.Value.Should().Be(1);
        redelivered.Value.Should().Be(1, "the same tap arriving again is a duplicate delivery");
        redelivered.UpdatedAtUtc.Should().Be(first.UpdatedAtUtc);
        afterRedelivery!.Habits.Single().Value.Should().Be(1);
        nextMillisecond.Value.Should().Be(2);
        olderFromAnotherDevice.Value.Should().Be(3, "an older tap from another device still counts");
    }

    [Fact]
    public async Task Increment_RedeliveryOfTheLatestTap_IsRecognisedOnAnExistingLog_UpAndDown()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";
        await client.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}", new { value = 4, clientTimestamp = T0 });

        var up = await Tap(client, url, 1, "2026-11-09T09:00:00.001Z");
        var upAgain = await Tap(client, url, 1, "2026-11-09T09:00:00.001Z");
        var down = await Tap(client, url, -1, "2026-11-09T09:00:05.999Z");
        var downAgain = await Tap(client, url, -1, "2026-11-09T09:00:05.999Z");
        // The same instant written with another offset is still the same tap.
        var downOnceMore = await Tap(client, url, -1, "2026-11-09T10:00:05.999+01:00");

        up.Value.Should().Be(5);
        upAgain.Value.Should().Be(5);
        down.Value.Should().Be(4);
        downAgain.Value.Should().Be(4);
        downOnceMore.Value.Should().Be(4);
    }

    [Theory]
    [InlineData("2026-11-09T08:15:00.1234567Z")]   // 100 ns ticks, as a .NET client's DateTimeOffset.UtcNow serialises
    [InlineData("2026-11-09T08:15:00.9999999Z")]   // would move to the next second if anything rounded up
    [InlineData("2026-11-09T08:15:00.123456Z")]    // microseconds, exactly what the column holds
    [InlineData("2026-11-09T08:15:00Z")]           // no fraction at all
    public async Task Increment_Redelivery_IsRecognisedAtWhateverPrecisionTheClientStamps(string tappedAt)
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";

        var first = await Tap(client, url, 1, tappedAt);
        var redelivered = await Tap(client, url, 1, tappedAt);
        var later = await Tap(client, url, 1, "2026-11-09T08:16:00.000Z");
        var laterRedelivered = await Tap(client, url, 1, "2026-11-09T08:16:00.000Z");

        first.Value.Should().Be(1);
        redelivered.Value.Should().Be(1);
        later.Value.Should().Be(2);
        laterRedelivered.Value.Should().Be(2);
    }

    [Fact]
    public async Task Increment_WithoutClientTimestamp_AlwaysCounts()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment";

        var first = await Tap(client, url, 1, null);
        var second = await Tap(client, url, 1, null);
        var third = await Tap(client, url, 1, null);

        first.Value.Should().Be(1);
        second.Value.Should().Be(2, "with no timestamp there is nothing to recognise a duplicate by");
        third.Value.Should().Be(3);
    }

    [Fact]
    public async Task SetLog_OlderClientTimestamp_DoesNotOverwrite()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        var url = $"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}";

        var newer = await client.PutAsJsonAsync(url, new { value = 5, clientTimestamp = T0.AddMinutes(5) });
        var older = await client.PutAsJsonAsync(url, new { value = 2, clientTimestamp = T0 });

        newer.StatusCode.Should().Be(HttpStatusCode.OK);
        older.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await older.Content.ReadFromJsonAsync<HabitLogDto>();
        stored!.Value.Should().Be(5);
        stored.UpdatedAtUtc.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public async Task Today_ReachingMinimum_CountsTheDayAndStartsTheStreak()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        await client.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}", new { value = 4, clientTimestamp = T0 });
        var today = await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");

        var row = today!.Habits.Single();
        row.MetMinimum.Should().BeTrue();
        row.MetTarget.Should().BeFalse();
        today.Streak.Should().Be(new HabitStreakDto(1, 1, Day, true, false));
    }

    [Fact]
    public async Task Import_IsIdempotent()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var body = new
        {
            habits = new[]
            {
                new
                {
                    externalKey = "terran:walks", name = "Walks", type = "Counter", target = 3, minimum = 1, unit = (string?)null, order = 1,
                    logs = new[] { new { date = "2026-11-07", value = 2 }, new { date = "2026-11-08", value = 3 } }
                }
            }
        };

        var first = await client.PostAsJsonAsync("/api/v1/habits/import", body);
        var second = await client.PostAsJsonAsync("/api/v1/habits/import", body);
        var habits = await client.GetFromJsonAsync<List<HabitDto>>("/api/v1/habits");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(1, 0, 2, 0));
        (await second.Content.ReadFromJsonAsync<ImportResult>()).Should().Be(new ImportResult(0, 1, 0, 2));
        habits.Should().ContainSingle(h => h.ExternalKey == "terran:walks");
    }

    [Fact]
    public async Task History_ReturnsValuesInRange()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);
        await client.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/2026-11-08", new { value = 6, clientTimestamp = T0 });
        await client.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/2026-11-09", new { value = 3, clientTimestamp = T0 });
        await client.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/2026-11-10", new { value = 7, clientTimestamp = T0 });

        var history = await client.GetFromJsonAsync<HabitHistoryDto>("/api/v1/habits/history?from=2026-11-08&to=2026-11-09");

        history!.Entries.Select(e => (e.Date, e.Value, e.MetTarget))
            .Should().Equal((new DateOnly(2026, 11, 8), 6m, true), (new DateOnly(2026, 11, 9), 3m, false));
    }

    [Fact]
    public async Task Archive_HidesFromListAndToday()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        var archive = await client.PostAsync($"/api/v1/habits/{water.Id}/archive", null);
        var active = await client.GetFromJsonAsync<List<HabitDto>>("/api/v1/habits");
        var all = await client.GetFromJsonAsync<List<HabitDto>>("/api/v1/habits?includeArchived=true");

        archive.StatusCode.Should().Be(HttpStatusCode.NoContent);
        active.Should().BeEmpty();
        all.Should().ContainSingle(h => h.Id == water.Id && h.Archived);
    }

    [Fact]
    public async Task SomeoneElsesHabit_IsForbidden()
    {
        var owner = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(owner);
        var intruder = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await intruder.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}", new { value = 1, clientTimestamp = T0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_MinimumAboveTarget_IsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/habits", new { name = "Walks", type = "Counter", target = 1, minimum = 3, order = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithoutAuth_IsUnauthorized()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/habits/today");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
