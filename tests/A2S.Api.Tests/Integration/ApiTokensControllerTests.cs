using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using A2S.Api.Controllers;
using A2S.Api.Services;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Personal API tokens end to end against Postgres: mint with a Clerk session, call the allow-listed
/// endpoints with the token, and be refused everywhere else.
/// The shared test host swaps <c>ICurrentUserService</c> for a static stand-in, which would hide
/// whether a token really resolves to its user. These tests put the real service back, so "who is
/// calling" comes from the request's own credentials exactly as it does in production.
/// </summary>
[Collection("Integration")]
public class ApiTokensControllerTests
{
    private const string TokensUrl = "/api/v1/users/me/api-tokens";
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private static readonly ConditionalWeakTable<TestWebApplicationFactory<Program>, WebApplicationFactory<Program>> RealUserHosts = new();

    private readonly WebApplicationFactory<Program> _host;

    public ApiTokensControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _host = RealUserHosts.GetValue(factory, parent => parent.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentUserService>();
                services.AddScoped<ICurrentUserService, CurrentUserService>();
            })));
    }

    private sealed record Session(HttpClient Client, string UserId, string Email);

    private Session ClerkSession()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var email = $"{userId}@example.com";
        var client = _host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestWebApplicationFactory<Program>.GenerateJwtToken(userId, email));
        return new Session(client, userId, email);
    }

    private HttpClient TokenClient(string token)
    {
        var client = _host.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        return client;
    }

    private static async Task<CreatedApiTokenDto> CreateToken(HttpClient clerk, string name = "Terran CC")
    {
        var response = await clerk.PostAsJsonAsync(TokensUrl, new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedApiTokenDto>())!;
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
    public async Task Create_Returns201_WithTheTokenOnce_AndStoresOnlyItsHash()
    {
        var clerk = ClerkSession();

        var response = await clerk.Client.PostAsJsonAsync(TokensUrl, new { name = "Terran CC" });
        var json = await response.Content.ReadAsStringAsync();
        var created = (await response.Content.ReadFromJsonAsync<CreatedApiTokenDto>())!;

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        json.Should().Contain("\"id\"").And.Contain("\"name\":\"Terran CC\"").And.Contain("\"prefix\"")
            .And.Contain("\"createdAt\"").And.Contain("\"token\":\"nns_pat_");
        created.Token.Should().MatchRegex("^nns_pat_[A-Za-z0-9_-]{43}$");
        created.Prefix.Should().Be(created.Token[..12]);
        created.Id.Should().NotBeEmpty();

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var row = await db.Database
            .SqlQuery<string>($"""SELECT CAST(t AS text) AS "Value" FROM "ApiTokens" t WHERE "Id" = {created.Id}""")
            .SingleAsync();
        row.Should().Contain(ApiToken.HashOf(created.Token));
        row.Should().NotContain(created.Token[12..]);
    }

    [Fact]
    public async Task AToken_ReadsAndWritesItsOwnersHabits()
    {
        var clerk = ClerkSession();
        var water = await CreateWater(clerk.Client);
        var stranger = ClerkSession();
        await CreateWater(stranger.Client);
        var token = TokenClient((await CreateToken(clerk.Client)).Token);

        var tap = await token.PostAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}/increment", new { delta = 1, clientTimestamp = T0 });
        var set = await token.PutAsJsonAsync($"/api/v1/habits/{water.Id}/logs/{Day:yyyy-MM-dd}", new { value = 3, clientTimestamp = T0.AddMinutes(1) });
        var todayResponse = await token.GetAsync($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");
        var today = await todayResponse.Content.ReadFromJsonAsync<HabitsTodayDto>();
        var viaClerk = await clerk.Client.GetFromJsonAsync<HabitsTodayDto>($"/api/v1/habits/today?date={Day:yyyy-MM-dd}");

        tap.StatusCode.Should().Be(HttpStatusCode.OK);
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        todayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        today!.Habits.Should().ContainSingle().Which.Id.Should().Be(water.Id);
        today.Habits.Single().Value.Should().Be(3);
        viaClerk.Should().BeEquivalentTo(today);
    }

    [Fact]
    public async Task AToken_ReachesEveryHabitRoute_BodyWeight_PlansToday_AndWhoAmI()
    {
        var clerk = ClerkSession();
        var water = await CreateWater(clerk.Client);
        var token = TokenClient((await CreateToken(clerk.Client)).Token);
        var planTodayViaClerk = await clerk.Client.GetAsync("/api/v1/plans/today");

        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["GET habits"] = await token.GetAsync("/api/v1/habits"),
            ["POST habits"] = await token.PostAsJsonAsync("/api/v1/habits", new { name = "Walk", type = "Counter", target = 2, minimum = 1, unit = "walks", order = 1 }),
            ["PUT habit"] = await token.PutAsJsonAsync($"/api/v1/habits/{water.Id}", new { name = "Water", target = 6, minimum = 4, unit = "bottles", order = 0, icon = "droplet" }),
            ["GET history"] = await token.GetAsync($"/api/v1/habits/history?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}"),
            ["POST import"] = await token.PostAsJsonAsync("/api/v1/habits/import", new { habits = Array.Empty<object>() }),
            ["POST archive"] = await token.PostAsync($"/api/v1/habits/{water.Id}/archive", null),
            ["POST unarchive"] = await token.PostAsync($"/api/v1/habits/{water.Id}/unarchive", null),
            ["PUT body-weight"] = await token.PutAsJsonAsync($"/api/v1/body-weight/{Day:yyyy-MM-dd}", new { kg = 94.6m }),
            ["GET trend"] = await token.GetAsync($"/api/v1/body-weight/trend?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}"),
            ["GET whoami"] = await token.GetAsync($"{TokensUrl}/whoami")
        };
        var planToday = await token.GetAsync("/api/v1/plans/today");

        foreach (var (route, response) in responses)
        {
            ((int)response.StatusCode).Should().BeInRange(200, 299, $"{route} is on the token allow-list");
        }

        planToday.StatusCode.Should().Be(planTodayViaClerk.StatusCode, "a token sees plans/today exactly as its owner's session does");
        planToday.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
        var trend = await clerk.Client.GetFromJsonAsync<WeightTrendDto>($"/api/v1/body-weight/trend?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}");
        trend!.Points.Should().ContainSingle().Which.Kg.Should().Be(94.6m);
    }

    [Fact]
    public async Task WhoAmI_NamesTheUserAndTheToken_OrNoTokenForAClerkSession()
    {
        var clerk = ClerkSession();
        var token = TokenClient((await CreateToken(clerk.Client, "Terran CC")).Token);

        var viaToken = await token.GetFromJsonAsync<WhoAmIResponse>($"{TokensUrl}/whoami");
        var viaClerkJson = await clerk.Client.GetStringAsync($"{TokensUrl}/whoami");
        var viaClerk = await clerk.Client.GetFromJsonAsync<WhoAmIResponse>($"{TokensUrl}/whoami");

        viaToken.Should().Be(new WhoAmIResponse(clerk.UserId, clerk.Email, "Terran CC"));
        viaClerk.Should().Be(new WhoAmIResponse(clerk.UserId, clerk.Email, null));
        viaClerkJson.Should().Contain("\"userId\"").And.Contain("\"email\"").And.Contain("\"tokenName\":null");
    }

    [Fact]
    public async Task AToken_IsForbiddenEverywhereOffTheAllowList()
    {
        var clerk = ClerkSession();
        var created = await CreateToken(clerk.Client);
        var token = TokenClient(created.Token);

        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["GET hevy key"] = await token.GetAsync("/api/v1/users/me/hevy-api-key"),
            ["PUT hevy key"] = await token.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = "nope" }),
            ["GET users/me"] = await token.GetAsync("/api/v1/users/me"),
            ["POST token"] = await token.PostAsJsonAsync(TokensUrl, new { name = "Minted by a token" }),
            ["GET tokens"] = await token.GetAsync(TokensUrl),
            ["DELETE token"] = await token.DeleteAsync($"{TokensUrl}/{created.Id}"),
            ["GET plans/active"] = await token.GetAsync("/api/v1/plans/active"),
            ["GET plans/calendar"] = await token.GetAsync("/api/v1/plans/calendar?year=2026&month=11"),
            ["POST plans"] = await token.PostAsJsonAsync("/api/v1/plans", new { }),
            ["GET workouts"] = await token.GetAsync("/api/v1/workouts"),
            ["GET meals"] = await token.GetAsync("/api/v1/meals"),
            ["GET push"] = await token.GetAsync("/api/v1/push/subscriptions"),
            ["GET health"] = await token.GetAsync("/health"),
            ["GET nowhere"] = await token.GetAsync("/api/v1/no-such-route")
        };

        foreach (var (route, response) in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{route} is not on the token allow-list");
        }

        var tokens = await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl);
        tokens.Should().ContainSingle("the token could neither mint a second token nor revoke itself")
            .Which.RevokedAt.Should().BeNull();
        (await token.GetAsync("/api/v1/habits/today")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_Returns204_AndTheTokenStopsWorkingImmediately()
    {
        var clerk = ClerkSession();
        var created = await CreateToken(clerk.Client);
        var token = TokenClient(created.Token);
        (await token.GetAsync("/api/v1/habits/today")).StatusCode.Should().Be(HttpStatusCode.OK);

        var revoke = await clerk.Client.DeleteAsync($"{TokensUrl}/{created.Id}");
        var afterRevoke = await token.GetAsync("/api/v1/habits/today");
        var whoami = await token.GetAsync($"{TokensUrl}/whoami");
        var offList = await token.GetAsync("/api/v1/users/me/hevy-api-key");
        var again = await clerk.Client.DeleteAsync($"{TokensUrl}/{created.Id}");
        var tokens = await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl);

        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        whoami.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        offList.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tokens.Should().ContainSingle().Which.RevokedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("nns_pat_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("nns_pat_garbage")]
    [InlineData("nns_pat_")]
    public async Task AGarbageToken_IsUnauthorized(string garbage)
    {
        var token = TokenClient(garbage);

        (await token.GetAsync("/api/v1/habits/today")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await token.GetAsync($"{TokensUrl}/whoami")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await token.GetAsync("/api/v1/users/me/hevy-api-key")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithoutCredentials_EveryTokenRouteIsUnauthorized()
    {
        var anonymous = _host.CreateClient();

        (await anonymous.GetAsync(TokensUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync(TokensUrl, new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"{TokensUrl}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{TokensUrl}/whoami")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShowsLastUsedOnceTheTokenHasBeenUsed_AndNeverASecret()
    {
        var clerk = ClerkSession();
        var created = await CreateToken(clerk.Client);

        var before = await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl);
        var calledAt = DateTimeOffset.UtcNow;
        (await TokenClient(created.Token).GetAsync("/api/v1/habits/today")).StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await clerk.Client.GetStringAsync(TokensUrl);
        var after = await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl);

        before.Should().ContainSingle().Which.LastUsedAt.Should().BeNull();
        var listed = after.Should().ContainSingle().Subject;
        listed.Id.Should().Be(created.Id);
        listed.Name.Should().Be("Terran CC");
        listed.Prefix.Should().Be(created.Prefix);
        listed.RevokedAt.Should().BeNull();
        listed.LastUsedAt.Should().NotBeNull();
        listed.LastUsedAt!.Value.Should().BeCloseTo(calledAt, TimeSpan.FromMinutes(1));
        json.Should().Contain("\"lastUsedAt\"").And.Contain("\"revokedAt\":null").And.Contain("\"createdAt\"");
        json.Should().NotContain(created.Token[12..]);
        json.Should().NotContain(ApiToken.HashOf(created.Token));
        json.Should().NotContain("\"token\"").And.NotContain("hash", "the list carries no secret material");
    }

    [Fact]
    public async Task List_IsNewestFirst_AndOnlyTheCallersOwn()
    {
        var clerk = ClerkSession();
        var other = ClerkSession();
        await CreateToken(other.Client, "Theirs");
        var first = await CreateToken(clerk.Client, "First");
        var second = await CreateToken(clerk.Client, "Second");

        var tokens = await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl);

        tokens!.Select(t => t.Id).Should().Equal(second.Id, first.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task Create_WithABadName_Is400(string name)
    {
        var clerk = ClerkSession();

        var response = await clerk.Client.PostAsJsonAsync(TokensUrl, new { name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await clerk.Client.GetFromJsonAsync<List<ApiTokenDto>>(TokensUrl)).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithoutAName_Is400()
    {
        var clerk = ClerkSession();

        var response = await clerk.Client.PostAsJsonAsync(TokensUrl, new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AllowsTenActiveTokens_RefusesTheEleventh_UntilOneIsRevoked()
    {
        var clerk = ClerkSession();
        var created = new List<CreatedApiTokenDto>();
        for (var i = 1; i <= 10; i++)
        {
            created.Add(await CreateToken(clerk.Client, $"Token {i}"));
        }

        var eleventh = await clerk.Client.PostAsJsonAsync(TokensUrl, new { name = "Token 11" });
        await clerk.Client.DeleteAsync($"{TokensUrl}/{created[0].Id}");
        var afterRevoke = await clerk.Client.PostAsJsonAsync(TokensUrl, new { name = "Token 11" });

        eleventh.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await eleventh.Content.ReadAsStringAsync()).Should().Contain("at most 10");
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Revoke_SomeoneElsesToken_Is404_AndItKeepsWorking()
    {
        var owner = ClerkSession();
        var created = await CreateToken(owner.Client);
        var other = ClerkSession();

        var response = await other.Client.DeleteAsync($"{TokensUrl}/{created.Id}");
        var unknown = await other.Client.DeleteAsync($"{TokensUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TokenClient(created.Token).GetAsync("/api/v1/habits/today")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheToken_NeverReachesALogEvent()
    {
        var clerk = ClerkSession();
        var sink = _host.Services.GetRequiredService<CapturingLogSink>();
        sink.Clear();

        var created = await CreateToken(clerk.Client);
        var token = TokenClient(created.Token);
        await token.GetAsync("/api/v1/habits/today");
        await token.GetAsync("/api/v1/users/me/hevy-api-key");
        await clerk.Client.DeleteAsync($"{TokensUrl}/{created.Id}");
        await token.GetAsync("/api/v1/habits/today");
        const string garbage = "nns_pat_ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";
        await TokenClient(garbage).GetAsync("/api/v1/habits/today");

        var secret = created.Token[12..];
        sink.Events.Should().NotBeEmpty("request logging must be flowing through the capturing sink");
        sink.AllText().Should().NotContain(t => t.Contains(secret, StringComparison.Ordinal));
        sink.AllText().Should().NotContain(t => t.Contains(garbage, StringComparison.Ordinal));
    }
}
