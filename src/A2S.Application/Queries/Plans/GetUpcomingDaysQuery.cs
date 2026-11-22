using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>
/// The next <paramref name="Days"/> days of the active plan from <paramref name="From"/> (today by default),
/// with session statuses. Today's "Up next" card picks the first day with a non-rest session from it.
/// </summary>
public sealed record GetUpcomingDaysQuery(DateOnly? From, int Days = 14) : IQuery<Result<IReadOnlyList<PlanDayDto>>>;

public sealed class GetUpcomingDaysQueryHandler : IRequestHandler<GetUpcomingDaysQuery, Result<IReadOnlyList<PlanDayDto>>>
{
    public const int MaxDays = 31;

    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IHevyLinkRepository _hevyLinks;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;

    public GetUpcomingDaysQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IHevyLinkRepository hevyLinks,
        ICurrentUserService currentUser,
        PlanCalendarService calendar,
        ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _hevyLinks = hevyLinks ?? throw new ArgumentNullException(nameof(hevyLinks));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<PlanDayDto>>> Handle(GetUpcomingDaysQuery request, CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > MaxDays)
        {
            return Result.Failure<IReadOnlyList<PlanDayDto>>($"Days must be 1–{MaxDays}.", ErrorCode.ValidationFailed);
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

        var plan = load.Value;
        var from = request.From ?? _clock.Today();
        var history = await PlanAccess.LiftingHistoryAsync(_workouts, plan, cancellationToken);
        var routines = await HevyRoutineLookup.LoadAsync(_hevyLinks, user.Value, cancellationToken);

        return Result.Success<IReadOnlyList<PlanDayDto>>(Enumerable.Range(0, request.Days)
            .Select(offset => _calendar.DayFor(plan, from.AddDays(offset), history).ToDto(plan.Id, routines))
            .ToList());
    }
}
