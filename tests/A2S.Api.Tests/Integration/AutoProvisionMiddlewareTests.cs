using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Responses;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Integration tests for the AutoProvisionUserMiddleware.
/// </summary>
[Collection("Integration")]
public class AutoProvisionMiddlewareTests
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory<Program> _factory;

    public AutoProvisionMiddlewareTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task AuthenticatedRequest_ShouldAutoProvisionUser()
    {
        var client = CreateClient();

        // The middleware should auto-create the User entity
        var getUsersResponse = await client.GetAsync("/api/v1/users/me");

        getUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getUsersResponse.Content.ReadFromJsonAsync<UserResponse>();
        userResponse.Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleAuthenticatedRequests_ShouldUseSameUser()
    {
        var client = CreateClient();

        var response1 = await client.GetAsync("/api/v1/users/me");
        var response2 = await client.GetAsync("/api/v1/users/me");

        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var user1 = await response1.Content.ReadFromJsonAsync<UserResponse>();
        var user2 = await response2.Content.ReadFromJsonAsync<UserResponse>();

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        user1!.Id.Should().Be(user2!.Id);
    }
}
