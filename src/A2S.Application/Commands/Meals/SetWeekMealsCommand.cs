using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>
/// Sets which meals are on the menu for a week. Any date in the week is accepted and snapped to its
/// Monday. The menu lives on the week's shopping list; items change only on the next generate.
/// </summary>
public sealed record SetWeekMealsCommand(DateOnly WeekStart, IReadOnlyList<Guid> MealIds) : ICommand<Result<WeekMealsDto>>;

public sealed class SetWeekMealsCommandValidator : AbstractValidator<SetWeekMealsCommand>
{
    public SetWeekMealsCommandValidator()
    {
        RuleFor(x => x.MealIds).NotNull();
        RuleForEach(x => x.MealIds).NotEmpty();
    }
}

public sealed class SetWeekMealsCommandHandler : IRequestHandler<SetWeekMealsCommand, Result<WeekMealsDto>>
{
    private readonly IMealRepository _meals;
    private readonly IShoppingListRepository _lists;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetWeekMealsCommandHandler(
        IMealRepository meals,
        IShoppingListRepository lists,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _meals = meals ?? throw new ArgumentNullException(nameof(meals));
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<WeekMealsDto>> Handle(SetWeekMealsCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<WeekMealsDto>(user);
        }

        var wanted = request.MealIds.Distinct().Select(id => new MealId(id)).ToList();
        var owned = await _meals.GetManyAsync(user.Value, wanted, cancellationToken);
        if (owned.Count != wanted.Count)
        {
            return Result.Failure<WeekMealsDto>("One or more meals were not found.", ErrorCode.NotFound);
        }

        var weekStart = ShoppingList.WeekStartOf(request.WeekStart);
        var (list, created) = await MealAccess.LoadOrCreateListAsync(_lists, user.Value, weekStart, cancellationToken);
        list.SetSourceMeals(wanted);

        if (created)
        {
            await _lists.AddAsync(list, cancellationToken);
        }
        else
        {
            _lists.Update(list);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new WeekMealsDto(weekStart, list.SourceMealIds.Select(id => id.Value).ToList()));
    }
}
