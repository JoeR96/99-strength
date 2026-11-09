using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Meals;

/// <summary>Creates a meal for the current user.</summary>
public sealed record CreateMealCommand(
    string Title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MealType>))] MealType Type,
    string? Description,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string>? Steps,
    IReadOnlyList<string>? Tags,
    bool CutFriendly) : ICommand<Result<MealDto>>;

public sealed class CreateMealCommandValidator : AbstractValidator<CreateMealCommand>
{
    public CreateMealCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Meal.MaxTitleLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(Meal.MaxDescriptionLength);
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("A meal needs at least one ingredient.");
        RuleForEach(x => x.Ingredients).SetValidator(new IngredientDtoValidator());
    }
}

/// <summary>An ingredient line: a name, and a positive quantity with a known unit or neither.</summary>
public sealed class IngredientDtoValidator : AbstractValidator<IngredientDto>
{
    public IngredientDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit)
            .Must(u => IngredientUnits.IsKnown(IngredientUnits.Normalise(u)))
            .When(x => !string.IsNullOrWhiteSpace(x.Unit))
            .WithMessage(x => $"Unknown unit '{x.Unit}'. Use one of: {string.Join(", ", IngredientUnits.All)}.");
        RuleFor(x => x)
            .Must(x => x.Quantity.HasValue == !string.IsNullOrWhiteSpace(x.Unit))
            .WithMessage("An ingredient has both a quantity and a unit, or neither.");
    }
}

public sealed class CreateMealCommandHandler : IRequestHandler<CreateMealCommand, Result<MealDto>>
{
    private readonly IMealRepository _meals;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateMealCommandHandler(IMealRepository meals, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _meals = meals ?? throw new ArgumentNullException(nameof(meals));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<MealDto>> Handle(CreateMealCommand request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<MealDto>(user);
        }

        try
        {
            var meal = Meal.Create(
                user.Value,
                request.Title,
                request.Type,
                request.Description,
                request.Ingredients.Select(i => i.ToDomain()),
                request.Steps,
                request.Tags,
                request.CutFriendly);

            await _meals.AddAsync(meal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(MealDto.From(meal));
        }
        catch (DomainException ex)
        {
            return MealAccess.FromDomain<MealDto>(ex);
        }
    }
}
