using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>Every day of a calendar month for the active plan, with session statuses.</summary>
public sealed record GetPlanCalendarQuery(int Year, int Month) : IQuery<Result<IReadOnlyList<PlanDayDto>>>;

public sealed class GetPlanCalendarQueryHandler : IRequestHandler<GetPlanCalendarQuery, Result<IReadOnlyList<PlanDayDto>>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;

    public GetPlanCalendarQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICurrentUserService currentUser,
        PlanCalendarService calendar)
    {
        _plans = plans;
        _workouts = workouts;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyList<PlanDayDto>>> Handle(GetPlanCalendarQuery request, CancellationToken cancellationToken)
    {
        if (request.Month is < 1 or > 12 || request.Year is < 2000 or > 2100)
        {
            return Result.Failure<IReadOnlyList<PlanDayDto>>("Month must be 1–12 and year 2000–2100.", ErrorCode.ValidationFailed);
        }

        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<PlanDayDto>>(user);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<PlanDayDto>>(load);
        }

        var history = await PlanAccess.LiftingHistoryAsync(_workouts, load.Value, cancellationToken);
        var days = _calendar.Month(load.Value, request.Year, request.Month, history);

        return Result.Success<IReadOnlyList<PlanDayDto>>(days.Select(d => d.ToDto()).ToList());
    }
}
