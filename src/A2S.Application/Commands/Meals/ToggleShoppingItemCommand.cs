using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>Ticks or unticks one line, matched by normalised name and unit.</summary>
public sealed record ToggleShoppingItemCommand(Guid ListId, string Name, string? Unit) : ICommand<Result<ShoppingListDto>>;

public sealed class ToggleShoppingItemCommandValidator : AbstractValidator<ToggleShoppingItemCommand>
{
    public ToggleShoppingItemCommandValidator()
    {
        RuleFor(x => x.ListId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(20);
    }
}

public sealed class ToggleShoppingItemCommandHandler : IRequestHandler<ToggleShoppingItemCommand, Result<ShoppingListDto>>
{
    private readonly IShoppingListRepository _lists;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ToggleShoppingItemCommandHandler(IShoppingListRepository lists, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<ShoppingListDto>> Handle(ToggleShoppingItemCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<ShoppingListDto>(user);
        }

        var owned = await MealAccess.LoadOwnedListAsync(_lists, user.Value, request.ListId, cancellationToken);
        if (owned.IsFailure)
        {
            return MealAccess.Fail<ShoppingListDto>(owned);
        }

        var list = owned.Value;
        if (!list.Toggle(request.Name, request.Unit))
        {
            return Result.Failure<ShoppingListDto>("Shopping list item not found.", ErrorCode.NotFound);
        }

        _lists.Update(list);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ShoppingListDto.From(list));
    }
}
