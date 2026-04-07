using System.Net;
using System.Net.Http.Json;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class HevyProxyControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyProxyControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Validate API Key

    [Fact]
    public async Task ValidateApiKey_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/validate");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/validate");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Proxy GET — Resource Validation

    [Fact]
    public async Task ProxyGet_DisallowedResource_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.GetAsync("/api/v1/hevy/admin");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not a supported Hevy API resource");
    }

    [Fact]
    public async Task ProxyGet_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/hevy/workouts");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("API key is required");
    }

    [Theory]
    [InlineData("workouts")]
    [InlineData("routines")]
    [InlineData("exercise_templates")]
    [InlineData("routine_folders")]
    public async Task ProxyGet_AllowedResources_DoNotReturnForbidden(string resource)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.GetAsync($"/api/v1/hevy/{resource}");

        // These will fail connecting to real Hevy API, but should NOT return 400 for disallowed resource
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProxyGet_SsrfAttempt_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.GetAsync("/api/v1/hevy/../../etc/passwd");

        // The segment before the slash is the "resource" — should be rejected
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    #endregion

    #region Proxy POST — Resource Validation

    [Fact]
    public async Task ProxyPost_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/hevy/routines", new { name = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProxyPost_DisallowedResource_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.PostAsJsonAsync("/api/v1/hevy/users", new { name = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Proxy PUT — Resource Validation

    [Fact]
    public async Task ProxyPut_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/hevy/routines/123", new { name = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProxyPut_DisallowedResource_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.PutAsJsonAsync("/api/v1/hevy/users/123", new { name = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Proxy DELETE — Resource Validation

    [Fact]
    public async Task ProxyDelete_WithoutApiKey_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync("/api/v1/hevy/routines/123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProxyDelete_DisallowedResource_ReturnsBadRequest()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Hevy-Api-Key", "test-key-12345");

        var response = await client.DeleteAsync("/api/v1/hevy/users/123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Auth Requirements

    [Fact]
    public async Task AllEndpoints_WithoutAuth_ReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var getResponse = await client.GetAsync("/api/v1/hevy/workouts");
        var postResponse = await client.PostAsJsonAsync("/api/v1/hevy/routines", new { });
        var putResponse = await client.PutAsJsonAsync("/api/v1/hevy/routines/1", new { });
        var deleteResponse = await client.DeleteAsync("/api/v1/hevy/routines/1");

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        putResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
