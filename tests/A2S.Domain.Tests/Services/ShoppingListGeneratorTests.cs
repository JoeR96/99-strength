using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

/// <summary>Review focus 1 and 2 of the Phase 10 plan: summing garlic across meals, never converting units.</summary>
public class ShoppingListGeneratorTests
{
    private static readonly UserId Joe = new("user_joe");

    private static Meal MealWith(string title, params Ingredient[] ingredients) =>
        Meal.Create(Joe, title, MealType.Dinner, null, ingredients, null, null, true);

    [Fact]
    public void TwoMealsNeedingGarlic_ProduceOneLineOfSixCloves()
    {
        var orzo = MealWith("Orzo", new Ingredient("garlic", 2, "clove"), new Ingredient("orzo", 200, "g"));
        var barbacoa = MealWith("Barbacoa", new Ingredient("Garlic", 4, "clove"), new Ingredient("beef chuck", 1.2m, "kg"));

        var items = ShoppingListGenerator.Build(new[] { orzo, barbacoa });

        var garlic = items.Single(i => i.Name == "garlic");
        garlic.Quantity.Should().Be(6);
        garlic.Unit.Should().Be("clove");
        garlic.Checked.Should().BeFalse();
        garlic.FromMeals.Should().Equal(orzo.Id, barbacoa.Id);
        items.Select(i => i.Name).Should().Equal("beef chuck", "garlic", "orzo");
    }

    [Fact]
    public void SameIngredientInDifferentUnits_StaysSeparateLines()
    {
        var orzo = MealWith("Orzo", new Ingredient("baby spinach", 1, "handful"));
        var curry = MealWith("Curry", new Ingredient("baby spinach", 200, "g"));

        var items = ShoppingListGenerator.Build(new[] { orzo, curry });

        items.Select(i => (i.Name, i.Quantity, i.Unit)).Should().Equal(
            ("baby spinach", 200m, "g"),
            ("baby spinach", 1m, "handful"));
    }

    [Fact]
    public void UnquantifiedIngredients_AreDeduplicated_NeverSummed()
    {
        var orzo = MealWith("Orzo", Ingredient.Unquantified("salt"), Ingredient.Unquantified("fresh parsley"));
        var wrap = MealWith("Wrap", Ingredient.Unquantified("Salt"), new Ingredient("salt", 1, "tsp"));

        var items = ShoppingListGenerator.Build(new[] { orzo, wrap });

        items.Select(i => (i.Name, i.Quantity, i.Unit)).Should().Equal(
            ("fresh parsley", (decimal?)null, (string?)null),
            ("salt", (decimal?)null, (string?)null),
            ("salt", 1m, "tsp"));
        items.Single(i => i.Name == "salt" && i.Unit is null).FromMeals.Should().Equal(orzo.Id, wrap.Id);
    }

    [Fact]
    public void Plurals_AreMergedWithSingulars_WhenKnown()
    {
        var bagel = MealWith("Bagel", new Ingredient("eggs", 2, "piece"));
        var rice = MealWith("Rice", new Ingredient("egg", 4, "piece"));

        var items = ShoppingListGenerator.Build(new[] { bagel, rice });

        items.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "egg", Quantity = 6m, Unit = "piece" });
    }

    [Fact]
    public void SameIngredientTwiceInOneMeal_SumsButListsTheMealOnce()
    {
        var nandos = MealWith("Nandos", new Ingredient("olive oil", 1, "tbsp"), new Ingredient("olive oil", 1, "tbsp"));

        var items = ShoppingListGenerator.Build(new[] { nandos });

        items.Single().Quantity.Should().Be(2);
        items.Single().FromMeals.Should().Equal(nandos.Id);
    }

    [Fact]
    public void NoMeals_IsAnEmptyList()
    {
        ShoppingListGenerator.Build(Array.Empty<Meal>()).Should().BeEmpty();
    }
}
