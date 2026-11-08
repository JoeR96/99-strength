using A2S.Application.Common;
using A2S.Application.DTOs;
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
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;

    public GetTodayQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICurrentUserService currentUser,
        PlanCalendarService calendar,
        ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
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

        var day = _calendar.DayFor(plan, date, history);
        var week = _calendar.ForWeek(plan, date, history);

        return Result.Success(new TodayDto(day.Date, day.Phase, day.Sessions.Select(s => s.ToDto()).ToList(), week.ToDto()));
    }
}
