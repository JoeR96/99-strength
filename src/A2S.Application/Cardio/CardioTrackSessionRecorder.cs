using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;

namespace A2S.Application.Cardio;

/// <summary>
/// Records a cardio session against a track: loads the track (owned by the user), evaluates it with
/// the user's heart-rate zones, and — for a walk logged for the first time — ticks the Walks habit.
/// Shared by manual logging, CompleteSession and the Hevy pull so they behave identically.
/// Does not save; the caller owns the unit of work.
/// </summary>
public sealed class CardioTrackSessionRecorder
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUserRepository _users;
    private readonly IHabitRepository _habits;
    private readonly ITrainingPlanRepository? _plans;

    public CardioTrackSessionRecorder(ICardioTrackRepository tracks, IUserRepository users, IHabitRepository habits, ITrainingPlanRepository? plans = null)
    {
        _plans = plans;
        _tracks = tracks;
        _users = users;
        _habits = habits;
    }

    /// <summary>The track's prescription title before a session is recorded (null when not the user's track).</summary>
    public async Task<string?> PrescriptionAsync(UserId userId, CardioTrackId trackId, CancellationToken ct)
    {
        var track = await _tracks.GetByIdAsync(trackId, ct);
        return track is null || track.UserId != userId ? null : track.Prescription.Title;
    }

    /// <summary>Null when the track does not exist or is not the user's.</summary>
    public async Task<(CardioTrack Track, TrackEvaluationResult Result)?> RecordAsync(
        UserId userId,
        CardioTrackId trackId,
        string key,
        DateOnly date,
        CardioLog? log,
        bool completedAsPrescribed,
        DateTimeOffset at,
        CancellationToken ct,
        TrainingPlan? plan = null)
    {
        var track = await _tracks.GetByIdAsync(trackId, ct);
        if (track is null || track.UserId != userId)
        {
            return null;
        }

        // "Mark done" with nothing logged: the prescription, done as written.
        log ??= new CardioLog(Math.Max(1, track.Prescription.Minutes) * 60, null, null, null, null);

        var user = await _users.GetByIdAsync(userId, ct);
        // The plan's schedule sets a week program's gate (planned and missed sessions this week).
        plan ??= _plans is null ? null : await _plans.GetActiveForUserAsync(userId, ct);
        var week = CardioWeekContext.For(plan, track.Id, date, track.State.WeekStartedOn);
        var result = track.LogSession(key, date, log, completedAsPrescribed, user?.HeartRateZones, at, week);
        _tracks.Update(track);

        if (!result.AlreadyRecorded && track.Modality == CardioModality.Walk)
        {
            await TickWalksHabitAsync(userId, date, log, at, ct);
        }

        return (track, result);
    }

    private async Task TickWalksHabitAsync(UserId userId, DateOnly date, CardioLog log, DateTimeOffset at, CancellationToken ct)
    {
        var habits = await _habits.ListForUserAsync(userId, includeArchived: false, ct);
        var walks = habits.FirstOrDefault(h =>
            string.Equals(h.Name.Trim(), "Walks", StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.Name.Trim(), "Walk", StringComparison.OrdinalIgnoreCase)
            || (h.ExternalKey?.EndsWith(":walks", StringComparison.OrdinalIgnoreCase) ?? false));
        if (walks is null)
        {
            return;
        }

        var step = walks.Type == HabitType.Numeric && (walks.Unit?.Contains("min", StringComparison.OrdinalIgnoreCase) ?? false)
            ? Math.Ceiling(log.DurationSeconds / 60m)
            : 1m;

        var existing = await _habits.GetLogAsync(walks.Id, date, ct);
        if (existing is null)
        {
            await _habits.AddLogAsync(HabitLog.Create(walks.Id, userId, date, step, at), ct);
            return;
        }

        if (walks.Type == HabitType.Checkbox)
        {
            existing.SetValue(1, at);
        }
        else
        {
            existing.Increment(step, at);
        }

        _habits.UpdateLog(existing);
    }
}
