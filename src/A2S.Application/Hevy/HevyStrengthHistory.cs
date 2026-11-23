using System.Text.Json;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace A2S.Application.Hevy;

/// <summary>Where the strength history came from: Hevy answered, there is no key, or Hevy failed.</summary>
public enum HevyHistoryStatus
{
    Ok,
    NoKey,
    Unavailable
}

/// <summary>The user's recent Hevy workouts (API pages plus stored snapshots, de-duplicated by id).</summary>
public sealed record HevyHistory(HevyHistoryStatus Status, IReadOnlyList<HevyWorkout> Workouts, string? Message);

/// <summary>
/// Reads up to <see cref="MaxPages"/> × 10 recent workouts from Hevy (stopping once a page reaches a year
/// back) and merges in the stored <see cref="HevyWorkoutSnapshot"/> rows. A successful read is cached per
/// user for <see cref="CacheFor"/>; a failed one is not, so the next look tries again. Never throws for
/// a Hevy outcome: a failure comes back as <see cref="HevyHistoryStatus.Unavailable"/> with the snapshots.
/// </summary>
public sealed class HevyStrengthHistory
{
    public const int MaxPages = 20;
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LookBack = TimeSpan.FromDays(365);

    private static readonly SnapshotStatus[] UsableStatuses =
        [SnapshotStatus.Matched, SnapshotStatus.Unmatched, SnapshotStatus.Pending, SnapshotStatus.Ignored];

    private readonly IHevyClient _hevy;
    private readonly IHevyKeyResolver _keys;
    private readonly IHevyWorkoutSnapshotRepository _snapshots;
    private readonly IMemoryCache _cache;
    private readonly ILocalClock _clock;

    public HevyStrengthHistory(
        IHevyClient hevy, IHevyKeyResolver keys, IHevyWorkoutSnapshotRepository snapshots, IMemoryCache cache, ILocalClock clock)
    {
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<HevyHistory> LoadAsync(UserId userId, CancellationToken ct = default)
    {
        var cacheKey = ("hevy-strength-history", userId.Value);
        if (_cache.TryGetValue(cacheKey, out HevyHistory? cached) && cached is not null)
        {
            return cached;
        }

        var stored = await StoredAsync(userId, ct);
        var key = await _keys.ForUserAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(key))
        {
            return new HevyHistory(HevyHistoryStatus.NoKey, stored, "Connect Hevy to get suggestions");
        }

        var cutoff = _clock.NowUtc() - LookBack;
        var fetched = new List<HevyWorkout>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var result = await _hevy.GetWorkoutsAsync(key, page, ct);
            if (!result.Ok || result.Value is null)
            {
                return new HevyHistory(HevyHistoryStatus.Unavailable, Merge(fetched, stored), "Hevy unavailable");
            }

            fetched.AddRange(result.Value.Items);
            if (!result.Value.HasMore || result.Value.Items.Count == 0 || result.Value.Items.Min(w => w.StartTime) < cutoff)
            {
                break;
            }
        }

        var history = new HevyHistory(HevyHistoryStatus.Ok, Merge(fetched, stored), null);
        _cache.Set(cacheKey, history, CacheFor);
        return history;
    }

    private async Task<List<HevyWorkout>> StoredAsync(UserId userId, CancellationToken ct)
    {
        var workouts = new List<HevyWorkout>();
        foreach (var status in UsableStatuses)
        {
            foreach (var snapshot in await _snapshots.ListAsync(userId, status, ct))
            {
                try
                {
                    if (JsonSerializer.Deserialize<HevyWorkout>(snapshot.RawJson) is { } workout)
                    {
                        workouts.Add(workout);
                    }
                }
                catch (JsonException)
                {
                    // A snapshot we cannot read adds nothing; the API copy (if any) still counts.
                }
            }
        }

        return workouts;
    }

    private static IReadOnlyList<HevyWorkout> Merge(IEnumerable<HevyWorkout> fetched, IEnumerable<HevyWorkout> stored)
    {
        var byId = new Dictionary<string, HevyWorkout>(StringComparer.Ordinal);
        foreach (var workout in stored.Concat(fetched))
        {
            byId[string.IsNullOrEmpty(workout.Id) ? Guid.NewGuid().ToString() : workout.Id] = workout;
        }

        return byId.Values.OrderByDescending(w => w.StartTime).ToList();
    }
}
