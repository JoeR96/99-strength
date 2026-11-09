using A2S.Application.Commands.Meals;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Meals;

public class MealCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");
    private static readonly IReadOnlyList<IngredientDto> OrzoIngredients = new[]
    {
        new IngredientDto("orzo", 200, "g"),
        new IngredientDto("garlic", 2, "clove"),
        new IngredientDto("salt", null, null),
    };

    private readonly IMealRepository _meals = Substitute.For<IMealRepository>();
    private readonly IShoppingListRepository _lists = Substitute.For<IShoppingListRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    public MealCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
    }

    private static Meal Orzo(UserId user) =>
        Meal.Create(user, "Orzo", MealType.Dinner, "x", OrzoIngredients.Select(i => i.ToDomain()), new[] { "Cook." }, new[] { "pasta" }, true, "cooks:orzo");

    [Fact]
    public async Task Create_PersistsAndReturnsDto()
    {
        var handler = new CreateMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var result = await handler.Handle(
            new CreateMealCommand("Orzo", MealType.Dinner, "Light.", OrzoIngredients, new[] { "Cook." }, new[] { "Pasta" }, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Orzo");
        result.Value.Type.Should().Be(MealType.Dinner);
        result.Value.Ingredients.Should().Equal(OrzoIngredients);
        result.Value.Tags.Should().Equal("pasta");
        result.Value.ExternalKey.Should().BeNull();
        await _meals.Received(1).AddAsync(Arg.Is<Meal>(m => m.UserId == Joe), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);
        var handler = new CreateMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var result = await handler.Handle(new CreateMealCommand("Orzo", MealType.Dinner, null, OrzoIngredients, null, null, true), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
        await _meals.DidNotReceive().AddAsync(Arg.Any<Meal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UnknownUnit_IsADomainRuleViolation()
    {
        var handler = new CreateMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var result = await handler.Handle(
            new CreateMealCommand("Orzo", MealType.Dinner, null, new[] { new IngredientDto("flour", 1, "cup") }, null, null, true),
            CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        result.Error.Should().Contain("cup");
    }

    [Fact]
    public async Task Update_ReplacesContent_KeepsKey()
    {
        var orzo = Orzo(Joe);
        _meals.GetByIdAsync(orzo.Id, Arg.Any<CancellationToken>()).Returns(orzo);
        var handler = new UpdateMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var result = await handler.Handle(
            new UpdateMealCommand(orzo.Id.Value, "Orzo v2", MealType.Lunch, null, new[] { new IngredientDto("orzo", 150, "g") }, null, null, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new { Title = "Orzo v2", Type = MealType.Lunch, Description = "", CutFriendly = false, ExternalKey = "cooks:orzo" });
        result.Value.Ingredients.Should().Equal(new IngredientDto("orzo", 150, "g"));
        _meals.Received(1).Update(orzo);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_SomeoneElsesMeal_IsNotFound()
    {
        var theirs = Orzo(Other);
        _meals.GetByIdAsync(theirs.Id, Arg.Any<CancellationToken>()).Returns(theirs);
        var handler = new UpdateMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var result = await handler.Handle(
            new UpdateMealCommand(theirs.Id.Value, "Mine now", MealType.Dinner, null, OrzoIngredients, null, null, true),
            CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        theirs.Title.Should().Be("Orzo");
    }

    [Fact]
    public async Task Delete_RemovesOwnMeal_NotFoundForMissing()
    {
        var orzo = Orzo(Joe);
        _meals.GetByIdAsync(orzo.Id, Arg.Any<CancellationToken>()).Returns(orzo);
        var handler = new DeleteMealCommandHandler(_meals, _unitOfWork, _currentUser);

        var deleted = await handler.Handle(new DeleteMealCommand(orzo.Id.Value), CancellationToken.None);
        var missing = await handler.Handle(new DeleteMealCommand(Guid.NewGuid()), CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        _meals.Received(1).Remove(orzo);
        missing.ErrorCode.Should().Be(ErrorCode.NotFound);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetWeekMeals_OnAWeekWithNoList_CreatesTheList_SnappedToMonday()
    {
        var orzo = Orzo(Joe);
        _meals.GetManyAsync(Joe, Arg.Any<IEnumerable<MealId>>(), Arg.Any<CancellationToken>()).Returns(new[] { orzo });
        _lists.GetForWeekAsync(Joe, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((ShoppingList?)null);
        var handler = new SetWeekMealsCommandHandler(_meals, _lists, _unitOfWork, _currentUser);

        var result = await handler.Handle(new SetWeekMealsCommand(new DateOnly(2026, 11, 11), new[] { orzo.Id.Value, orzo.Id.Value }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WeekStart.Should().Be(new DateOnly(2026, 11, 9));
        result.Value.MealIds.Should().Equal(orzo.Id.Value);
        await _lists.Received(1).AddAsync(
            Arg.Is<ShoppingList>(l => l.UserId == Joe && l.WeekStart == new DateOnly(2026, 11, 9) && l.SourceMealIds.SequenceEqual(new[] { orzo.Id }) && l.Items.Count == 0),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetWeekMeals_OnAnExistingList_UpdatesTheMenu_LeavesItemsAlone()
    {
        var orzo = Orzo(Joe);
        var list = ShoppingList.Create(Joe, new DateOnly(2026, 11, 9), new[] { orzo.Id }, new[] { new ShoppingListItem("orzo", 200, "g", true, new[] { orzo.Id }) });
        _meals.GetManyAsync(Joe, Arg.Any<IEnumerable<MealId>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Meal>());
        _lists.GetForWeekAsync(Joe, new DateOnly(2026, 11, 9), Arg.Any<CancellationToken>()).Returns(list);
        var handler = new SetWeekMealsCommandHandler(_meals, _lists, _unitOfWork, _currentUser);

        var result = await handler.Handle(new SetWeekMealsCommand(new DateOnly(2026, 11, 9), Array.Empty<Guid>()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MealIds.Should().BeEmpty();
        list.SourceMealIds.Should().BeEmpty();
        list.Items.Should().ContainSingle().Which.Checked.Should().BeTrue();
        _lists.Received(1).Update(list);
        await _lists.DidNotReceive().AddAsync(Arg.Any<ShoppingList>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetWeekMeals_WithSomeoneElsesMeal_IsNotFound()
    {
        var theirs = Orzo(Other);
        _meals.GetManyAsync(Joe, Arg.Any<IEnumerable<MealId>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Meal>());
        var handler = new SetWeekMealsCommandHandler(_meals, _lists, _unitOfWork, _currentUser);

        var result = await handler.Handle(new SetWeekMealsCommand(new DateOnly(2026, 11, 9), new[] { theirs.Id.Value }), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_FlipsTheLine_AndReturnsTheList()
    {
        var orzo = Orzo(Joe);
        var list = ShoppingList.Create(Joe, new DateOnly(2026, 11, 9), new[] { orzo.Id }, new[] { new ShoppingListItem("garlic", 2, "clove", false, new[] { orzo.Id }) });
        _lists.GetByIdAsync(list.Id, Arg.Any<CancellationToken>()).Returns(list);
        var handler = new ToggleShoppingItemCommandHandler(_lists, _unitOfWork, _currentUser);

        var result = await handler.Handle(new ToggleShoppingItemCommand(list.Id.Value, "Garlic", "clove"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Single().Checked.Should().BeTrue();
        result.Value.CheckedCount.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        _lists.Received(1).Update(list);
    }

    [Fact]
    public async Task Toggle_UnknownLine_OrSomeoneElsesList_IsNotFound()
    {
        var mine = ShoppingList.Create(Joe, new DateOnly(2026, 11, 9), Array.Empty<MealId>(), new[] { new ShoppingListItem("garlic", 2, "clove", false, Array.Empty<MealId>()) });
        var theirs = ShoppingList.Create(Other, new DateOnly(2026, 11, 9), Array.Empty<MealId>(), new[] { new ShoppingListItem("garlic", 2, "clove", false, Array.Empty<MealId>()) });
        _lists.GetByIdAsync(mine.Id, Arg.Any<CancellationToken>()).Returns(mine);
        _lists.GetByIdAsync(theirs.Id, Arg.Any<CancellationToken>()).Returns(theirs);
        var handler = new ToggleShoppingItemCommandHandler(_lists, _unitOfWork, _currentUser);

        var unknown = await handler.Handle(new ToggleShoppingItemCommand(mine.Id.Value, "garlic", "g"), CancellationToken.None);
        var notMine = await handler.Handle(new ToggleShoppingItemCommand(theirs.Id.Value, "garlic", "clove"), CancellationToken.None);

        unknown.ErrorCode.Should().Be(ErrorCode.NotFound);
        notMine.ErrorCode.Should().Be(ErrorCode.NotFound);
        theirs.Items.Single().Checked.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
