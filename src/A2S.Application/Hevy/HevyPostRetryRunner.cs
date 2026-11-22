using A2S.Domain.Common;
using A2S.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>
/// One tick of the gym-session Hevy retry (the hosted service calls <see cref="RunOnceAsync"/> every few
/// minutes inside a scope): every pending post whose backoff has elapsed gets one more attempt through
/// <see cref="HevyWorkoutPoster"/>, which looks Hevy up before re-posting so a retry never duplicates.
/// </summary>
public sealed class HevyPostRetryRunner
{
    /// <summary>Posts tried per tick, oldest due first.</summary>
    public const int BatchSize = 20;

    private readonly IHevyWorkoutPostRepository _posts;
    private readonly HevyWorkoutPoster _poster;
    private readonly ILocalClock _clock;
    private readonly ILogger<HevyPostRetryRunner> _logger;

    public HevyPostRetryRunner(IHevyWorkoutPostRepository posts, HevyWorkoutPoster poster, ILocalClock clock, ILogger<HevyPostRetryRunner> logger)
    {
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _poster = poster ?? throw new ArgumentNullException(nameof(poster));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Tries every due post once; returns how many were attempted.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var due = await _posts.ListDueAsync(_clock.NowUtc(), BatchSize, ct);
        foreach (var post in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _poster.PostAsync(post, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Gym session Hevy retry failed for one post");
            }
        }

        return due.Count;
    }
}
