using System.Text.Json;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Interfaces;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>
/// Brief § 3a, last bullets: walks, runs, cardio sessions and home routines done in the app are written to Hevy
/// from the server with the same <see cref="HevyWorkoutPost"/> pattern as gym mode (queue, try once, retry in the
/// background, look up and adopt before re-posting, the pull recognises it). <see cref="StageAsync"/> adds the post
/// to the caller's unit of work; <see cref="TryPostAsync"/> runs after the caller saved.
/// </summary>
public sealed class SessionHevyPoster
{
    private readonly IHevyWorkoutPostRepository _posts;
    private readonly IHevyKeyResolver _keys;
    private readonly HevyWorkoutPoster _poster;
    private readonly IMobilityRoutineRepository _routines;
    private readonly ICardioProtocolRepository _protocols;
    private readonly ICardioTrackRepository _tracks;
    private readonly ILocalClock _clock;
    private readonly ILogger<SessionHevyPoster> _logger;

    public SessionHevyPoster(
        IHevyWorkoutPostRepository posts,
        IHevyKeyResolver keys,
        HevyWorkoutPoster poster,
        IMobilityRoutineRepository routines,
        ICardioProtocolRepository protocols,
        ICardioTrackRepository tracks,
        ILocalClock clock,
        ILogger<SessionHevyPoster> logger)
    {
        _posts = posts;
        _keys = keys;
        _poster = poster;
        _routines = routines;
        _protocols = protocols;
        _tracks = tracks;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Stages the Hevy post for a cardio or mobility completion recorded in the app. Idempotent on the completion
    /// id (used as the client session id). Null when there is nothing to post (no duration known, or already queued).
    /// </summary>
    public async Task<HevyWorkoutPost?> StageForCompletionAsync(TrainingPlan plan, SessionCompletion completion, CancellationToken ct)
    {
        if (completion.Source != CompletionSource.Manual || completion.Kind is not (SessionKind.Cardio or SessionKind.Mobility))
        {
            return null;
        }

        var label = plan.Schedule.FirstOrDefault(s => s.Day == completion.Date.DayOfWeek && s.Kind == completion.Kind
            && (s.CardioTrackId == completion.CardioTrackId || s.CardioProtocolId == completion.CardioProtocolId || s.MobilityRoutineId == completion.MobilityRoutineId))?.Label;

        GymSessionPayload payload;
        int duration;
        if (completion.Kind == SessionKind.Mobility)
        {
            if (completion.MobilityRoutineId is not { } routineId || await _routines.GetByIdAsync(routineId, ct) is not { } routine)
            {
                return null;
            }

            duration = (completion.DurationMinutes ?? routine.TargetMinutes) * 60;
            label ??= routine.Name;
            payload = new GymSessionPayload
            {
                Routine = new RoutineSessionPost { MobilityRoutineId = routineId.Value, Name = routine.Name, DurationSeconds = duration, DrillTicks = completion.DrillTicks }
            };
        }
        else
        {
            var cardio = await CardioPostAsync(completion.CardioTrackId, completion.CardioProtocolId, completion.Cardio, ct);
            if (cardio is null)
            {
                return null;
            }

            duration = cardio.DurationSeconds;
            label ??= "Cardio";
            payload = new GymSessionPayload { Cardio = cardio };
        }

        return await StageAsync(plan.UserId, completion.Id.Value, plan.Id, completion.Id, completion.Date,
            RoutineBuilder.SlotTitle(plan.Name, label), completion.RecordedAtUtc, duration, payload, ct);
    }

    /// <summary>Stages the post for a track session logged outside the plan (Tracks page or Today).</summary>
    public async Task<HevyWorkoutPost?> StageForTrackAsync(CardioTrack track, Guid clientSessionId, DateOnly date, CardioLog log, DateTimeOffset at, CancellationToken ct)
    {
        var cardio = await CardioPostAsync(track.Id, null, log, ct, track.Sessions.LastOrDefault()?.Prescription);
        return cardio is null
            ? null
            : await StageAsync(track.UserId, clientSessionId, null, null, date, track.Name, at, cardio.DurationSeconds, new GymSessionPayload { Cardio = cardio }, ct);
    }

    /// <summary>One attempt after the caller saved; failures stay queued for the background retry.</summary>
    public async Task TryPostAsync(HevyWorkoutPost? post, CancellationToken ct)
    {
        if (post is null || post.Status != HevyPostStatus.Pending)
        {
            return;
        }

        try
        {
            await _poster.PostAsync(post, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Session Hevy post failed; it stays queued");
        }
    }

    private async Task<HevyWorkoutPost?> StageAsync(
        UserId userId,
        Guid clientSessionId,
        TrainingPlanId? planId,
        SessionCompletionId? completionId,
        DateOnly date,
        string title,
        DateTimeOffset endedAt,
        int durationSeconds,
        GymSessionPayload payload,
        CancellationToken ct)
    {
        if (await _posts.FindByClientSessionAsync(userId, clientSessionId, ct) is not null)
        {
            return null;
        }

        var post = HevyWorkoutPost.Create(userId, clientSessionId, planId, completionId, date, title,
            endedAt.AddSeconds(-Math.Max(1, durationSeconds)), endedAt,
            JsonSerializer.Serialize(payload, FinishGymSessionCommandHandler.Json), "{}", _clock.NowUtc());
        if (string.IsNullOrEmpty(await _keys.ForUserAsync(userId, ct)))
        {
            post.MarkSkipped("no Hevy key");
        }

        await _posts.AddAsync(post, ct);
        return post;
    }

    private async Task<CardioSessionPost?> CardioPostAsync(CardioTrackId? trackId, CardioProtocolId? protocolId, CardioLog? log, CancellationToken ct, string? prescription = null)
    {
        var duration = log?.DurationSeconds;
        if (duration is null && protocolId is { } p && await _protocols.GetByIdAsync(p, ct) is { } protocol)
        {
            duration = protocol.TotalMinutes * 60;
        }

        if (trackId is { } t && prescription is null && await _tracks.GetByIdAsync(t, ct) is { } track)
        {
            prescription = track.Sessions.LastOrDefault()?.Prescription;
            duration ??= track.Sessions.LastOrDefault()?.DurationSeconds;
        }

        return duration is null or <= 0
            ? null
            : new CardioSessionPost
            {
                CardioTrackId = trackId?.Value,
                CardioProtocolId = protocolId?.Value,
                Prescription = prescription,
                DurationSeconds = duration.Value,
                DistanceMeters = log?.DistanceMeters is { } d ? (int)Math.Round(d) : null,
                Floors = log?.Floors,
                IntensityValue = log?.IntensityValue,
                Rpe = log?.EffectiveRpe
            };
    }
}
