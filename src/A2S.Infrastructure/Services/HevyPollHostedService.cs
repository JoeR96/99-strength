using A2S.Application.Hevy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Ticks <see cref="HevyPollRunner.RunOnceAsync"/> every 15 minutes inside a fresh DI scope, so a
/// workout logged in Hevy completes its session without anyone opening the app. Idle in the
/// <c>Testing</c> environment (test hosts never call Hevy). Logs counts only; never a key.
/// </summary>
public sealed class HevyPollHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HevyPollHostedService> _logger;

    public HevyPollHostedService(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<HevyPollHostedService> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether this host polls Hevy at all.</summary>
    public bool IsEnabled => !_environment.IsEnvironment("Testing");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Hevy poll disabled (Testing environment)");
            return;
        }

        _logger.LogInformation("Hevy poll enabled; pulling every {Interval}", Interval);
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
            var runner = scope.ServiceProvider.GetRequiredService<HevyPollRunner>();
            var users = await runner.RunOnceAsync(ct);
            if (users > 0)
            {
                _logger.LogInformation("Hevy poll pulled for {Count} user(s)", users);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bad tick must not kill the loop.
            _logger.LogError(ex, "Hevy poll tick failed");
        }
    }
}
