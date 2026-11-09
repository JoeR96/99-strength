using System.Net;
using System.Net.Http.Json;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Proves the Hevy API key never reaches a log event, even with EF sensitive-data logging switched
/// on (the test factory enables it to reproduce the pre-v2 leak), and never comes back from the API.
/// </summary>
[Collection("Integration")]
public class HevyApiKeyLoggingTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyApiKeyLoggingTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SavingAndReadingTheKey_NeverWritesItToAnyLogEvent()
    {
        var client = _factory.CreateAuthenticatedClient();
        var sink = _factory.Services.GetRequiredService<CapturingLogSink>();
        const string secret = "hevy-secret-k3y-ZQ91-never-logged";
        sink.Clear();

        var put = await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = secret });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var status = await client.GetAsync("/api/v1/users/me/hevy-api-key");
        var proxied = await client.GetAsync("/api/v1/hevy/workouts/count");

        var statusBody = await status.Content.ReadAsStringAsync();
        statusBody.Should().NotContain(secret);
        statusBody.Should().Contain("\"hasKey\":true").And.Contain("\"lastFour\":\"gged\"");
        _ = proxied;

        sink.Events.Should().NotBeEmpty("request logging must be flowing through the capturing sink");
        sink.AllText().Should().NotContain(t => t.Contains(secret, StringComparison.Ordinal));
        sink.AllText().Should().NotContain(t => t.Contains("ZQ91", StringComparison.Ordinal));
    }
}
