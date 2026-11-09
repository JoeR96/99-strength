using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using A2S.Api.Authentication;
using A2S.Application.ApiTokens;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace A2S.Api.Tests.Unit;

/// <summary>
/// The two authentication schemes and the token allow-list, exercised through a real ASP.NET Core
/// pipeline (the API's own <c>AddA2SAuthentication</c> and <c>UseApiTokenScope</c>) with a stand-in
/// for the token store. No database, so these run without Docker.
/// </summary>
public class ApiTokenAuthenticationPipelineTests : IAsyncLifetime
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly string GoodToken = ApiToken.Issue(Joe, "Terran CC", 0, At).Plaintext;
    private static readonly string UnknownToken = ApiToken.Issue(Joe, "Never stored", 0, At).Plaintext;

    private readonly IApiTokenAuthenticator _authenticator = Substitute.For<IApiTokenAuthenticator>();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _authenticator.AuthenticateAsync(GoodToken, Arg.Any<CancellationToken>())
            .Returns(new ApiTokenIdentity(Joe, "joe@example.com", new ApiTokenId(new Guid("33333333-3333-3333-3333-333333333333")), "Terran CC"));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddA2SAuthentication("https://clerk.invalid", requireHttpsMetadata: true);
        builder.Services.AddSingleton(_authenticator);
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            // Validate against the shared test key instead of fetching Clerk's signing keys.
            options.ConfigurationManager = null;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = TestWebApplicationFactory<Program>.TestJwtIssuer,
                ValidAudience = TestWebApplicationFactory<Program>.TestJwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebApplicationFactory<Program>.TestJwtSecret))
            };
        });

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseApiTokenScope();
        _app.UseAuthorization();

        _app.MapGet("/allowed", Describe).RequireAuthorization().WithMetadata(new AllowApiTokenAttribute());
        _app.MapGet("/clerk-only", Describe).RequireAuthorization();
        _app.MapGet("/open", () => "open");

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static string Describe(HttpContext context) =>
        $"{context.User.FindFirstValue(ClaimTypes.NameIdentifier)}|{context.User.FindFirstValue(ClaimTypes.Email)}|" +
        $"{context.User.FindFirstValue(ApiTokenAuthenticationDefaults.TokenNameClaim)}|{context.User.IsApiToken()}";

    private Task<HttpResponseMessage> Get(string path, string? bearer, string scheme = "Bearer")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"{scheme} {bearer}");
        }

        return _client.SendAsync(request);
    }

    private static string ClerkJwt() => TestWebApplicationFactory<Program>.GenerateJwtToken("user_clerk", "clerk@example.com");

    [Fact]
    public async Task AValidToken_OnAnAllowedEndpoint_IsThatUser()
    {
        var response = await Get("/allowed", GoodToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("user_joe|joe@example.com|Terran CC|True");
        await _authenticator.Received(1).AuthenticateAsync(GoodToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheBearerSchemeName_IsNotCaseSensitive()
    {
        var response = await Get("/allowed", GoodToken, scheme: "bearer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/clerk-only")]
    [InlineData("/open")]
    [InlineData("/no-such-route")]
    public async Task AValidToken_AnywhereElse_IsForbidden(string path)
    {
        var response = await Get(path, GoodToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotContain(GoodToken);
    }

    [Theory]
    [InlineData("/allowed")]
    [InlineData("/clerk-only")]
    [InlineData("/open")]
    public async Task AnUnknownToken_IsUnauthorizedEverywhere(string path)
    {
        var response = await Get(path, UnknownToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Contain("invalid_token");
    }

    [Theory]
    [InlineData("nns_pat_")]
    [InlineData("nns_pat_garbage")]
    [InlineData("nns_pat_!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task AMalformedToken_IsUnauthorized(string token)
    {
        var response = await Get("/allowed", token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoCredentials_IsUnauthorizedOnProtectedEndpoints_AndFineOnOpenOnes()
    {
        (await Get("/allowed", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Get("/clerk-only", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Get("/open", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/allowed")]
    [InlineData("/clerk-only")]
    public async Task AClerkSession_ReachesBothKindsOfEndpoint_AndNeverTouchesTheTokenStore(string path)
    {
        var response = await Get(path, ClerkJwt());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("user_clerk|clerk@example.com||False");
        await _authenticator.DidNotReceiveWithAnyArgs().AuthenticateAsync(default, default);
    }

    [Fact]
    public async Task AnInvalidClerkJwt_IsUnauthorized()
    {
        var response = await Get("/clerk-only", "eyJhbGciOiJIUzI1NiJ9.e30.not-a-real-signature");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenSentWithAnotherScheme_IsNotTreatedAsAToken()
    {
        var response = await Get("/allowed", GoodToken, scheme: "Basic");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _authenticator.DidNotReceiveWithAnyArgs().AuthenticateAsync(default, default);
    }
}
