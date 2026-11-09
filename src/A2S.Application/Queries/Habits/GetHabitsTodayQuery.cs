using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Habits;

/// <summary>The habit screen for a local date (today when omitted): every active habit with its value and the streak.</summary>
public sealed record GetHabitsTodayQuery(DateOnly? Date = null) : IQuery<Result<HabitsTodayDto>>;

public sealed class GetHabitsTodayQueryHandler : IRequestHandler<GetHabitsTodayQuery, Result<HabitsTodayDto>>
{
    private readonly IHabitRepository _habits;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;
    private readonly HabitStreakService _streaks;

    public GetHabitsTodayQueryHandler(
        IHabitRepository habits,
        ICurrentUserService currentUser,
        ILocalClock clock,
        HabitStreakService streaks)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _streaks = streaks ?? throw new ArgumentNullException(nameof(streaks));
    }

    public async Task<Result<HabitsTodayDto>> Handle(GetHabitsTodayQuery request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<HabitsTodayDto>(user);
        }

        var date = request.Date ?? _clock.Today();
        var habits = await _habits.ListForUserAsync(user.Value, includeArchived: false, cancellationToken);
        var logs = await _habits.GetLogsAsync(user.Value, DateOnly.MinValue, date, cancellationToken);

        var todays = logs.Where(l => l.Date == date).ToDictionary(l => l.HabitId);
        var items = habits
            .OrderBy(h => h.Order).ThenBy(h => h.Name)
            .Select(h =>
            {
                var log = todays.GetValueOrDefault(h.Id);
                return new HabitTodayDto(
                    h.Id.Value, h.Name, h.Type, h.Target, h.Minimum, h.Unit,
                    log?.Value ?? 0,
                    HabitRules.MetMinimum(h, log),
                    HabitRules.MetTarget(h, log),
                    h.Order, h.Icon);
            })
            .ToList();

        var streak = _streaks.Compute(habits, logs, date);

        return Result.Success(new HabitsTodayDto(date, items, HabitStreakDto.From(streak)));
    }
}
