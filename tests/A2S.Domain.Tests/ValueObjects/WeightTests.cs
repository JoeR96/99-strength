using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class WeightTests
{
    [Fact]
    public void Kilograms_WhenCreatedWithValidValue_ShouldStoreCorrectly()
    {
        var weight = Weight.Kilograms(100m);

        weight.Value.Should().Be(100m);
        weight.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Pounds_WhenCreatedWithValidValue_ShouldStoreCorrectly()
    {
        var weight = Weight.Pounds(225m);

        weight.Value.Should().Be(225m);
        weight.Unit.Should().Be(WeightUnit.Pounds);
    }

    [Fact]
    public void Create_WhenCalledWithValidValues_ShouldStoreCorrectly()
    {
        var weight = Weight.Create(80m, WeightUnit.Kilograms);

        weight.Value.Should().Be(80m);
        weight.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Create_WhenCalledWithZero_ShouldSucceed()
    {
        var weight = Weight.Kilograms(0m);

        weight.Value.Should().Be(0m);
    }

    [Fact]
    public void Create_WhenCalledWithNegativeValue_ShouldThrow()
    {
        Action act = () => Weight.Kilograms(-5m);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Weight cannot be negative*");
    }

    [Fact]
    public void Add_WhenSameUnit_ShouldReturnSum()
    {
        var w1 = Weight.Kilograms(60m);
        var w2 = Weight.Kilograms(40m);

        var result = w1.Add(w2);

        result.Value.Should().Be(100m);
        result.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Add_WhenDifferentUnits_ShouldThrow()
    {
        var w1 = Weight.Kilograms(60m);
        var w2 = Weight.Pounds(100m);

        Action act = () => w1.Add(w2);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Cannot add weights with different units*");
    }

    [Fact]
    public void Subtract_WhenSameUnit_ShouldReturnDifference()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(40m);

        var result = w1.Subtract(w2);

        result.Value.Should().Be(60m);
        result.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Subtract_WhenDifferentUnits_ShouldThrow()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Pounds(50m);

        Action act = () => w1.Subtract(w2);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Cannot subtract weights with different units*");
    }

    [Fact]
    public void Subtract_WhenResultWouldBeNegative_ShouldThrow()
    {
        var w1 = Weight.Kilograms(40m);
        var w2 = Weight.Kilograms(60m);

        Action act = () => w1.Subtract(w2);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Resulting weight cannot be negative*");
    }

    [Theory]
    [InlineData(72.1, 2.5, 72.5)]
    [InlineData(73.0, 2.5, 72.5)]
    [InlineData(73.8, 2.5, 75.0)]
    [InlineData(100.0, 5.0, 100.0)]
    [InlineData(102.0, 5.0, 100.0)]
    [InlineData(103.0, 5.0, 105.0)]
    public void RoundToIncrement_ShouldRoundCorrectly(
        decimal value, decimal increment, decimal expected)
    {
        var weight = Weight.Kilograms(value);

        var result = weight.RoundToIncrement(increment);

        result.Value.Should().Be(expected);
        result.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void ConvertTo_WhenSameUnit_ShouldReturnSameInstance()
    {
        var weight = Weight.Kilograms(100m);

        var result = weight.ConvertTo(WeightUnit.Kilograms);

        result.Should().BeSameAs(weight);
    }

    [Fact]
    public void ConvertTo_KilogramsToPounds_ShouldConvertCorrectly()
    {
        var weight = Weight.Kilograms(100m);

        var result = weight.ConvertTo(WeightUnit.Pounds);

        result.Value.Should().BeApproximately(220.462m, 0.001m);
        result.Unit.Should().Be(WeightUnit.Pounds);
    }

    [Fact]
    public void ConvertTo_PoundsToKilograms_ShouldConvertCorrectly()
    {
        var weight = Weight.Pounds(220.462m);

        var result = weight.ConvertTo(WeightUnit.Kilograms);

        result.Value.Should().BeApproximately(100m, 0.01m);
        result.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Equals_WhenSameValueAndUnit_ShouldBeEqual()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        w1.Should().Be(w2);
        (w1 == w2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentValue_ShouldNotBeEqual()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(105m);

        w1.Should().NotBe(w2);
        (w1 != w2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentUnit_ShouldNotBeEqual()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Pounds(100m);

        w1.Should().NotBe(w2);
    }

    [Fact]
    public void GetHashCode_WhenEqual_ShouldBeSame()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        w1.GetHashCode().Should().Be(w2.GetHashCode());
    }
}
