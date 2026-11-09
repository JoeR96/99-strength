using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Meals;

public class MealTests
{
    private static readonly UserId Joe = new("user_joe");

    private static Ingredient Orzo => new("orzo", 200, "g");

    [Fact]
    public void Ingredient_QuantifiedKeepsQuantityAndNormalisedUnit()
    {
        var garlic = new Ingredient(" garlic ", 2, " Clove ");

        garlic.Name.Should().Be("garlic");
        garlic.Quantity.Should().Be(2);
        garlic.Unit.Should().Be("clove");
        garlic.IsQuantified.Should().BeTrue();
    }

    [Fact]
    public void Ingredient_UnquantifiedHasNeitherQuantityNorUnit()
    {
        var parsley = Ingredient.Unquantified("fresh parsley");

        parsley.Quantity.Should().BeNull();
        parsley.Unit.Should().BeNull();
        parsley.IsQuantified.Should().BeFalse();
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(null, "g")]
    public void Ingredient_QuantityWithoutUnitOrUnitWithoutQuantity_Throws(int? quantity, string? unit)
    {
        var act = () => new Ingredient("salt", quantity, unit);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData("cup")]
    [InlineData("oz")]
    public void Ingredient_UnknownUnit_Throws(string unit)
    {
        var act = () => new Ingredient("flour", 1, unit);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*Unknown ingredient unit*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ingredient_NonPositiveQuantity_Throws(int quantity)
    {
        var act = () => new Ingredient("flour", quantity, "g");

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Ingredient_BlankName_Throws()
    {
        var act = () => new Ingredient(" ", null, null);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_TrimsAndKeepsEverything()
    {
        var meal = Meal.Create(
            Joe,
            "  Creamy Lemon Orzo ",
            MealType.Dinner,
            " Light and lemony. ",
            new[] { Orzo, Ingredient.Unquantified("salt") },
            new[] { " Sear the chicken. ", "", "Toast the orzo." },
            new[] { "Chicken", "pasta", " chicken " },
            cutFriendly: true,
            externalKey: " cooks:creamy-lemon-orzo ");

        meal.UserId.Should().Be(Joe);
        meal.Title.Should().Be("Creamy Lemon Orzo");
        meal.Type.Should().Be(MealType.Dinner);
        meal.Description.Should().Be("Light and lemony.");
        meal.Ingredients.Should().Equal(Orzo, Ingredient.Unquantified("salt"));
        meal.Steps.Should().Equal("Sear the chicken.", "Toast the orzo.");
        meal.Tags.Should().Equal("chicken", "pasta");
        meal.CutFriendly.Should().BeTrue();
        meal.ExternalKey.Should().Be("cooks:creamy-lemon-orzo");
        meal.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_NullDescriptionAndLists_BecomeEmpty()
    {
        var meal = Meal.Create(Joe, "Toast", MealType.Breakfast, null, new[] { Orzo }, null, null, false);

        meal.Description.Should().BeEmpty();
        meal.Steps.Should().BeEmpty();
        meal.Tags.Should().BeEmpty();
        meal.ExternalKey.Should().BeNull();
    }

    [Fact]
    public void Create_BlankTitle_Throws()
    {
        var act = () => Meal.Create(Joe, " ", MealType.Dinner, null, new[] { Orzo }, null, null, true);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*title*");
    }

    [Fact]
    public void Create_NoIngredients_Throws()
    {
        var act = () => Meal.Create(Joe, "Air", MealType.Snack, null, Array.Empty<Ingredient>(), null, null, true);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*at least one ingredient*");
    }

    [Fact]
    public void Update_ReplacesEverythingButOwnerAndKey()
    {
        var meal = Meal.Create(Joe, "Orzo", MealType.Dinner, "x", new[] { Orzo }, new[] { "a" }, new[] { "pasta" }, true, "cooks:orzo");
        var eggs = new Ingredient("eggs", 2, "piece");

        meal.Update("Eggs on toast", MealType.Breakfast, null, new[] { eggs }, new[] { "Fry." }, new[] { "eggs" }, false);

        meal.Title.Should().Be("Eggs on toast");
        meal.Type.Should().Be(MealType.Breakfast);
        meal.Description.Should().BeEmpty();
        meal.Ingredients.Should().Equal(eggs);
        meal.Steps.Should().Equal("Fry.");
        meal.Tags.Should().Equal("eggs");
        meal.CutFriendly.Should().BeFalse();
        meal.UserId.Should().Be(Joe);
        meal.ExternalKey.Should().Be("cooks:orzo");
    }

    [Fact]
    public void Update_Invalid_LeavesTheMealUntouched()
    {
        var meal = Meal.Create(Joe, "Orzo", MealType.Dinner, "x", new[] { Orzo }, new[] { "a" }, new[] { "pasta" }, true);

        var act = () => meal.Update("", MealType.Breakfast, null, Array.Empty<Ingredient>(), null, null, false);

        act.Should().Throw<BusinessRuleViolationException>();
        meal.Title.Should().Be("Orzo");
        meal.Ingredients.Should().Equal(Orzo);
        meal.CutFriendly.Should().BeTrue();
    }
}
