using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Domain.Services;
using A2S.Infrastructure.SeedData;
using FluentAssertions;

namespace A2S.Infrastructure.Tests.SeedData;

/// <summary>
/// The hand-parsed meals-seed.json itself, checked without a database: every entry builds a valid
/// Meal, every ingredient has a non-empty normalised name and every unit is in the closed set.
/// </summary>
public class MealSeedFileTests
{
    private static readonly UserId Joe = new("user_joe");

    [Fact]
    public async Task Seed_HasTheTenCooksAssistantRecipes_AllCutFriendlyWithKeysAndTags()
    {
        var entries = await MealSeeder.LoadSeedAsync();

        entries.Should().HaveCount(10);
        entries.Select(e => e.Key).Should().OnlyHaveUniqueItems().And.OnlyContain(k => k.StartsWith("cooks:"));
        entries.Should().OnlyContain(e => e.Tags.Count > 0 && e.Steps.Count > 0 && e.Ingredients.Count > 0);
        entries.Select(e => e.Title).Should().Contain("Creamy Lemon Orzo with Skinless Chicken Thighs")
            .And.Contain("Berry and Almond Overnight Oats");

        var meals = entries.Select(e => MealSeeder.ToMeal(Joe, e)).ToList();

        meals.Should().OnlyContain(m => m.CutFriendly && m.UserId == Joe && m.ExternalKey != null);
        meals.Count(m => m.Type == MealType.Breakfast).Should().Be(5);
        meals.Count(m => m.Type == MealType.Lunch).Should().Be(1);
        meals.Count(m => m.Type == MealType.Dinner).Should().Be(4);
    }

    [Fact]
    public async Task Seed_EveryIngredient_HasANormalisedName_AndAUnitFromTheClosedSet()
    {
        var entries = await MealSeeder.LoadSeedAsync();

        var ingredients = entries.SelectMany(e => e.Ingredients).ToList();

        ingredients.Should().OnlyContain(i => IngredientName.Normalise(i.Name).Length > 0);
        ingredients.Where(i => i.Unit is not null).Should().OnlyContain(i => IngredientUnits.IsKnown(i.Unit) && i.Quantity > 0);
        ingredients.Where(i => i.Unit is null).Should().OnlyContain(i => i.Quantity == null, "free-text ingredients carry neither quantity nor unit");
    }

    [Fact]
    public async Task Seed_OrzoAndBarbacoa_SumToSixClovesOfGarlic()
    {
        var entries = await MealSeeder.LoadSeedAsync();
        var orzo = MealSeeder.ToMeal(Joe, entries.Single(e => e.Key.Contains("orzo")));
        var barbacoa = MealSeeder.ToMeal(Joe, entries.Single(e => e.Key.Contains("barbacoa")));

        var items = ShoppingListGenerator.Build(new[] { orzo, barbacoa });

        var garlic = items.Single(i => i.Name == "garlic");
        garlic.Quantity.Should().Be(6);
        garlic.Unit.Should().Be("clove");
        items.Single(i => i.Name == "olive oil").Quantity.Should().Be(4);
        items.Where(i => i.Name == "salt").Select(i => i.Unit).Should().BeEquivalentTo(new[] { null, "tsp" });
    }
}
