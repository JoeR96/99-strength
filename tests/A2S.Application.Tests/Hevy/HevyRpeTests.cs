using System.Globalization;
using A2S.Application.Interfaces.Hevy;
using FluentAssertions;

namespace A2S.Application.Tests.Hevy;

public class HevyRpeTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("4", null)]
    [InlineData("5.5", null)]
    [InlineData("6", "6")]
    [InlineData("6.5", "7")]
    [InlineData("7", "7")]
    [InlineData("7.3", "7.5")]
    [InlineData("9", "9")]
    [InlineData("10", "10")]
    [InlineData("11", "10")]
    public void ForHevy_OnlyReturnsValuesHevyAccepts(string? input, string? expected)
    {
        HevyRpe.ForHevy(Parse(input)).Should().Be(Parse(expected));
    }

    private static decimal? Parse(string? value) => value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
}
