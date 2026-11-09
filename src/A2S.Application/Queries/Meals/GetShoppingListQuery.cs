using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Meals;

/// <summary>The shopping list for a week (this week when omitted); an empty list when none has been generated.</summary>
public sealed record GetShoppingListQuery(DateOnly? Week = null) : IQuery<Result<ShoppingListDto>>;

public sealed class GetShoppingListQueryHandler : IRequestHandler<GetShoppingListQuery, Result<ShoppingListDto>>
{
    private readonly IShoppingListRepository _lists;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetShoppingListQueryHandler(IShoppingListRepository lists, ICurrentUserService currentUser, ILocalClock clock)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<ShoppingListDto>> Handle(GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<ShoppingListDto>(user);
        }

        var weekStart = ShoppingList.WeekStartOf(request.Week ?? _clock.Today());
        var list = await _lists.GetForWeekAsync(user.Value, weekStart, cancellationToken);

        return Result.Success(list is null ? ShoppingListDto.Empty(weekStart) : ShoppingListDto.From(list));
    }
}
