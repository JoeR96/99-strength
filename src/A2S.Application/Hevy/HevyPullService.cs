using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>What one pull did. <see cref="SkippedReason"/> is set when nothing was attempted (no key, no plan).</summary>
public sealed record PullSummary(int Pulled, int Matched, int Unmatched, int Skipped, string? SkippedReason = null)
{
    public bool WasSkipped => SkippedReason is not null;

    public static PullSummary SkippedBecause(string reason) => new(0, 0, 0, 0, reason);
}

/// <summary>
/// Pulls a user's Hevy workout events since their cursor, stores each workout once as a
/// <see cref="HevyWorkoutSnapshot"/> (keyed by Hevy's id, so a replayed event updates rather than
/// duplicates), runs every snapshot still awaiting a match through <see cref="WorkoutMatcher"/> and
/// completes matched sessions through <see cref="HevySessionCompleter"/>. A deleted event flags the
/// snapshot; any completion it produced stays. Without a stored key or an active plan it reports
/// Skipped rather than failing.
/// </summary>
public sealed class HevyPullService
{
    /// <summary>Upper bound on event pages per pull (10 events each).</summary>
    public const int MaxPages = 50;

    private readonly IHevyClient _hevy;
    private readonly IHevyKeyResolver _keys;
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IHevyLinkRepository _links;
    private readonly IHevyWorkoutSnapshotRepository _snapshots;
    private readonly IHevyPullCursorRepository _cursors;
    private readonly HevyTemplateCatalogue _catalogue;
    private readonly HevySessionCompleter _completer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;
    private readonly ILogger<HevyPullService> _logger;

    public HevyPullService(
        IHevyClient hevy,
        IHevyKeyResolver keys,
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IHevyLinkRepository links,
        IHevyWorkoutSnapshotRepository snapshots,
        IHevyPullCursorRepository cursors,
        HevyTemplateCatalogue catalogue,
        HevySessionCompleter completer,
        IUnitOfWork unitOfWork,
        ILocalClock clock,
        ILogger<HevyPullService> logger)
    {
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _cursors = cursors ?? throw new ArgumentNullException(nameof(cursors));
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _completer = completer ?? throw new ArgumentNullException(nameof(completer));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PullSummary>> PullAsync(UserId userId, bool force, CancellationToken ct = default)
    {
        var apiKey = await _keys.ForUserAsync(userId, ct);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result.Success(PullSummary.SkippedBecause("no Hevy key"));
        }

        var plan = await _plans.GetActiveForUserAsync(userId, ct);
        if (plan is null)
        {
            return Result.Success(PullSummary.SkippedBecause("no active plan"));
        }

        var now = _clock.NowUtc();
        var planStart = new DateTimeOffset(_clock.BoundsOf(plan.StartDate).StartUtc, TimeSpan.Zero);
        var cursor = await _cursors.GetByIdAsync(userId, ct);
        if (cursor is null)
        {
            cursor = HevyPullCursor.Start(userId, planStart, now);
            await _cursors.AddAsync(cursor, ct);
        }
        else if (force)
        {
            cursor.Rewind(planStart);
        }

        var events = await FetchEventsAsync(apiKey, cursor.LastEventAtUtc, ct);
        if (events.IsFailure)
        {
            return PlanAccess.Fail<PullSummary>(events);
        }

        var (pulled, skipped, newest) = await StoreAsync(userId, events.Value, now, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var (matched, unmatched, alreadyDone) = await MatchAwaitingAsync(userId, plan, now, ct);
        skipped += alreadyDone;

        cursor.Advance(newest, now);
        _cursors.Update(cursor);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Hevy pull for user: {Pulled} event(s), {Matched} matched, {Unmatched} unmatched, {Skipped} skipped",
            pulled, matched, unmatched, skipped);
        return Result.Success(new PullSummary(pulled, matched, unmatched, skipped));
    }

    private async Task<Result<List<HevyWorkoutEvent>>> FetchEventsAsync(string apiKey, DateTimeOffset since, CancellationToken ct)
    {
        var events = new List<HevyWorkoutEvent>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var result = await _hevy.GetWorkoutEventsAsync(apiKey, since, page, ct: ct);
            if (!result.Ok)
            {
                return result.Error!.ToResult<List<HevyWorkoutEvent>>();
            }

            events.AddRange(result.Value!.Items);
            if (!result.Value.HasMore)
            {
                break;
            }
        }

