using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Application.Queries.Meals;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.Meals;

public class MealQueryHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private readonly IMealRepository _meals = Substitute.For<IMealRepository>();
    private readonly IShoppingListRepository _lists = Substitute.For<IShoppingListRepository>();
    private readonly IMealSeeder _seeder = Substitute.For<IMealSeeder>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();

    public MealQueryHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _clock.Today().Returns(Monday.AddDays(2));
    }

    private static Meal MealNamed(string title, MealType type) =>
        Meal.Create(Joe, title, type, null, new[] { new Ingredient("x", 1, "g") }, null, null, true);

    [Fact]
    public async Task GetMeals_WhenTheUserHasNone_SeedsThenLists()
    {
        var oats = MealNamed("Oats", MealType.Breakfast);
        _meals.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(Array.Empty<Meal>(), new[] { oats });
        _seeder.EnsureForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(10);
        var handler = new GetMealsQueryHandler(_meals, _seeder, _currentUser);

        var result = await handler.Handle(new GetMealsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(m => m.Title).Should().Equal("Oats");
        await _seeder.Received(1).EnsureForUserAsync(Joe, Arg.Any<CancellationToken>());
        await _meals.Received(2).ListForUserAsync(Joe, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMeals_WhenTheUserHasSome_DoesNotSeed_AndOrdersByTypeThenTitle()
    {
        _meals.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new[]
        {
            MealNamed("Orzo", MealType.Dinner), MealNamed("bagel", MealType.Breakfast), MealNamed("Oats", MealType.Breakfast),
        });
        var handler = new GetMealsQueryHandler(_meals, _seeder, _currentUser);

        var result = await handler.Handle(new GetMealsQuery(), CancellationToken.None);

        result.Value.Select(m => m.Title).Should().Equal("bagel", "Oats", "Orzo");
        await _seeder.DidNotReceive().EnsureForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMeals_Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);
        var handler = new GetMealsQueryHandler(_meals, _seeder, _currentUser);

        var result = await handler.Handle(new GetMealsQuery(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Fact]
    public async Task GetWeekMeals_DefaultsToThisWeek_AndIsEmptyWithoutAList()
    {
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns((ShoppingList?)null);
        var handler = new GetWeekMealsQueryHandler(_lists, _currentUser, _clock);

        var result = await handler.Handle(new GetWeekMealsQuery(), CancellationToken.None);

        result.Value.WeekStart.Should().Be(Monday);
        result.Value.MealIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeekMeals_ReturnsTheMenu_ForAnyDateInTheWeek()
    {
        var orzo = new MealId(Guid.NewGuid());
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(ShoppingList.Create(Joe, Monday, new[] { orzo }));
        var handler = new GetWeekMealsQueryHandler(_lists, _currentUser, _clock);

        var result = await handler.Handle(new GetWeekMealsQuery(Monday.AddDays(6)), CancellationToken.None);

        result.Value.MealIds.Should().Equal(orzo.Value);
        _clock.DidNotReceive().Today();
    }

    [Fact]
    public async Task GetShoppingList_NoList_IsEmptyWithNullId()
    {
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns((ShoppingList?)null);
        var handler = new GetShoppingListQueryHandler(_lists, _currentUser, _clock);

        var result = await handler.Handle(new GetShoppingListQuery(), CancellationToken.None);

        result.Value.Id.Should().BeNull();
        result.Value.WeekStart.Should().Be(Monday);
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetShoppingList_ReturnsItemsAndCounts()
    {
        var orzo = new MealId(Guid.NewGuid());
        var list = ShoppingList.Create(Joe, Monday, new[] { orzo }, new[]
        {
            new ShoppingListItem("garlic", 2, "clove", true, new[] { orzo }),
            new ShoppingListItem("salt", null, null, false, new[] { orzo }),
        });
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);
        var handler = new GetShoppingListQueryHandler(_lists, _currentUser, _clock);

        var result = await handler.Handle(new GetShoppingListQuery(Monday), CancellationToken.None);

        result.Value.Id.Should().Be(list.Id.Value);
        result.Value.SourceMealIds.Should().Equal(orzo.Value);
        result.Value.CheckedCount.Should().Be(1);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].Should().BeEquivalentTo(new { Name = "garlic", Quantity = 2m, Unit = "clove", Checked = true });
        result.Value.Items[0].FromMealIds.Should().Equal(orzo.Value);
    }
}
