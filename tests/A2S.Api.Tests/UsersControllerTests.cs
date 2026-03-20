using System.Net;
using System.Net.Http.Json;
using A2S.Api.Controllers;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for Users API endpoints.
/// Uses WebApplicationFactory and Testcontainers to test full stack.
/// </summary>
[Collection("Integration")]
public class UsersControllerTests
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory<Program> _factory;

    public UsersControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsCreated()
    {
        // Arrange
        var client = CreateClient();

        var request = new CreateUserRequest(
            $"newuser-{Guid.NewGuid()}@example.com",
            "New User");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        userResponse.Should().NotBeNull();
        userResponse!.Email.Should().Be(request.Email.ToLowerInvariant());
        userResponse.Name.Should().Be(request.Name);
        userResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();

        var request = new CreateUserRequest("invalid-email", "Test User");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();

        var email = $"duplicate-{Guid.NewGuid()}@example.com";
        var request = new CreateUserRequest(email, "First User");

        // Create the first user
        var response1 = await client.PostAsJsonAsync("/api/v1/users", request);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Try to create another user with the same email
        var request2 = new CreateUserRequest(email, "Second User");
        var response2 = await client.PostAsJsonAsync("/api/v1/users", request2);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserById_WhenUserExists_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();

        var createRequest = new CreateUserRequest(
            $"getuser-{Guid.NewGuid()}@example.com",
            "Get User Test");

        var createResponse = await client.PostAsJsonAsync("/api/v1/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var response = await client.GetAsync($"/api/v1/users/{createdUser!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        userResponse.Should().NotBeNull();
        userResponse!.Id.Should().Be(createdUser.Id);
        userResponse.Email.Should().Be(createdUser.Email);
    }

    [Fact]
    public async Task GetUserById_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateUserRequest("test@example.com", "Test User");

        // Act - No auth token
        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act - No auth token
        var response = await _client.GetAsync($"/api/v1/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
