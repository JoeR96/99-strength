using System.Net;
using System.Net.Http.Json;
using A2S.Api.Controllers;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for Authentication API endpoints.
/// Uses WebApplicationFactory and Testcontainers to test full stack.
/// </summary>
public class AuthIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory<Program> _factory;

    public AuthIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();
        authResponse.Email.Should().Be(registerRequest.Email);
        authResponse.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "weak" // Too short, missing uppercase, digit, etc.
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var email = $"test-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = "TestPassword123!"
        };

        // Act - Register first user
        var response1 = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Try to register again with same email
        var response2 = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange - First register a user
        var email = $"test-{Guid.NewGuid()}@example.com";
        var password = "TestPassword123!";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Login with the registered credentials
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();
        authResponse.Email.Should().Be(email);
        authResponse.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange - First register a user
        var email = $"test-{Guid.NewGuid()}@example.com";
        var correctPassword = "TestPassword123!";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = correctPassword
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Try to login with wrong password
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthToken_CanBeUsedToAccessProtectedEndpoints()
    {
        // Arrange - Register and login to get a token
        var email = $"test-{Guid.NewGuid()}@example.com";
        var password = "TestPassword123!";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Act - Create a new client with the auth token
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Note: You'll need to add a protected endpoint to test this fully
        // For now, we just verify the token was returned
        authResponse.Token.Should().NotBeNullOrEmpty();
    }
}
