using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>Progress rings for the Mon–Sun week containing a date (this week by default).</summary>
public sealed record GetWeeklyGoalsQuery(DateOnly? AnyDay = null) : IQuery<Result<WeeklyGoalsDto>>;

public sealed class GetWeeklyGoalsQueryHandler : IRequestHandler<GetWeeklyGoalsQuery, Result<WeeklyGoalsDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;

    public GetWeeklyGoalsQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICurrentUserService currentUser,
        PlanCalendarService calendar,
        ILocalClock clock)
    {
        _plans = plans;
        _workouts = workouts;
        _currentUser = currentUser;
        _calendar = calendar;
        _clock = clock;
    }

    public async Task<Result<WeeklyGoalsDto>> Handle(GetWeeklyGoalsQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<WeeklyGoalsDto>(user);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<WeeklyGoalsDto>(load);
        }

        var history = await PlanAccess.LiftingHistoryAsync(_workouts, load.Value, cancellationToken);
        var goals = _calendar.ForWeek(load.Value, request.AnyDay ?? _clock.Today(), history);

        return Result.Success(goals.ToDto());
    }
}
