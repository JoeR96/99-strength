using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.Push;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>The finisher wizard's schedule endpoints over HTTP (Clerk-authenticated, per user).</summary>
[Collection("Integration")]
public class FinisherSessionsControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public FinisherSessionsControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_Replace_Delete()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var url = "/api/v1/finisher-sessions/gym-abc/schedule";
        var at = DateTimeOffset.UtcNow.AddMinutes(3);

        var first = await client.PostAsJsonAsync(url, new { day = 2, nudges = new[] { new { at, body = "Hard: level 6 for 1 min" } } });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<FinisherScheduleDto>())!.Revision.Should().Be(1);

        var second = await client.PostAsJsonAsync(url, new { day = 2, nudges = new[] { new { at, body = "a" }, new { at = at.AddMinutes(1), body = "b" } } });
        var dto = (await second.Content.ReadFromJsonAsync<FinisherScheduleDto>())!;
        dto.Revision.Should().Be(2);
        dto.Pending.Should().Be(2);

        (await client.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_WithABadDay_IsRejected()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/finisher-sessions/gym-abc/schedule", new { day = 9, nudges = Array.Empty<object>() });

        ((int)response.StatusCode).Should().BeOneOf(400, 422);
    }

    [Fact]
    public async Task WithoutAuth_IsUnauthorized()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/v1/finisher-sessions/gym-abc/schedule");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
