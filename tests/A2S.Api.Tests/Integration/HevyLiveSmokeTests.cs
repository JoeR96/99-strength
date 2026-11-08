using A2S.Application.Interfaces;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Runs only when a real Hevy key is available locally:
///   dotnet user-secrets set "Hevy:LiveTestApiKey" "&lt;key&gt;" --project src/A2S.Api
/// (the test project shares the API's UserSecretsId) or the HEVY_LIVE_TEST_API_KEY environment variable.
/// The key is read at test time and never written anywhere. Skipped everywhere else, including CI.
/// </summary>
[Collection("Integration")]
public class HevyLiveSmokeTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyLiveSmokeTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [LiveHevyFact]
    public async Task ValidateKey_AgainstRealHevy_Succeeds()
    {
        var key = LiveHevyFactAttribute.ReadKey()!;
        using var scope = _factory.Services.CreateScope();
        var hevy = scope.ServiceProvider.GetRequiredService<IHevyIntegrationService>();

        var valid = await hevy.ValidateApiKeyAsync(key);

        valid.Should().BeTrue();
    }
}

/// <summary>Fact that is skipped unless a live Hevy key is configured locally.</summary>
public sealed class LiveHevyFactAttribute : FactAttribute
{
    public LiveHevyFactAttribute()
    {
        if (string.IsNullOrEmpty(ReadKey()))
        {
            Skip = "No live Hevy key configured (user-secret Hevy:LiveTestApiKey or env HEVY_LIVE_TEST_API_KEY).";
        }
    }

    public static string? ReadKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<HevyLiveSmokeTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration["Hevy:LiveTestApiKey"]
            ?? Environment.GetEnvironmentVariable("HEVY_LIVE_TEST_API_KEY");
    }
}
