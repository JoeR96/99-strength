using A2S.Domain.Aggregates.Meals;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Meals;

public class IngredientNameTests
{
    [Theory]
    [InlineData("Garlic", "garlic")]
    [InlineData("  baby   spinach ", "baby spinach")]
    [InlineData("eggs", "egg")]
    [InlineData("Eggs", "egg")]
    [InlineData("oats", "oats")]
    [InlineData("rolled oats", "rolled oats")]
    [InlineData("beansprouts", "beansprouts")]
    [InlineData("chicken thighs", "chicken thigh")]
    [InlineData("Spring Onions", "spring onion")]
    [InlineData("tomatoes", "tomatoes")]
    [InlineData("s", "s")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalise_TrimsCollapsesLowercases_AndSingularisesOnlyKnownWords(string input, string expected)
    {
        IngredientName.Normalise(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("g", "g")]
    [InlineData(" TBSP ", "tbsp")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Units_Normalise(string? input, string? expected)
    {
        IngredientUnits.Normalise(input).Should().Be(expected);
    }

    [Fact]
    public void Units_ClosedSetHasElevenEntries()
    {
        IngredientUnits.All.Should().BeEquivalentTo(new[] { "g", "kg", "ml", "l", "tbsp", "tsp", "piece", "clove", "slice", "handful", "pinch" });
    }
}
