using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Habits;

/// <summary>The current user's habit definitions, in display order.</summary>
public sealed record GetHabitsQuery(bool IncludeArchived = false) : IQuery<Result<IReadOnlyList<HabitDto>>>;

public sealed class GetHabitsQueryHandler : IRequestHandler<GetHabitsQuery, Result<IReadOnlyList<HabitDto>>>
{
    private readonly IHabitRepository _habits;
    private readonly ICurrentUserService _currentUser;

    public GetHabitsQueryHandler(IHabitRepository habits, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<IReadOnlyList<HabitDto>>> Handle(GetHabitsQuery request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<IReadOnlyList<HabitDto>>(user);
        }

        var habits = await _habits.ListForUserAsync(user.Value, request.IncludeArchived, cancellationToken);
        IReadOnlyList<HabitDto> dtos = habits.OrderBy(h => h.Order).ThenBy(h => h.Name).Select(HabitDto.From).ToList();
        return Result.Success(dtos);
    }
}
