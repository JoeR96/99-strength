using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using FluentAssertions;

namespace A2S.Infrastructure.Tests.Persistence;

/// <summary>
/// The jsonb converters behind the Meals and ShoppingLists columns, checked without a database:
/// the value objects have constructor parameters, so a round trip must rebuild them exactly.
/// </summary>
public class MealJsonColumnTests
{
    private static readonly MealId Orzo = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly MealId Barbacoa = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static List<T> RoundTrip<T>(List<T> list)
    {
        var converter = JsonColumn.Converter<T>();
        var json = (string)converter.ConvertToProvider(list)!;
        return (List<T>)converter.ConvertFromProvider(json)!;
    }

    [Fact]
    public void Ingredients_RoundTrip_QuantifiedAndNot()
    {
        var list = new List<Ingredient> { new("garlic", 2, "clove"), Ingredient.Unquantified("salt"), new("beef chuck", 1.2m, "kg") };

        var back = RoundTrip(list);

        back.Should().Equal(list);
        JsonColumn.Comparer<Ingredient>().Equals(list, back).Should().BeTrue();
    }

    [Fact]
    public void ShoppingListItems_RoundTrip_WithTicksAndSourceMeals()
    {
        var list = new List<ShoppingListItem>
        {
            new("garlic", 6, "clove", true, new[] { Orzo, Barbacoa }),
            new("salt", null, null, false, new[] { Orzo }),
        };

        var back = RoundTrip(list);

        back.Should().Equal(list);
        back[0].FromMeals.Should().Equal(Orzo, Barbacoa);
        back[0].Checked.Should().BeTrue();
        back[1].Quantity.Should().BeNull();
        back[1].Unit.Should().BeNull();
    }

    [Fact]
    public void MealIds_AndStrings_RoundTrip()
    {
        RoundTrip(new List<MealId> { Orzo, Barbacoa }).Should().Equal(Orzo, Barbacoa);
        RoundTrip(new List<string> { "Sear.", "Simmer." }).Should().Equal("Sear.", "Simmer.");
        RoundTrip(new List<string>()).Should().BeEmpty();
    }
}
