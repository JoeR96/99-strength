using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>
/// Builds (or rebuilds) the week's shopping list from the meals on its menu. Lines whose
/// (name, unit) was already ticked stay ticked; lines no longer needed are dropped; meals that have
/// since been deleted fall off the menu.
/// </summary>
public sealed record GenerateShoppingListCommand(DateOnly WeekStart) : ICommand<Result<ShoppingListDto>>;

public sealed class GenerateShoppingListCommandValidator : AbstractValidator<GenerateShoppingListCommand>
{
    public GenerateShoppingListCommandValidator() => RuleFor(x => x.WeekStart).NotEqual(default(DateOnly));
}

public sealed class GenerateShoppingListCommandHandler : IRequestHandler<GenerateShoppingListCommand, Result<ShoppingListDto>>
{
    private readonly IMealRepository _meals;
    private readonly IShoppingListRepository _lists;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public GenerateShoppingListCommandHandler(
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

    public async Task<Result<ShoppingListDto>> Handle(GenerateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<ShoppingListDto>(user);
        }

        var weekStart = ShoppingList.WeekStartOf(request.WeekStart);
        var (list, created) = await MealAccess.LoadOrCreateListAsync(_lists, user.Value, weekStart, cancellationToken);

        var meals = await _meals.GetManyAsync(user.Value, list.SourceMealIds, cancellationToken);
        var byId = meals.ToDictionary(m => m.Id);
        var ordered = list.SourceMealIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

        list.SetSourceMeals(ordered.Select(m => m.Id));
        list.Regenerate(ShoppingListGenerator.Build(ordered));

        if (created)
        {
            await _lists.AddAsync(list, cancellationToken);
        }
        else
        {
            _lists.Update(list);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ShoppingListDto.From(list));
    }
}
