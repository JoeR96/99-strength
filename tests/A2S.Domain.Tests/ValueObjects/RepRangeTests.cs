using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class RepRangeTests
{
    [Fact]
    public void Create_WithValidRange_ShouldSucceed()
    {
        // Act
        var range = RepRange.Create(8, 10, 12);

        // Assert
        range.Minimum.Should().Be(8);
        range.Target.Should().Be(10);
        range.Maximum.Should().Be(12);
    }

    [Fact]
    public void Create_WithMinimumZero_ShouldThrowException()
    {
        // Act
        Action act = () => RepRange.Create(0, 10, 12);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Minimum reps must be greater than zero*");
    }

    [Fact]
    public void Create_WithTargetLessThanMinimum_ShouldThrowException()
    {
        // Act
        Action act = () => RepRange.Create(10, 8, 12);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Target must be greater than or equal to minimum*");
    }

    [Fact]
    public void Create_WithMaximumLessThanTarget_ShouldThrowException()
    {
        // Act
        Action act = () => RepRange.Create(8, 12, 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Maximum must be greater than or equal to target*");
    }

    [Fact]
    public void Create_WithSpanExceeding10_ShouldThrowException()
    {
        // Act
        Action act = () => RepRange.Create(5, 10, 20);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Rep range span cannot exceed 10 reps*");
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(10, false)]
    [InlineData(12, false)]
    public void IsBelowMinimum_ShouldReturnCorrectResult(int actualReps, bool expected)
    {
        // Arrange
        var range = RepRange.Create(8, 10, 12);

        // Act
        var result = range.IsBelowMinimum(actualReps);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(13, true)]
    [InlineData(11, false)]
    [InlineData(8, false)]
    public void MeetsMaximum_ShouldReturnCorrectResult(int actualReps, bool expected)
    {
        // Arrange
        var range = RepRange.Create(8, 10, 12);

        // Act
        var result = range.MeetsMaximum(actualReps);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(12, true)]
    [InlineData(13, false)] // Above maximum
    public void IsInRange_ShouldReturnCorrectResult(int actualReps, bool expected)
    {
        // Arrange
        var range = RepRange.Create(8, 10, 12);

        // Act
        var result = range.IsInRange(actualReps);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CommonRanges_ShouldHaveCorrectValues()
    {
        // Assert
        RepRange.Common.Low.Should().Be(RepRange.Create(4, 6, 8));
        RepRange.Common.MediumLow.Should().Be(RepRange.Create(6, 8, 10));
        RepRange.Common.Medium.Should().Be(RepRange.Create(8, 10, 12));
        RepRange.Common.MediumHigh.Should().Be(RepRange.Create(10, 12, 15));
        RepRange.Common.High.Should().Be(RepRange.Create(12, 15, 20));
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var range = RepRange.Create(8, 10, 12);

        // Act
        var result = range.ToString();

        // Assert
        result.Should().Be("8-10-12");
    }

    [Fact]
    public void Equals_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var range1 = RepRange.Create(8, 10, 12);
        var range2 = RepRange.Create(8, 10, 12);

        // Act & Assert
        range1.Should().Be(range2);
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var range1 = RepRange.Create(8, 10, 12);
        var range2 = RepRange.Create(6, 8, 10);

        // Act & Assert
        range1.Should().NotBe(range2);
    }
}
