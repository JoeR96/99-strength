using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Responses;
using A2S.Api.Controllers;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Users are provisioned from the JWT on first request; the API exposes only the caller's own
/// record and a Hevy key store that never echoes the key back.
/// </summary>
[Collection("Integration")]
public class UsersControllerTests
{
    private readonly HttpClient _anonymous;
    private readonly TestWebApplicationFactory<Program> _factory;

    public UsersControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsProvisionedUser()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var email = $"{userId}@example.com";
        var client = _factory.CreateAuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Id.Should().Be(userId);
        user.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetUserById_OwnId_ReturnsOk()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateAuthenticatedClient(userId);

        var response = await client.GetAsync($"/api/v1/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserResponse>())!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_SomeoneElsesId_ReturnsForbidden()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.GetAsync($"/api/v1/users/user_{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserById_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _anonymous.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_EndpointNoLongerExists()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/users", new { email = "x@example.com", name = "X" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task HevyApiKey_Status_BeforeAnyKey_IsEmpty()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/users/me/hevy-api-key");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<HevyApiKeyStatusResponse>();
        status.Should().Be(new HevyApiKeyStatusResponse(false, null));
    }

    [Fact]
    public async Task HevyApiKey_Put_ThenStatus_ReportsLastFourOnly()
    {
        var client = _factory.CreateAuthenticatedClient();
        const string key = "hevy-live-key-ABCD-7781";

        var put = await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = key });
        var status = await client.GetAsync("/api/v1/users/me/hevy-api-key");

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await status.Content.ReadAsStringAsync();
        body.Should().NotContain(key);
        body.Should().NotContain("ABCD");
        var parsed = await status.Content.ReadFromJsonAsync<HevyApiKeyStatusResponse>();
        parsed.Should().Be(new HevyApiKeyStatusResponse(true, "7781"));
    }

    [Fact]
    public async Task HevyApiKey_Put_WithBlank_Clears()
    {
        var client = _factory.CreateAuthenticatedClient();
        await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = "hevy-live-key-0001" });

        var clear = await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = (string?)null });
        var status = await client.GetFromJsonAsync<HevyApiKeyStatusResponse>("/api/v1/users/me/hevy-api-key");

        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);
        status!.HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task HevyApiKey_Delete_Clears()
    {
        var client = _factory.CreateAuthenticatedClient();
        await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = "hevy-live-key-0002" });

        var delete = await client.DeleteAsync("/api/v1/users/me/hevy-api-key");
        var status = await client.GetFromJsonAsync<HevyApiKeyStatusResponse>("/api/v1/users/me/hevy-api-key");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        status!.HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task HevyApiKey_IsStoredEncrypted_NotAsPlaintext()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateAuthenticatedClient(userId);
        const string key = "hevy-live-key-PLAIN-4455";
        await client.PutAsJsonAsync("/api/v1/users/me/hevy-api-key", new { apiKey = key });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2S.Infrastructure.Persistence.A2SDbContext>();
        var stored = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.Users.Where(u => u.Id == new A2S.Domain.Common.UserId(userId)));

        stored.HevyApiKeyProtected.Should().NotBeNull();
        stored.HevyApiKeyProtected.Should().NotContain("PLAIN");
        stored.HevyApiKeyProtected.Should().NotBe(key);
    }
}
