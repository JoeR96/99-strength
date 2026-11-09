using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>Replaces a meal's content. The owner and the seed key never change.</summary>
public sealed record UpdateMealCommand(
    Guid MealId,
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType Type,
    string? Description,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string>? Steps,
    IReadOnlyList<string>? Tags,
    bool CutFriendly) : ICommand<Result<MealDto>>;

public sealed class UpdateMealCommandValidator : AbstractValidator<UpdateMealCommand>
{
    public UpdateMealCommandValidator()
    {
        RuleFor(x => x.MealId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Meal.MaxTitleLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(Meal.MaxDescriptionLength);
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("A meal needs at least one ingredient.");
        RuleForEach(x => x.Ingredients).SetValidator(new IngredientDtoValidator());
    }
}

public sealed class UpdateMealCommandHandler : IRequestHandler<UpdateMealCommand, Result<MealDto>>
{
    private readonly IMealRepository _meals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateMealCommandHandler(IMealRepository meals, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _meals = meals ?? throw new ArgumentNullException(nameof(meals));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<MealDto>> Handle(UpdateMealCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<MealDto>(user);
        }

        var owned = await MealAccess.LoadOwnedMealAsync(_meals, user.Value, request.MealId, cancellationToken);
        if (owned.IsFailure)
        {
            return MealAccess.Fail<MealDto>(owned);
        }

        try
        {
            var meal = owned.Value;
            meal.Update(
                request.Title,
                request.Type,
                request.Description,
                request.Ingredients.Select(i => i.ToDomain()),
                request.Steps,
                request.Tags,
                request.CutFriendly);

            _meals.Update(meal);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(MealDto.From(meal));
        }
        catch (DomainException ex)
        {
            return MealAccess.FromDomain<MealDto>(ex);
        }
    }
}