        return Result.Success(events);
    }

    /// <summary>Upserts snapshots for updated events, flags deletions. Oldest first so a replayed older event never overwrites a newer one.</summary>
    private async Task<(int Pulled, int Skipped, DateTimeOffset? Newest)> StoreAsync(UserId userId, List<HevyWorkoutEvent> events, DateTimeOffset now, CancellationToken ct)
    {
        var skipped = 0;
        DateTimeOffset? newest = null;
        var seen = new Dictionary<string, HevyWorkoutSnapshot>(StringComparer.Ordinal);

        foreach (var evt in events.OrderBy(e => e.OccurredAt ?? DateTimeOffset.MinValue))
        {
            if (evt.OccurredAt is { } at && (newest is null || at > newest))
            {
                newest = at;
            }

            if (evt.IsUpdated)
            {
                var workout = evt.Workout!;
                var id = new HevyWorkoutId(workout.Id);
                var snapshot = await FindAsync(id, seen, ct);
                var localDate = _clock.ToLocalDate(workout.StartTime);
                var raw = JsonSerializer.Serialize(workout);

                if (snapshot is null)
                {
                    snapshot = HevyWorkoutSnapshot.Create(id, userId, workout.Title, workout.StartTime, workout.EndTime, workout.RoutineId, raw, localDate, now);
                    await _snapshots.AddAsync(snapshot, ct);
                    seen[workout.Id] = snapshot;
                }
                else if (snapshot.UserId != userId)
                {
                    skipped++;
                }
                else
                {
                    snapshot.Refresh(workout.Title, workout.StartTime, workout.EndTime, workout.RoutineId, raw, localDate, now);
                    _snapshots.Update(snapshot);
                }
            }
            else if (evt.IsDeleted)
            {
                var snapshot = await FindAsync(new HevyWorkoutId(evt.Id!), seen, ct);
                if (snapshot is null || snapshot.UserId != userId)
                {
                    skipped++;
                }
                else
                {
                    // Review focus 4: the completion, if any, stays; undoing it is the person's call.
                    snapshot.MarkDeletedInHevy(now);
                    _snapshots.Update(snapshot);
                }
            }
            else
            {
                skipped++;
            }
        }

        return (events.Count, skipped, newest);
    }

    private async Task<HevyWorkoutSnapshot?> FindAsync(HevyWorkoutId id, Dictionary<string, HevyWorkoutSnapshot> seen, CancellationToken ct)
    {
        if (seen.TryGetValue(id.Value, out var inBatch))
        {
            return inBatch;
        }

        var stored = await _snapshots.GetByIdAsync(id, ct);
        if (stored is not null)
        {
            seen[id.Value] = stored;
        }

        return stored;
    }

    private async Task<(int Matched, int Unmatched, int AlreadyDone)> MatchAwaitingAsync(UserId userId, TrainingPlan plan, DateTimeOffset now, CancellationToken ct)
    {
        var awaiting = await _snapshots.ListAwaitingMatchAsync(userId, ct);
        if (awaiting.Count == 0)
        {
            return (0, 0, 0);
        }

        var routineLinks = await _links.ListAsync(userId, HevyLinkKind.Routine, ct);
        var templateLinks = await _catalogue.TemplateIdsAsync(userId, ct);
        var program = plan.LinkedWorkoutId is { } workoutId ? await _workouts.GetByIdAsync(workoutId, ct) : null;

        var matched = 0;
        var unmatched = 0;
        var alreadyDone = 0;

        foreach (var snapshot in awaiting)
        {
            // Review focus 1: a workout that already completed a session never completes another.
            var existing = plan.Completions.FirstOrDefault(c => string.Equals(c.HevyWorkoutId, snapshot.Id.Value, StringComparison.Ordinal));
            if (existing is not null)
            {
                snapshot.MarkMatched(existing.Id, now);
                _snapshots.Update(snapshot);
                alreadyDone++;
                continue;
            }

            HevyWorkout? workout;
            try
            {
                workout = JsonSerializer.Deserialize<HevyWorkout>(snapshot.RawJson);
            }
            catch (JsonException)
            {
                workout = null;
            }

            if (workout is null)
            {
                snapshot.MarkUnmatched("stored workout could not be read", now);
                _snapshots.Update(snapshot);
                unmatched++;
                continue;
            }

            var match = WorkoutMatcher.Match(workout, plan, routineLinks, program, templateLinks, _clock);
            if (match is MatchResult.Matched hit)
            {
                var completion = await _completer.CompleteAsync(plan, program, hit.Session, hit.Date, workout, templateLinks, extra: false, ct);
                if (completion.IsSuccess)
                {
                    snapshot.MarkMatched(completion.Value, now);
                    matched++;
                }
                else
                {
                    snapshot.MarkUnmatched(completion.Error, now);
                    unmatched++;
                }
            }
            else if (match is MatchResult.Unmatched miss)
            {
                snapshot.MarkUnmatched(miss.Reason, now);
                unmatched++;
            }

            _snapshots.Update(snapshot);
        }

        return (matched, unmatched, alreadyDone);
    }
}
