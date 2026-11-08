using A2S.Application.Common;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2S.Application.Push;

/// <summary>
/// One tick of the nudge loop (the hosted service calls <see cref="RunOnceAsync"/> every minute inside a
/// scope). For every user with a subscription: the "time to train" nudge at their session start time
/// when a planned session is still waiting, and the "still under minimum" nudge at 20:00 local. Each
/// goes out once per user per day (<see cref="PushNudgeLog"/>); a dead subscription (404/410) is
/// removed. With no VAPID keys configured the tick is a no-op.
/// </summary>
public sealed class PushNudgeService
{
    private readonly IPushSubscriptionRepository _pushes;
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IHabitRepository _habits;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;
    private readonly IPushSender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PushOptions _options;
    private readonly ILogger<PushNudgeService> _logger;

    public PushNudgeService(
        IPushSubscriptionRepository pushes,
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IHabitRepository habits,
        PlanCalendarService calendar,
        ILocalClock clock,
        IPushSender sender,
        IUnitOfWork unitOfWork,
        IOptions<PushOptions> options,
        ILogger<PushNudgeService> logger)
    {
        _pushes = pushes ?? throw new ArgumentNullException(nameof(pushes));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Evaluates every subscribed user once. Returns how many nudges were sent (to any device).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            return 0;
        }

        var nowUtc = _clock.NowUtc();
        var today = _clock.Today();
        var nowLocal = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc.UtcDateTime, LocalClock.Zone));

        var sent = 0;
        foreach (var userId in await _pushes.ListSubscribedUsersAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var subscriptions = await _pushes.ListForUserAsync(userId, ct);
            if (subscriptions.Count == 0)
            {
                continue;
            }

            var nudges = new List<PushNudge>(2);

            var session = await SessionNudgeAsync(userId, subscriptions, today, nowLocal, ct);
            if (session is not null)
            {
                nudges.Add(session);
            }

            var habits = await HabitsNudgeAsync(userId, today, nowLocal, ct);
            if (habits is not null)
            {
                nudges.Add(habits);
            }

            if (nudges.Count == 0)
            {
                continue;
            }

            foreach (var nudge in nudges)
            {
                await SendAsync(userId, subscriptions, nudge, ct);
                await _pushes.AddNudgeAsync(PushNudgeLog.Create(userId, today, nudge.Kind, nowUtc), ct);
                sent++;
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        return sent;
    }

    private async Task<PushNudge?> SessionNudgeAsync(
        UserId userId,
        IReadOnlyList<Domain.Aggregates.Push.PushSubscription> subscriptions,
        DateOnly today,
        TimeOnly nowLocal,
        CancellationToken ct)
    {
        // Devices may ask for different times; the earliest wins and every device gets the nudge.
        var sessionStart = subscriptions.Min(s => s.SessionStartTime);
        if (!PushNudgePlanner.IsDue(nowLocal, sessionStart))
        {
            return null;
        }

        if (await _pushes.HasNudgeAsync(userId, today, PushNudgeKind.SessionStart, ct))
        {
            return null;
        }

        var plan = await _plans.GetActiveForUserAsync(userId, ct);
        if (plan is null || plan.IsPaused(today))
        {
            return null;
        }

        var history = await PlanAccess.LiftingHistoryAsync(_workouts, plan, ct);
        var day = _calendar.DayFor(plan, today, history);
        var sessions = day.Sessions.Select(s => new PlannedSession(s.Session.Label, s.Session.Kind, s.Status)).ToList();
        return PushNudgePlanner.SessionNudge(nowLocal, sessionStart, sessions);
    }

    private async Task<PushNudge?> HabitsNudgeAsync(UserId userId, DateOnly today, TimeOnly nowLocal, CancellationToken ct)
    {
        if (!PushNudgePlanner.IsDue(nowLocal, PushNudgePlanner.HabitsReminderTime))
        {
            return null;
        }

        if (await _pushes.HasNudgeAsync(userId, today, PushNudgeKind.HabitsUnderMinimum, ct))
        {
            return null;
        }

        var definitions = await _habits.ListForUserAsync(userId, includeArchived: false, ct);
        if (definitions.Count == 0)
        {
            return null;
        }

        var logs = await _habits.GetLogsAsync(userId, today, today, ct);
        var standings = definitions
            .Select(d => new HabitStanding(d.Name, logs.FirstOrDefault(l => l.HabitId == d.Id)?.Value ?? 0, d.Minimum))
            .ToList();
        return PushNudgePlanner.HabitsNudge(nowLocal, standings);
    }

    private async Task SendAsync(
        UserId userId,
        IReadOnlyList<Domain.Aggregates.Push.PushSubscription> subscriptions,
        PushNudge nudge,
        CancellationToken ct)
    {
        var message = nudge.ToMessage();
        foreach (var subscription in subscriptions)
        {
            var status = await _sender.SendAsync(new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth), message, ct);
            switch (status)
            {
                case PushSendStatus.Gone:
                    _logger.LogInformation("Push subscription {SubscriptionId} for user {UserId} is gone; removing it", subscription.Id.Value, userId.Value);
                    _pushes.Remove(subscription);
                    break;
                case PushSendStatus.Failed:
                    _logger.LogWarning("Push {Kind} to subscription {SubscriptionId} failed", nudge.Kind, subscription.Id.Value);
                    break;
            }
        }
    }
}
