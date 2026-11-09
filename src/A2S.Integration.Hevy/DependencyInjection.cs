using A2S.Application.Interfaces;
using A2S.Integration.Hevy.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace A2S.Integration.Hevy;

/// <summary>
/// Dependency injection configuration for the Hevy integration layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Hevy integration services to the DI container.
    /// </summary>
    public static IServiceCollection AddHevyIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HevyOptions>(
            configuration.GetSection(HevyOptions.SectionName));

        var hevyOptions = configuration
            .GetSection(HevyOptions.SectionName)
            .Get<HevyOptions>() ?? new HevyOptions();

        services.AddHttpClient(HevyIntegrationService.HttpClientName)
            .AddPolicyHandler(GetRetryPolicy(hevyOptions.RetryCount))
            .AddPolicyHandler(GetCircuitBreakerPolicy(
                hevyOptions.CircuitBreakerThreshold,
                hevyOptions.CircuitBreakerDurationSeconds));

        services.AddScoped<IHevyIntegrationService, HevyIntegrationService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        int threshold, int durationSeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                threshold,
                TimeSpan.FromSeconds(durationSeconds));
    }
}
