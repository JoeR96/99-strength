using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class TrainingMaxTests
{
    [Fact]
    public void Create_WithValidValues_ShouldSucceed()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);

        tm.Value.Should().Be(100m);
        tm.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Create_WithZeroWeight_ShouldThrowException()
    {
        Action act = () => TrainingMax.Create(0m, WeightUnit.Kilograms);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Training Max must be greater than zero*");
    }

    [Fact]
    public void Create_WithNegativeWeight_ShouldThrowException()
    {
        Action act = () => TrainingMax.Create(-50m, WeightUnit.Kilograms);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Training Max must be greater than zero*");
    }

    [Theory]
    [InlineData(0.70, 100, 70)]
    [InlineData(0.85, 100, 85)]
    [InlineData(1.0, 100, 100)]
    public void CalculateWorkingWeight_WithValidIntensity_ShouldReturnCorrectWeight(
        decimal intensity, decimal tmValue, decimal expected)
    {
        var tm = TrainingMax.Create(tmValue, WeightUnit.Kilograms);

        var result = tm.CalculateWorkingWeight(intensity);

        result.Value.Should().Be(expected);
    }

    [Fact]
    public void CalculateWorkingWeight_ShouldRoundToNearestIncrement()
    {
        var tm = TrainingMax.Create(103m, WeightUnit.Kilograms);

        // 70% of 103 = 72.1, should round to 72.5
        var result = tm.CalculateWorkingWeight(0.70m);

        result.Value.Should().Be(72.5m);
    }

    [Fact]
    public void ApplyAdjustment_WithPercentageIncrease_ShouldIncreaseTrainingMax()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(0.05m); // 5% increase

        var result = tm.ApplyAdjustment(adjustment);

        result.Value.Should().Be(105m);
    }

    [Fact]
    public void ApplyAdjustment_WithPercentageDecrease_ShouldDecreaseTrainingMax()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(-0.05m); // 5% decrease

        var result = tm.ApplyAdjustment(adjustment);

        result.Value.Should().Be(95m);
    }

    [Fact]
    public void ApplyAdjustment_WithAbsoluteIncrease_ShouldAddAmount()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Absolute(5m);

        var result = tm.ApplyAdjustment(adjustment);

        result.Value.Should().Be(105m);
    }

    [Fact]
    public void ApplyAdjustment_ShouldRoundToTwoDecimalPlaces()
    {
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(0.012m); // 1.2% = 101.2

        var result = tm.ApplyAdjustment(adjustment);

        result.Value.Should().Be(101.20m);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldBeEqual()
    {
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(100m, WeightUnit.Kilograms);

        tm1.Should().Be(tm2);
        (tm1 == tm2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldNotBeEqual()
    {
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(105m, WeightUnit.Kilograms);

        tm1.Should().NotBe(tm2);
        (tm1 != tm2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentUnits_ShouldNotBeEqual()
    {
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(100m, WeightUnit.Pounds);

        tm1.Should().NotBe(tm2);
    }
}
