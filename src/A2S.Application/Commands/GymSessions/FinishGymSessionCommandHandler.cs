using System.Text.Json;
using A2S.Application.Cardio;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Commands.GymSessions;

/// <summary>
/// Orchestrates a gym-mode finish. The plan completions and the <see cref="HevyWorkoutPost"/> are staged
/// on the unit of work first, then <see cref="CompleteDayCommand"/> runs unchanged and its save commits
/// everything together (one SaveChanges, one database transaction), so a session is never half finished.
/// The Hevy post is then tried once; if Hevy is down the session is still complete and the background
/// retry picks the post up.
/// </summary>
public sealed class FinishGymSessionCommandHandler : IRequestHandler<FinishGymSessionCommand, Result<FinishGymSessionResult>>
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string AlreadyFinishedElsewhere = "This day was already finished on another device";

    private readonly ISender _sender;
    private readonly IWorkoutRepository _workouts;
    private readonly ITrainingPlanRepository _plans;
    private readonly IMobilityRoutineRepository _routines;
    private readonly IHevyWorkoutPostRepository _posts;
    private readonly IHevyKeyResolver _keys;
    private readonly HevyWorkoutPoster _poster;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;
    private readonly ILogger<FinishGymSessionCommandHandler> _logger;
    private readonly CardioTrackSessionRecorder? _trackRecorder;

    public FinishGymSessionCommandHandler(
        ISender sender,
        IWorkoutRepository workouts,
        ITrainingPlanRepository plans,
        IMobilityRoutineRepository routines,
        IHevyWorkoutPostRepository posts,
        IHevyKeyResolver keys,
        HevyWorkoutPoster poster,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        ILogger<FinishGymSessionCommandHandler> logger,
        CardioTrackSessionRecorder? trackRecorder = null)
    {
        _trackRecorder = trackRecorder;
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _poster = poster ?? throw new ArgumentNullException(nameof(poster));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<FinishGymSessionResult>> Handle(FinishGymSessionCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<FinishGymSessionResult>(user);
        }

        var userId = user.Value;
        var existing = await _posts.FindByClientSessionAsync(userId, request.ClientSessionId, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(Replay(existing));
        }

        var workout = await _workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout is null || workout.UserId != userId)
        {
            return Result.Failure<FinishGymSessionResult>("Workout not found.", ErrorCode.NotFound);
        }

        // Two phones: the other one finished this week's day first (its own client session id), so this
        // finish must not record the day or move the progression a second time.
        if (workout.IsDayCompletedInCurrentWeek(request.Day))
        {
            return Result.Failure<FinishGymSessionResult>(AlreadyFinishedElsewhere, ErrorCode.Conflict);
        }

        var date = _clock.ToLocalDate(request.StartedAt);
        var week = workout.CurrentWeek;
        var plan = await _plans.GetActiveForUserAsync(userId, cancellationToken);
        if (plan is not null && plan.LinkedWorkoutId != workout.Id)
        {
            plan = null;
        }

        var session = plan?.SessionsOn(date).FirstOrDefault(s => s.Kind == SessionKind.Lift && s.LiftDay == request.Day)
            ?? plan?.Schedule.FirstOrDefault(s => s.Kind == SessionKind.Lift && s.LiftDay == request.Day);
        var warmUpRoutineId = session?.WarmUpRoutineId ?? await DefaultWarmUpAsync(userId, cancellationToken);
        var title = plan is not null && session is not null
            ? RoutineBuilder.LiftTitle(plan.Name, week, session.Label)
            : RoutineBuilder.LiftTitle(workout.Name, week, $"Day {(int)request.Day}");

        SessionCompletion? lift = null;
        SessionCompletion? finisher = null;
        var before = plan?.Completions.Select(c => c.Id).ToHashSet() ?? new HashSet<SessionCompletionId>();
        try
        {
            if (plan is not null)
            {
                lift = plan.CompleteSession(date, SessionKind.Lift, CompletionSource.Manual, liftDay: request.Day, recordedAt: request.EndedAt);
                if (request.Finisher is { } log && session?.Finisher is { } attached)
                {
                    finisher = plan.CompleteSession(date, SessionKind.Cardio, CompletionSource.Manual,
                        cardio: attached.CardioProtocolId, log: FinisherCardioLog(log), recordedAt: request.EndedAt,
                        cardioTrack: attached.CardioTrackId);
                    // A fixed cool-down is a plain cardio log on the day: no protocol, no track, nothing evaluated.
                }

                _plans.Update(plan);
            }

            var payload = new GymSessionPayload
            {
                WorkoutId = request.WorkoutId,
                Day = request.Day,
                WarmUpRoutineId = warmUpRoutineId?.Value,
                WarmUpTicks = request.WarmUpTicks,
                Performances = request.Performances,
                FinisherProtocolId = session?.Finisher?.CardioProtocolId?.Value,
                FinisherTrackId = session?.Finisher?.CardioTrackId?.Value,
                FinisherFixedKind = session?.Finisher?.Fixed?.Kind.ToString(),
                FinisherFixedNotes = session?.Finisher?.Fixed?.Notes,
                FinisherPrescription = session?.Finisher?.CardioTrackId is { } finisherTrack && _trackRecorder is not null
                    ? await _trackRecorder.PrescriptionTextAsync(userId, finisherTrack, cancellationToken)
                    : null,
                Finisher = request.Finisher
            };

            var post = HevyWorkoutPost.Create(userId, request.ClientSessionId, plan?.Id, lift?.Id, date, title,
                request.StartedAt, request.EndedAt, JsonSerializer.Serialize(payload, Json), "{}", _clock.NowUtc());
            await _posts.AddAsync(post, cancellationToken);

            // CompleteDay's own save commits the staged plan completions and the post with the A2S2 day.
            var day = await _sender.Send(new CompleteDayCommand(request.WorkoutId, request.Day, request.Performances), cancellationToken);
            if (day.IsFailure)
            {
                Unstage(plan, before, post);
                return Result.Failure<FinishGymSessionResult>(day.Error, day.ErrorCode == ErrorCode.None ? ErrorCode.DomainRuleViolation : day.ErrorCode);
            }

            var result = new FinishGymSessionResult
            {
                ClientSessionId = request.ClientSessionId,
                CompleteDay = day.Value,
                CompletionId = lift?.Id.Value,
                FinisherCompletionId = finisher?.Id.Value
            };

            // A track finisher is evaluated once, keyed on its completion (a replay never gets here).
            if (finisher is { CardioTrackId: { } trackId } && _trackRecorder is not null && request.Finisher is { } logged)
            {
                var prescribed = await _trackRecorder.PrescriptionAsync(userId, trackId, cancellationToken);
                var recorded = await _trackRecorder.RecordAsync(userId, trackId, finisher.TrackSessionKey, date, finisher.Cardio,
                    logged.CompletedAsPrescribed && logged.Pain != PainFlag.Stop, _clock.NowUtc(), cancellationToken, plan);
                if (recorded is { } r && prescribed is not null)
                {
                    result = result with { FinisherOutcome = PlannedCardioBuilder.ForEvaluation(r.Track, prescribed, r.Result) };
                }
            }
            else if (finisher is not null && session?.Finisher is { } other)
            {
                result = result with
                {
                    FinisherOutcome = PlannedCardioBuilder.Logged(
                        other.Fixed is not null ? PlannedCardioBuilder.FixedKind : PlannedCardioBuilder.ProtocolKind,
                        other.Fixed?.DisplayName() ?? "Finisher")
                };
            }

            post.RecordResult(JsonSerializer.Serialize(result, Json));
            if (string.IsNullOrEmpty(await _keys.ForUserAsync(userId, cancellationToken)))
            {
                post.MarkSkipped("no Hevy key");
            }

            _posts.Update(post);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (post.Status == HevyPostStatus.Pending)
            {
                await TryPostAsync(post, cancellationToken);
            }

            return Result.Success(result with { HevyStatus = post.Status.ToString(), HevyWorkoutId = post.HevyWorkoutId });
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<FinishGymSessionResult>(ex);
        }
    }

    /// <summary>The finisher as a cardio log: Easy/Solid/Brutal (RPE 4/7/9), stair level, floors and any pain flag.</summary>
    public static CardioLog FinisherCardioLog(FinisherLogRequest log) => new(
        log.DurationSeconds,
        null,
        log.Effort.ToRpe(),
        null,
        null,
        effort: log.Effort switch { Effort.Easy => EffortRating.Easy, Effort.Brutal => EffortRating.Brutal, _ => EffortRating.Solid },
        intensityUnit: log.Level is null ? null : IntensityUnit.Level,
        intensityValue: log.Level,
        floors: log.Floors,
        pain: log.Pain,
        painArea: log.PainArea) { WorkMinutes = log.WorkMinutes is > 0 ? log.WorkMinutes : null };

    private async Task TryPostAsync(HevyWorkoutPost post, CancellationToken ct)
    {
        try
        {
            await _poster.PostAsync(post, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The session is complete either way; the background retry sends it later.
            _logger.LogWarning(ex, "Gym session Hevy post failed; it stays queued");
        }
    }

    private FinishGymSessionResult Replay(HevyWorkoutPost post)
    {
        FinishGymSessionResult? stored = null;
        try
        {
            stored = JsonSerializer.Deserialize<FinishGymSessionResult>(post.ResultJson, Json);
        }
        catch (JsonException)
        {
            // fall through to the minimal answer
        }

        return (stored ?? new FinishGymSessionResult()) with
        {
            ClientSessionId = post.ClientSessionId,
            CompletionId = stored?.CompletionId ?? post.CompletionId?.Value,
            HevyStatus = post.Status.ToString(),
            HevyWorkoutId = post.HevyWorkoutId,
            Replayed = true
        };
    }

    /// <summary>CompleteDay refused: take back what was staged so nothing of this finish is saved.</summary>
    private void Unstage(TrainingPlan? plan, HashSet<SessionCompletionId> before, HevyWorkoutPost post)
    {
        _posts.Remove(post);
        if (plan is null)
        {
            return;
        }

        foreach (var added in plan.Completions.Where(c => !before.Contains(c.Id)).Select(c => c.Id).ToList())
        {
            plan.UndoCompletion(added);
        }
    }

    private async Task<MobilityRoutineId?> DefaultWarmUpAsync(UserId userId, CancellationToken ct)
    {
        var routines = await _routines.ListForUserAsync(userId, ct);
        return routines.FirstOrDefault(r => string.Equals(r.Name, CutTo85Nov2026.MobilityAName, StringComparison.OrdinalIgnoreCase))?.Id;
    }
}
