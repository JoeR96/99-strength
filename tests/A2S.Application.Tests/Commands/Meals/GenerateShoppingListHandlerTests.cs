using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Meals;

/// <summary>Review focus 3 and 4 of the Phase 10 plan: regenerating keeps ticks; a removed meal drops its lines.</summary>
public class GenerateShoppingListHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private readonly IMealRepository _meals = Substitute.For<IMealRepository>();
    private readonly IShoppingListRepository _lists = Substitute.For<IShoppingListRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly Meal _orzo = Meal.Create(Joe, "Orzo", MealType.Dinner, null,
        new[] { new Ingredient("garlic", 2, "clove"), new Ingredient("orzo", 200, "g"), Ingredient.Unquantified("salt") }, null, null, true);

    private readonly Meal _barbacoa = Meal.Create(Joe, "Barbacoa", MealType.Dinner, null,
        new[] { new Ingredient("garlic", 4, "clove"), new Ingredient("beef chuck", 1.2m, "kg"), new Ingredient("salt", 1, "tsp") }, null, null, true);

    public GenerateShoppingListHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _meals.GetManyAsync(Joe, Arg.Any<IEnumerable<MealId>>(), Arg.Any<CancellationToken>())
            .Returns(call => new[] { _orzo, _barbacoa }.Where(m => call.Arg<IEnumerable<MealId>>().Contains(m.Id)).ToList());
    }

    private GenerateShoppingListCommandHandler Handler() => new(_meals, _lists, _unitOfWork, _currentUser);

    [Fact]
    public async Task FirstGenerate_OnAWeekWithNoList_CreatesAnEmptyList()
    {
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns((ShoppingList?)null);

        var result = await Handler().Handle(new GenerateShoppingListCommand(Monday.AddDays(3)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeNull();
        result.Value.WeekStart.Should().Be(Monday);
        result.Value.Items.Should().BeEmpty();
        await _lists.Received(1).AddAsync(Arg.Is<ShoppingList>(l => l.WeekStart == Monday), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SumsAcrossTheWeeksMeals()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { _orzo.Id, _barbacoa.Id });
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);

        var result = await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => (i.Name, i.Quantity, i.Unit)).Should().Equal(
            ("beef chuck", 1.2m, "kg"),
            ("garlic", 6m, "clove"),
            ("orzo", 200m, "g"),
            ("salt", (decimal?)null, (string?)null),
            ("salt", 1m, "tsp"));
        result.Value.Items.Single(i => i.Name == "garlic").FromMealIds.Should().Equal(_orzo.Id.Value, _barbacoa.Id.Value);
        result.Value.TotalCount.Should().Be(5);
        _lists.Received(1).Update(list);
        await _lists.DidNotReceive().AddAsync(Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Regenerate_SameWeek_KeepsItemsAlreadyTicked()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { _orzo.Id, _barbacoa.Id });
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);
        await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);
        list.Toggle("garlic", "clove");
        list.Toggle("salt", null);

        var again = await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);

        again.Value.Items.Where(i => i.Checked).Select(i => (i.Name, i.Unit)).Should().Equal(("garlic", "clove"), ("salt", (string?)null));
        again.Value.Items.Single(i => i.Name == "garlic").Quantity.Should().Be(6);
        again.Value.CheckedCount.Should().Be(2);
    }

    [Fact]
    public async Task MealRemovedMidWeek_DropsItsLinesOnTheNextGenerate_ButKeepsTicksOnTheRest()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { _orzo.Id, _barbacoa.Id });
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);
        await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);
        list.Toggle("garlic", "clove");
        list.Toggle("beef chuck", "kg");

        list.SetSourceMeals(new[] { _orzo.Id });
        var after = await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);

        after.Value.Items.Select(i => (i.Name, i.Quantity, i.Unit, i.Checked)).Should().Equal(
            ("garlic", 2m, "clove", true),
            ("orzo", 200m, "g", false),
            ("salt", (decimal?)null, (string?)null, false));
        after.Value.Items.Should().NotContain(i => i.Name == "beef chuck");
        after.Value.SourceMealIds.Should().Equal(_orzo.Id.Value);
    }

    [Fact]
    public async Task MealDeletedOutright_FallsOffTheMenuOnGenerate()
    {
        var gone = new MealId(Guid.NewGuid());
        var list = ShoppingList.Create(Joe, Monday, new[] { gone, _orzo.Id });
        _lists.GetForWeekAsync(Joe, Monday, Arg.Any<CancellationToken>()).Returns(list);

        var result = await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);

        result.Value.SourceMealIds.Should().Equal(_orzo.Id.Value);
        result.Value.Items.Select(i => i.Name).Should().Equal("garlic", "orzo", "salt");
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await Handler().Handle(new GenerateShoppingListCommand(Monday), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
        await _lists.DidNotReceive().GetForWeekAsync(Arg.Any<UserId>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
