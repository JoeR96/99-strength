using System.Net;
using System.Net.Http.Json;
using A2S.Application.DTOs;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Web push over HTTP. The VAPID key test needs no tables; the subscription tests need the
/// PushSubscriptions table (the integrator's AddPushSubscriptions migration).
/// </summary>
[Collection("Integration")]
public class PushControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public PushControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VapidPublicKey_IsTheConfiguredOne()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var key = await client.GetFromJsonAsync<VapidPublicKeyDto>("/api/v1/push/vapid-public-key");

        key!.PublicKey.Should().Be(_factory.VapidKeys.PublicKey);
    }

    [Fact]
    public async Task Subscribe_List_Resubscribe_Unsubscribe()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var endpoint = $"https://push.example.com/{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/push/subscribe", new { endpoint, p256dh = "p1", auth = "a1", userAgent = "Safari" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await created.Content.ReadFromJsonAsync<PushSubscriptionDto>())!;
        first.Endpoint.Should().Be(endpoint);
        first.SessionStartTime.Should().Be(new TimeOnly(17, 30));

        var again = await client.PostAsJsonAsync("/api/v1/push/subscribe", new { endpoint, p256dh = "p2", auth = "a2", sessionStartTime = "06:45:00" });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await again.Content.ReadFromJsonAsync<PushSubscriptionDto>())!;
        second.Id.Should().Be(first.Id);
        second.SessionStartTime.Should().Be(new TimeOnly(6, 45));

        var list = await client.GetFromJsonAsync<List<PushSubscriptionDto>>("/api/v1/push/subscriptions");
        list.Should().ContainSingle(s => s.Endpoint == endpoint);

        var remove = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/subscribe") { Content = JsonContent.Create(new { endpoint }) });
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<PushSubscriptionDto>>("/api/v1/push/subscriptions")).Should().BeEmpty();

        var removeAgain = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/subscribe") { Content = JsonContent.Create(new { endpoint }) });
        removeAgain.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Subscribe_WithAnInsecureEndpoint_IsRejected()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/push/subscribe", new { endpoint = "http://plain.example.com/x", p256dh = "p", auth = "a" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task WithoutAuth_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/push/vapid-public-key");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
