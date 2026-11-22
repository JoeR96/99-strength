using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>One day of the active plan (today by default) with its sessions, phase and the week's goals.</summary>
public sealed record GetTodayQuery(DateOnly? Date = null) : IQuery<Result<TodayDto>>;

public sealed class GetTodayQueryHandler : IRequestHandler<GetTodayQuery, Result<TodayDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IHevyLinkRepository _hevyLinks;
    private readonly IHevyWorkoutPostRepository _posts;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;

    public GetTodayQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IHevyLinkRepository hevyLinks,
        IHevyWorkoutPostRepository posts,
        ICurrentUserService currentUser,
        PlanCalendarService calendar,
        ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _hevyLinks = hevyLinks ?? throw new ArgumentNullException(nameof(hevyLinks));
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<TodayDto>> Handle(GetTodayQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TodayDto>(user);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<TodayDto>(load);
        }

        var plan = load.Value;
        var date = request.Date ?? _clock.Today();
        var history = await PlanAccess.LiftingHistoryAsync(_workouts, plan, cancellationToken);

        var routines = await HevyRoutineLookup.LoadAsync(_hevyLinks, user.Value, cancellationToken);

        var day = _calendar.DayFor(plan, date, history);
        var week = _calendar.ForWeek(plan, date, history);

        var posts = await _posts.ListForUserAsync(user.Value, day.Date, day.Date, cancellationToken);
        var scheduled = plan.SessionsOn(day.Date);
        var sessions = day.ToDto(plan.Id, routines).Sessions.Select(s => WithGymMode(s, scheduled, posts)).ToList();

        return Result.Success(new TodayDto(day.Date, day.Phase, sessions, week.ToDto()));
    }

    /// <summary>Adds the lift slot's warm-up and finisher, and the Hevy post status when it was finished in gym mode.</summary>
    private static TodaySessionDto WithGymMode(TodaySessionDto session, IReadOnlyList<ScheduledSession> scheduled, IReadOnlyList<HevyWorkoutPost> posts)
    {
        if (session.Kind != SessionKind.Lift)
        {
            return session;
        }

        var slot = scheduled.FirstOrDefault(s => s.Kind == SessionKind.Lift && s.LiftDay == session.LiftDay && s.Label == session.Label);
        var post = session.CompletionId is { } completionId
            ? posts.LastOrDefault(p => p.CompletionId?.Value == completionId)
            : null;

        return session with
        {
            WarmUpRoutineId = slot?.WarmUpRoutineId?.Value,
            FinisherCardioProtocolId = slot?.Finisher?.CardioProtocolId?.Value,
            FinisherCardioTrackId = slot?.Finisher?.CardioTrackId?.Value,
            FinisherFixed = slot?.Finisher?.Fixed?.ToDto(),
            GymSessionId = post?.ClientSessionId,
            HevyPostStatus = post?.Status.ToString()
        };
    }
}
