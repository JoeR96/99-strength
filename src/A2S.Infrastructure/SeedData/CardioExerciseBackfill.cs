using System.Text.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.GymSessions;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Moves a running "Falador Fat Furnace" (plan v3, matched by name and former names: plans keep no template key)
/// from finishers onto cardio exercises. For each active such plan whose linked workout has no cardio exercise yet:
/// <list type="number">
/// <item>inserts the plan's four cardio exercises, each last on its day;</item>
/// <item>seeds the stair climber from the old stair finisher track's most recent session (its work minutes, one step
/// more when that session would step up: Easy, finished, no pain; kept inside 6–12 min, level 6);</item>
/// <item>adds this week's stair finisher sessions, and fixed treadmill cool-downs, to the completed lift days' history
/// (no progression is applied);</item>
/// <item>rewrites those days' Hevy posts (finisher out, cardio exercise in): one already in Hevy queues an in-place
/// update, one not sent yet (pending, failed, skipped) just carries the new payload; the Hevy retry service sends
/// either later, never here;</item>
/// <item>clears the stair and fixed cool-down finishers from the lift slots and pauses the stair track (history kept).</item>
/// </list>
/// One save per plan. Idempotent: a workout that already has a cardio exercise is left alone.
/// </summary>
public static class CardioExerciseBackfill
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Returns how many plans were moved over. Never throws (bar cancellation): the API must always boot, so any failure
    /// is logged; a plan that fails is left exactly as it was.
    /// </summary>
    public static async Task<int> RunAsync(A2SDbContext db, ILogger logger, DateTimeOffset? now = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        var moved = 0;
        try
        {
            // PlanTemplates.IsSamePlan for plan v3: its current name or one it was started under before.
            var names = PlanV3BaseBuildTransition.FormerNames.Append(PlanV3BaseBuildTransition.TemplateName).ToList();
            var planIds = await db.TrainingPlans
                .AsNoTracking()
                .Where(p => p.IsActive && p.LinkedWorkoutId != null && names.Contains(p.Name))
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var planId in planIds)
            {
                // Each plan starts from a clean tracker and its own fresh load, so a failure before it cannot leave it half-written.
                db.ChangeTracker.Clear();
                try
                {
                    var plan = await db.TrainingPlans.Include(p => p.Completions).FirstOrDefaultAsync(p => p.Id == planId, ct);
                    if (plan is not null && await MoveAsync(db, plan, now ?? DateTimeOffset.UtcNow, ct))
                    {
                        await db.SaveChangesAsync(ct);
                        moved++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Moving a plan onto cardio exercises failed; it is left as it was");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The cardio exercise backfill could not run");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }

        if (moved > 0)
        {
            logger.LogInformation("Moved {Count} plan(s) onto cardio exercises.", moved);
        }

        return moved;
    }

    private static async Task<bool> MoveAsync(A2SDbContext db, TrainingPlan plan, DateTimeOffset now, CancellationToken ct)
    {
        var workout = await db.Workouts
            .Include(w => w.Exercises).ThenInclude(e => e.Progression)
            .FirstOrDefaultAsync(w => w.Id == plan.LinkedWorkoutId!.Value, ct);
        if (workout is null
            || workout.Status is not (WorkoutStatus.Active or WorkoutStatus.NotStarted)
            || workout.Exercises.Any(e => e.Category == ExerciseCategory.Cardio))
        {
            return false;
        }

        // 1. The plan's cardio exercises, last on their days.
        foreach (var request in PlanTemplatePreview.PreviewSpec(new PlanV3BaseBuildTransition(), plan.StartDate).Exercises
                     .Where(e => e.ProgressionType == CardioStageStrategy.TypeName))
        {
            workout.InsertExercise(Exercise.CreateWithCardioStageProgression(
                request.TemplateName, request.AssignedDay, int.MaxValue, request.ExternalTemplateId, request.CardioLane!.Value));
        }

        var stairTracks = await db.CardioTracks
            .Where(t => t.UserId == plan.UserId && t.TemplateKey == CardioTrackTemplates.StairFinisher)
            .ToListAsync(ct);

        // 2. Seed the stair climber from the old track's most recent session.
        var latest = stairTracks.SelectMany(t => t.Sessions).OrderBy(s => s.Date).LastOrDefault();
        if (latest is not null)
        {
            workout.SeedCardioLane(CardioLane.Hard, SeedMinutes(latest));
        }

        // 3. This week's completed lift days get their stair finisher (or fixed cool-down) as cardio history.
        var retroLogged = new List<(DayNumber Day, DateOnly Date, int Week, ExercisePerformanceRequest Request)>();
        foreach (var activity in workout.CompletedActivities.Where(a => a.WeekNumber == workout.CurrentWeek).ToList())
        {
            if (activity.Performances.Any(p => p.Cardio is not null))
            {
                continue;
            }

            // The day can be done once a week, so its latest lift completion is this week's.
            var lift = plan.Completions
                .Where(c => c.Kind == SessionKind.Lift && c.LiftDay == activity.Day)
                .OrderBy(c => c.Date).ThenBy(c => c.RecordedAtUtc)
                .LastOrDefault();
            if (lift is null)
            {
                continue;
            }

            var cardioThatDay = plan.Completions.Where(c => c.Kind == SessionKind.Cardio && c.Date == lift.Date).ToList();
            var logged = StairSession(workout, activity, lift.Date, cardioThatDay, stairTracks)
                         ?? CoolDown(workout, activity, cardioThatDay);
            if (logged is not { } found)
            {
                continue;
            }

            workout.AttachCardioPerformance(activity.Day, activity.WeekNumber, found.Performance);
            retroLogged.Add((activity.Day, lift.Date, activity.WeekNumber, found.Request));
        }

        // 4. Those days' Hevy posts are rewritten: an in-place update once in Hevy, the next post otherwise.
        foreach (var (day, date, week, request) in retroLogged)
        {
            var posts = await db.HevyWorkoutPosts
                .Where(p => p.UserId == plan.UserId && p.LocalDate == date)
                .ToListAsync(ct);
            foreach (var post in posts)
            {
                var payload = JsonSerializer.Deserialize<GymSessionPayload>(post.PayloadJson, Json);
                if (payload is null || payload.WorkoutId != workout.Id.Value || payload.Day != day)
                {
                    continue;
                }

                var rewritten = payload with
                {
                    Week = payload.Week ?? week,
                    Performances = [.. payload.Performances.Where(p => p.ExerciseId != request.ExerciseId), request],
                    Finisher = null,
                    FinisherTrackId = null,
                    FinisherProtocolId = null,
                    FinisherFixedKind = null,
                    FinisherFixedNotes = null,
                    FinisherPrescription = null
                };
                var json = JsonSerializer.Serialize(rewritten, Json);
                if (post.HevyWorkoutId is not null && post.Status is (HevyPostStatus.Posted or HevyPostStatus.UpdatePending))
                {
                    post.RequestUpdate(json, now);
                }
                else
                {
                    // Not in Hevy yet (pending, failed or skipped): its next attempt posts the new payload as a new workout.
                    post.ReplacePayload(json);
                }
            }
        }

        // 5. No more stair or cool-down finishers; the old stair track stops (its history stays).
        var stairIds = stairTracks.Select(t => t.Id).ToHashSet();
        if (plan.Schedule.Any(s => IsReplacedFinisher(s.Finisher, stairIds)))
        {
            plan.ReplaceSchedule(plan.Schedule.Select(s => IsReplacedFinisher(s.Finisher, stairIds)
                ? new ScheduledSession(s.Day, s.Kind, s.LiftDay, s.CardioProtocolId, s.MobilityRoutineId, s.Label,
                    s.WarmUpRoutineId, finisher: null, s.CardioTrackId)
                : s).ToList());
        }

        foreach (var track in stairTracks.Where(t => t.Status == CardioTrackStatus.Active))
        {
            track.Pause();
        }

        return true;
    }

    /// <summary>
    /// The session's work minutes, one step more when the hard lane would step up on it (Easy, finished, no pain),
    /// kept inside the first stage's range.
    /// </summary>
    internal static int SeedMinutes(CardioTrackSession session)
    {
        var stage = CardioLanes.StairClimber().Stages[0];
        var worked = session.WorkMinutes ?? session.DurationSeconds / 60m;
        var stepsUp = session.Effort == EffortRating.Easy && session.Completed && session.Pain == PainFlag.None;
        var minutes = (int)Math.Round(worked, MidpointRounding.AwayFromZero) + (stepsUp ? stage.MinuteStep!.Value : 0);
        return Math.Clamp(minutes, stage.MinMinutes!.Value, stage.MaxMinutes!.Value);
    }

    private sealed record Logged(ExercisePerformance Performance, ExercisePerformanceRequest Request);

    /// <summary>The stair finisher session done on the lift day, on that day's stair exercise.</summary>
    private static Logged? StairSession(
        Workout workout, WorkoutActivity activity, DateOnly date, List<SessionCompletion> cardioThatDay, List<CardioTrack> stairTracks)
    {
        var stairs = CardioOn(workout, activity.Day, CardioLane.Hard);
        if (stairs is null)
        {
            return null;
        }

        foreach (var completion in cardioThatDay.Where(c => c.CardioTrackId is not null))
        {
            var track = stairTracks.FirstOrDefault(t => t.Id == completion.CardioTrackId);
            var session = track?.Sessions.FirstOrDefault(s => s.Key == completion.TrackSessionKey)
                          ?? track?.Sessions.LastOrDefault(s => s.Date == date);
            if (session is null)
            {
                continue;
            }

            var request = new ExercisePerformanceRequest
            {
                ExerciseId = stairs.Id.Value,
                Cardio = new CardioResultRequest(session.Completed, session.Effort.ToEffort(), session.Pain, session.PainArea,
                    session.DurationSeconds, session.WorkMinutes, session.IntensityValue, null, session.Floors)
            };
            return new Logged(Performance(stairs, activity, request), request);
        }

        return null;
    }

    /// <summary>A fixed cool-down (a cardio log with no protocol and no track) on a lower day, on that day's walk.</summary>
    private static Logged? CoolDown(Workout workout, WorkoutActivity activity, List<SessionCompletion> cardioThatDay)
    {
        var walk = CardioOn(workout, activity.Day, CardioLane.Easy);
        var log = cardioThatDay.FirstOrDefault(c => c.CardioProtocolId is null && c.CardioTrackId is null && c.Cardio is not null)?.Cardio;
        if (walk is null || log is null)
        {
            return null;
        }

        var request = new ExercisePerformanceRequest
        {
            ExerciseId = walk.Id.Value,
            Cardio = new CardioResultRequest(true, Effort.Easy, TotalSeconds: log.DurationSeconds)
        };
        return new Logged(Performance(walk, activity, request), request);
    }

    /// <summary>History only: planned against the lane's starting block, done as logged.</summary>
    private static ExercisePerformance Performance(Exercise exercise, WorkoutActivity activity, ExercisePerformanceRequest request)
    {
        var lane = ((CardioStageStrategy)exercise.Progression).Lane;
        var start = CardioStageStrategy.Create(lane == CardioLane.Hard ? CardioLanes.StairClimber() : CardioLanes.InclineWalk());
        return new ExercisePerformance(exercise.Id, [], [], activity.CompletedAt,
            plannedCardio: start.CalculateCardioBlock(), cardio: request.Cardio!.ToCardioResult());
    }

    private static Exercise? CardioOn(Workout workout, DayNumber day, CardioLane lane) =>
        workout.Exercises.FirstOrDefault(e => e.AssignedDay == day && e.Progression is CardioStageStrategy s && s.Lane == lane);

    private static bool IsReplacedFinisher(SessionFinisher? finisher, HashSet<CardioTrackId> stairTracks) =>
        finisher is not null && (finisher.Fixed is not null || finisher.CardioTrackId is { } id && stairTracks.Contains(id));
}
