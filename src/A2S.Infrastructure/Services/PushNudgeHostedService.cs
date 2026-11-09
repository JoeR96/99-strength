using A2S.Application.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Ticks <see cref="PushNudgeService.RunOnceAsync"/> every minute inside a fresh DI scope. Stays idle
/// in the <c>Testing</c> environment and when no VAPID keys are configured, so test hosts and
/// developer machines without secrets never try to send anything.
/// </summary>
public sealed class PushNudgeHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly PushOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PushNudgeHostedService> _logger;

    public PushNudgeHostedService(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        IOptions<PushOptions> options,
        TimeProvider timeProvider,
        ILogger<PushNudgeHostedService> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether this host should send nudges at all.</summary>
    public bool IsEnabled => !_environment.IsEnvironment("Testing") && _options.IsConfigured;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Push nudges disabled ({Reason})", _options.IsConfigured ? "Testing environment" : "no VAPID keys configured");
            return;
        }

        _logger.LogInformation("Push nudges enabled; checking every {Interval}", Interval);
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
            var service = scope.ServiceProvider.GetRequiredService<PushNudgeService>();
            var sent = await service.RunOnceAsync(ct);
            if (sent > 0)
            {
                _logger.LogInformation("Sent {Count} push nudge(s)", sent);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bad tick must not kill the loop.
            _logger.LogError(ex, "Push nudge tick failed");
        }
    }
}
