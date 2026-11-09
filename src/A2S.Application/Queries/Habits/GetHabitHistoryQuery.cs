using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Queries.Habits;

/// <summary>Per-habit, per-day values between two local dates (inclusive), archived habits included.</summary>
public sealed record GetHabitHistoryQuery(DateOnly From, DateOnly To) : IQuery<Result<HabitHistoryDto>>;

public sealed class GetHabitHistoryQueryValidator : AbstractValidator<GetHabitHistoryQuery>
{
    public const int MaxDays = 400;

    public GetHabitHistoryQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'to' must not be before 'from'.");
        RuleFor(x => x.To.DayNumber - x.From.DayNumber)
            .LessThan(MaxDays)
            .WithMessage($"History range cannot exceed {MaxDays} days.");
    }
}

public sealed class GetHabitHistoryQueryHandler : IRequestHandler<GetHabitHistoryQuery, Result<HabitHistoryDto>>
{
    private readonly IHabitRepository _habits;
    private readonly ICurrentUserService _currentUser;

    public GetHabitHistoryQueryHandler(IHabitRepository habits, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<HabitHistoryDto>> Handle(GetHabitHistoryQuery request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<HabitHistoryDto>(user);
        }

        var habits = await _habits.ListForUserAsync(user.Value, includeArchived: true, cancellationToken);
        var byId = habits.ToDictionary(h => h.Id);
        var logs = await _habits.GetLogsAsync(user.Value, request.From, request.To, cancellationToken);

        var entries = logs
            .Where(l => byId.ContainsKey(l.HabitId))
            .OrderBy(l => l.Date).ThenBy(l => byId[l.HabitId].Order)
            .Select(l =>
            {
                var habit = byId[l.HabitId];
                return new HabitHistoryEntryDto(
                    habit.Id.Value, l.Date, l.Value, HabitRules.MetMinimum(habit, l), HabitRules.MetTarget(habit, l));
            })
            .ToList();

        var dtos = habits.OrderBy(h => h.Order).ThenBy(h => h.Name).Select(HabitDto.From).ToList();
        return Result.Success(new HabitHistoryDto(request.From, request.To, dtos, entries));
    }
}
