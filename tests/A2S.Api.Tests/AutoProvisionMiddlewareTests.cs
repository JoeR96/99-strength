using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using A2S.Api.Controllers;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for the AutoProvisionUserMiddleware.
/// </summary>
public class AutoProvisionMiddlewareTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory<Program> _factory;

    public AutoProvisionMiddlewareTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email)> RegisterAndGetTokenAsync()
    {
        var email = $"autoprovision-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = "TestPassword123!"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (authResponse!.Token, email);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task AuthenticatedRequest_ShouldAutoProvisionUser()
    {
        // Arrange - Register a user (this creates ApplicationUser in Identity)
        var (token, email) = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        // Act - Make any authenticated request, which should trigger auto-provision
        // The middleware should auto-create the User entity
        var getUsersResponse = await client.GetAsync("/api/v1/users/me");

        // Assert - The user should be auto-provisioned and /me should work
        getUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getUsersResponse.Content.ReadFromJsonAsync<UserResponse>();
        userResponse.Should().NotBeNull();
        userResponse!.Email.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task MultipleAuthenticatedRequests_ShouldUseSameUser()
    {
        // Arrange
        var (token, email) = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        // Act - Make two authenticated requests
        var response1 = await client.GetAsync("/api/v1/users/me");
        var response2 = await client.GetAsync("/api/v1/users/me");

        // Assert - Both should return the same user
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var user1 = await response1.Content.ReadFromJsonAsync<UserResponse>();
        var user2 = await response2.Content.ReadFromJsonAsync<UserResponse>();

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        user1!.Id.Should().Be(user2!.Id);
        user1.Email.Should().Be(email.ToLowerInvariant());
    }
}
