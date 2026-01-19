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
        // Arrange & Act
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);

        // Assert
        tm.Value.Should().Be(100m);
        tm.Unit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void Create_WithZeroWeight_ShouldThrowException()
    {
        // Act
        Action act = () => TrainingMax.Create(0m, WeightUnit.Kilograms);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Training Max must be greater than zero*");
    }

    [Fact]
    public void Create_WithNegativeWeight_ShouldThrowException()
    {
        // Act
        Action act = () => TrainingMax.Create(-50m, WeightUnit.Kilograms);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Training Max must be greater than zero*");
    }

    [Theory]
    [InlineData(0.70, 100, 70)]
    [InlineData(0.85, 100, 85)]
    [InlineData(1.0, 100, 100)]
    public void CalculateWorkingWeight_WithValidIntensity_ShouldReturnCorrectWeight(
        decimal intensity, decimal tmValue, decimal expected)
    {
        // Arrange
        var tm = TrainingMax.Create(tmValue, WeightUnit.Kilograms);

        // Act
        var result = tm.CalculateWorkingWeight(intensity);

        // Assert
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void CalculateWorkingWeight_ShouldRoundToNearestIncrement()
    {
        // Arrange
        var tm = TrainingMax.Create(103m, WeightUnit.Kilograms);

        // Act - 70% of 103 = 72.1, should round to 72.5
        var result = tm.CalculateWorkingWeight(0.70m);

        // Assert
        result.Value.Should().Be(72.5m);
    }

    [Fact]
    public void ApplyAdjustment_WithPercentageIncrease_ShouldIncreaseTrainingMax()
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(0.05m); // 5% increase

        // Act
        var result = tm.ApplyAdjustment(adjustment);

        // Assert
        result.Value.Should().Be(105m);
    }

    [Fact]
    public void ApplyAdjustment_WithPercentageDecrease_ShouldDecreaseTrainingMax()
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(-0.05m); // 5% decrease

        // Act
        var result = tm.ApplyAdjustment(adjustment);

        // Assert
        result.Value.Should().Be(95m);
    }

    [Fact]
    public void ApplyAdjustment_WithAbsoluteIncrease_ShouldAddAmount()
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Absolute(5m);

        // Act
        var result = tm.ApplyAdjustment(adjustment);

        // Assert
        result.Value.Should().Be(105m);
    }

    [Fact]
    public void ApplyAdjustment_ShouldRoundToNearestIncrement()
    {
        // Arrange
        var tm = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var adjustment = TrainingMaxAdjustment.Percentage(0.012m); // 1.2% = 101.2, should round to 100

        // Act
        var result = tm.ApplyAdjustment(adjustment);

        // Assert
        result.Value.Should().Be(100m);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(100m, WeightUnit.Kilograms);

        // Act & Assert
        tm1.Should().Be(tm2);
        (tm1 == tm2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(105m, WeightUnit.Kilograms);

        // Act & Assert
        tm1.Should().NotBe(tm2);
        (tm1 != tm2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentUnits_ShouldNotBeEqual()
    {
        // Arrange
        var tm1 = TrainingMax.Create(100m, WeightUnit.Kilograms);
        var tm2 = TrainingMax.Create(100m, WeightUnit.Pounds);

        // Act & Assert
        tm1.Should().NotBe(tm2);
    }
}
