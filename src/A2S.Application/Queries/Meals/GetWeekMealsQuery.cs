using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Meals;

/// <summary>The menu for a week (this week when omitted). Any date in the week snaps to its Monday.</summary>
public sealed record GetWeekMealsQuery(DateOnly? Week = null) : IQuery<Result<WeekMealsDto>>;

public sealed class GetWeekMealsQueryHandler : IRequestHandler<GetWeekMealsQuery, Result<WeekMealsDto>>
{
    private readonly IShoppingListRepository _lists;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetWeekMealsQueryHandler(IShoppingListRepository lists, ICurrentUserService currentUser, ILocalClock clock)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<WeekMealsDto>> Handle(GetWeekMealsQuery request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<WeekMealsDto>(user);
        }

        var weekStart = ShoppingList.WeekStartOf(request.Week ?? _clock.Today());
        var list = await _lists.GetForWeekAsync(user.Value, weekStart, cancellationToken);

        return Result.Success(new WeekMealsDto(
            weekStart,
            list?.SourceMealIds.Select(id => id.Value).ToList() ?? (IReadOnlyList<Guid>)Array.Empty<Guid>()));
    }
}
