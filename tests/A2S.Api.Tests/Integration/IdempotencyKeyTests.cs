using System.Net;
using System.Net.Http.Json;
using A2S.Application.DTOs;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// The offline queue sends each op's id as <c>Idempotency-Key</c>. A replay of a +1 (the phone never heard
/// the first answer) must count once and get the first answer back; reusing a key for another body is 422.
/// </summary>
[Collection("Integration")]
public class IdempotencyKeyTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly TestWebApplicationFactory<Program> _factory;

    public IdempotencyKeyTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<HabitDto> CreateWater(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Water", type = "Counter", target = 6, minimum = 4, unit = "bottles", order = 0, icon = "droplet"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HabitDto>())!;
    }

    private static Task<HttpResponseMessage> Increment(HttpClient client, Guid habitId, string? key, int delta, DateTimeOffset at)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/habits/{habitId}/logs/{Day:yyyy-MM-dd}/increment")
        {
            Content = JsonContent.Create(new { delta, clientTimestamp = at })
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    private static async Task<decimal> TodayValue(HttpClient client) =>
        (await client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}"))!.Habits.Single().Value;

    [Fact]
    public async Task Increment_ReplayedWithTheSameKey_CountsOnce_AndReturnsTheFirstAnswer()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        var first = await Increment(client, water.Id, "op-abc123", 1, T0);
        var replay = await Increment(client, water.Id, "op-abc123", 1, T0);
        var again = await Increment(client, water.Id, "op-abc123", 1, T0);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(1);
        (await again.Content.ReadFromJsonAsync<HabitLogDto>())!.Value.Should().Be(1);
        (await TodayValue(client)).Should().Be(1);
    }

    [Fact]
    public async Task Increment_DifferentKeysOrNoKey_EachCount()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        await Increment(client, water.Id, "op-one", 1, T0);
        await Increment(client, water.Id, "op-two", 1, T0.AddMinutes(1));
        await Increment(client, water.Id, null, 1, T0.AddMinutes(2));

        (await TodayValue(client)).Should().Be(3);
    }

    [Fact]
    public async Task SameKey_DifferentBody_Is422_AndDoesNotRun()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var water = await CreateWater(client);

        await Increment(client, water.Id, "op-reused", 1, T0);
        var reused = await Increment(client, water.Id, "op-reused", 3, T0);

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TodayValue(client)).Should().Be(1);
    }

    [Fact]
    public async Task Keys_AreScopedToTheUser()
    {
        var joe = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var joeWater = await CreateWater(joe);
        await Increment(joe, joeWater.Id, "op-shared", 1, T0);

        var ann = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var annWater = await CreateWater(ann);
        var annTap = await Increment(ann, annWater.Id, "op-shared", 1, T0);

        annTap.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TodayValue(ann)).Should().Be(1);
    }
}
