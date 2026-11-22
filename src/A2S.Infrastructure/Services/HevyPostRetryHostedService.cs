using A2S.Application.Hevy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Ticks <see cref="HevyPostRetryRunner.RunOnceAsync"/> every 3 minutes inside a fresh DI scope, so a gym
/// session that could not reach Hevy at the gym lands later without anyone opening the app. Each post
/// backs off on its own (2, 4, 8 … minutes). Idle in the <c>Testing</c> environment. Logs counts only.
/// </summary>
public sealed class HevyPostRetryHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HevyPostRetryHostedService> _logger;

    public HevyPostRetryHostedService(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<HevyPostRetryHostedService> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => !_environment.IsEnvironment("Testing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Gym session Hevy retry disabled (Testing environment)");
            return;
        }

        using var timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<HevyPostRetryRunner>();
            var tried = await runner.RunOnceAsync(ct);
            if (tried > 0)
            {
                _logger.LogInformation("Gym session Hevy retry tried {Count} post(s)", tried);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gym session Hevy retry tick failed");
        }
    }
}
