using A2S.Application.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>
/// Deletes one of the current user's meals. A week that still lists it drops it at the next
/// shopping-list regeneration; already ticked lines are not touched here.
/// </summary>
public sealed record DeleteMealCommand(Guid MealId) : ICommand<Result>;

public sealed class DeleteMealCommandValidator : AbstractValidator<DeleteMealCommand>
{
    public DeleteMealCommandValidator() => RuleFor(x => x.MealId).NotEmpty();
}

public sealed class DeleteMealCommandHandler : IRequestHandler<DeleteMealCommand, Result>
{
    private readonly IMealRepository _meals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteMealCommandHandler(IMealRepository meals, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _meals = meals ?? throw new ArgumentNullException(nameof(meals));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteMealCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return user;
        }

        var owned = await MealAccess.LoadOwnedMealAsync(_meals, user.Value, request.MealId, cancellationToken);
        if (owned.IsFailure)
        {
            return owned;
        }

        _meals.Remove(owned.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
