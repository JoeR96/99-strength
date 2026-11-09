using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Meals;

public class ShoppingListTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly MealId Orzo = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly MealId Barbacoa = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static ShoppingListItem Item(string name, decimal? quantity, string? unit, bool @checked = false, params MealId[] from) =>
        new(name, quantity, unit, @checked, from.Length == 0 ? new[] { Orzo } : from);

    [Fact]
    public void Create_OnAMonday_KeepsMealsAndSortsItems()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { Orzo, Barbacoa, Orzo }, new[] { Item("orzo", 200, "g"), Item("garlic", 2, "clove") });

        list.UserId.Should().Be(Joe);
        list.WeekStart.Should().Be(Monday);
        list.SourceMealIds.Should().Equal(Orzo, Barbacoa);
        list.Items.Select(i => i.Name).Should().Equal("garlic", "orzo");
        list.CheckedCount.Should().Be(0);
    }

    [Fact]
    public void Create_NotAMonday_Throws()
    {
        var act = () => ShoppingList.Create(Joe, Monday.AddDays(1), Array.Empty<MealId>());

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*Monday*");
    }

    [Theory]
    [InlineData(2026, 11, 9, 2026, 11, 9)]
    [InlineData(2026, 11, 11, 2026, 11, 9)]
    [InlineData(2026, 11, 15, 2026, 11, 9)]
    [InlineData(2026, 11, 16, 2026, 11, 16)]
    public void WeekStartOf_IsTheMonday(int y, int m, int d, int ey, int em, int ed)
    {
        ShoppingList.WeekStartOf(new DateOnly(y, m, d)).Should().Be(new DateOnly(ey, em, ed));
    }

    [Fact]
    public void Create_DuplicateKeys_Throws()
    {
        var act = () => ShoppingList.Create(Joe, Monday, Array.Empty<MealId>(), new[] { Item("garlic", 2, "clove"), Item("Garlic", 4, "clove") });

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*distinct*");
    }

    [Fact]
    public void Toggle_FlipsOneLine_MatchingOnNormalisedNameAndUnit()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { Orzo }, new[] { Item("garlic", 6, "clove"), Item("salt", null, null) });

        var first = list.Toggle(" Garlic ", "CLOVE");
        var second = list.Toggle("salt", null);
        var missing = list.Toggle("garlic", "g");

        first.Should().BeTrue();
        second.Should().BeTrue();
        missing.Should().BeFalse();
        list.Items.Should().OnlyContain(i => i.Checked);
        list.CheckedCount.Should().Be(2);

        list.Toggle("garlic", "clove");
        list.Items.Single(i => i.Name == "garlic").Checked.Should().BeFalse();
    }

    [Fact]
    public void Regenerate_KeepsTicksForMatchingKeys_AndDropsTheRest()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { Orzo, Barbacoa }, new[]
        {
            Item("garlic", 6, "clove", from: new[] { Orzo, Barbacoa }),
            Item("orzo", 200, "g"),
            Item("beef chuck", 1.2m, "kg", from: Barbacoa),
        });
        list.Toggle("garlic", "clove");
        list.Toggle("beef chuck", "kg");

        list.SetSourceMeals(new[] { Orzo });
        list.Regenerate(new[] { Item("garlic", 2, "clove"), Item("orzo", 200, "g"), Item("spinach", 1, "handful") });

        list.SourceMealIds.Should().Equal(Orzo);
        list.Items.Select(i => (i.Name, i.Quantity, i.Checked)).Should().Equal(
            ("garlic", 2m, true),
            ("orzo", 200m, false),
            ("spinach", 1m, false));
    }

    [Fact]
    public void Regenerate_SameNameDifferentUnit_DoesNotInheritTheTick()
    {
        var list = ShoppingList.Create(Joe, Monday, new[] { Orzo }, new[] { Item("spinach", 1, "handful") });
        list.Toggle("spinach", "handful");

        list.Regenerate(new[] { Item("spinach", 100, "g") });

        list.Items.Single().Checked.Should().BeFalse();
    }

    [Fact]
    public void Item_NormalisesNameAndUnit_AndDeduplicatesMeals()
    {
        var item = new ShoppingListItem(" Eggs ", 2, " Piece ", false, new[] { Orzo, Orzo, Barbacoa });

        item.Name.Should().Be("egg");
        item.Unit.Should().Be("piece");
        item.FromMeals.Should().Equal(Orzo, Barbacoa);
        item.Key.Should().Be(new ShoppingItemKey("egg", "piece"));
    }

    [Fact]
    public void Item_WithChecked_IsValueEqualToAnEquivalentItem()
    {
        var a = Item("egg", 2, "piece", from: new[] { Orzo });
        var b = Item("egg", 2, "piece", from: new[] { Orzo });

        a.Should().Be(b);
        a.WithChecked(true).Should().Be(Item("egg", 2, "piece", true, Orzo));
        a.WithChecked(false).Should().BeSameAs(a);
    }

    [Fact]
    public void Item_QuantityWithoutUnit_Throws()
    {
        var act = () => new ShoppingListItem("salt", 1, null, false, Array.Empty<MealId>());

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
