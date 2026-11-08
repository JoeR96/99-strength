using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Interfaces;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Meals;

/// <summary>
/// The current user's meals, by type then title. A user with no meals at all gets the starter set
/// seeded first, so the page is never empty on first visit.
/// </summary>
public sealed record GetMealsQuery : IQuery<Result<IReadOnlyList<MealDto>>>;

public sealed class GetMealsQueryHandler : IRequestHandler<GetMealsQuery, Result<IReadOnlyList<MealDto>>>
{
    private readonly IMealRepository _meals;
    private readonly IMealSeeder _seeder;
    private readonly ICurrentUserService _currentUser;

    public GetMealsQueryHandler(IMealRepository meals, IMealSeeder seeder, ICurrentUserService currentUser)
    {
        _meals = meals ?? throw new ArgumentNullException(nameof(meals));
        _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<IReadOnlyList<MealDto>>> Handle(GetMealsQuery request, CancellationToken cancellationToken)
    {
        var user = MealAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return MealAccess.Fail<IReadOnlyList<MealDto>>(user);
        }

        var meals = await _meals.ListForUserAsync(user.Value, cancellationToken);
        if (meals.Count == 0 && await _seeder.EnsureForUserAsync(user.Value, cancellationToken) > 0)
        {
            meals = await _meals.ListForUserAsync(user.Value, cancellationToken);
        }

        IReadOnlyList<MealDto> dtos = meals
            .OrderBy(m => m.Type)
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .Select(MealDto.From)
            .ToList();
        return Result.Success(dtos);
    }
}
