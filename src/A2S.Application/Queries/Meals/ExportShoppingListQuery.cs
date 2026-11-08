using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Meals;

/// <summary>The week's shopping list as text or CSV, with the content type and a file name to suggest.</summary>
public sealed record ExportShoppingListQuery(DateOnly? Week, ShoppingListExportFormat Format) : IQuery<Result<ShoppingListExportDto>>;

public sealed class ExportShoppingListQueryHandler : IRequestHandler<ExportShoppingListQuery, Result<ShoppingListExportDto>>
{
    private readonly IShoppingListRepository _lists;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public ExportShoppingListQueryHandler(IShoppingListRepository lists, ICurrentUserService currentUser, ILocalClock clock)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<ShoppingListExportDto>> Handle(ExportShoppingListQuery request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<ShoppingListExportDto>(user);
        }

        var weekStart = ShoppingList.WeekStartOf(request.Week ?? _clock.Today());
        var list = await _lists.GetForWeekAsync(user.Value, weekStart, cancellationToken);
        var dto = list is null ? ShoppingListDto.Empty(weekStart) : ShoppingListDto.From(list);

        return Result.Success(ShoppingListExport.Export(dto, request.Format));
    }
}
