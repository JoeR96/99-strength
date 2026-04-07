using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class CompletedSetTests
{
    [Fact]
    public void Constructor_WhenValidValues_ShouldCreateSuccessfully()
    {
        var weight = Weight.Kilograms(80m);
        var set = new CompletedSet(1, weight, 5);

        set.SetNumber.Should().Be(1);
        set.Weight.Should().Be(weight);
        set.ActualReps.Should().Be(5);
        set.WasAmrap.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenAmrapSet_ShouldStoreAmrapFlag()
    {
        var weight = Weight.Kilograms(80m);
        var set = new CompletedSet(5, weight, 8, wasAmrap: true);

        set.WasAmrap.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenSetNumberIsZero_ShouldThrow()
    {
        Action act = () => new CompletedSet(0, Weight.Kilograms(80m), 5);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Set number must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenNegativeSetNumber_ShouldThrow()
    {
        Action act = () => new CompletedSet(-1, Weight.Kilograms(80m), 5);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Set number must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenNegativeReps_ShouldThrow()
    {
        Action act = () => new CompletedSet(1, Weight.Kilograms(80m), -1);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Actual reps cannot be negative*");
    }

    [Fact]
    public void Constructor_WhenZeroReps_ShouldSucceed()
    {
        var set = new CompletedSet(1, Weight.Kilograms(80m), 0);

        set.ActualReps.Should().Be(0);
    }

    [Theory]
    [InlineData(8, 5, 3)]
    [InlineData(5, 5, 0)]
    [InlineData(3, 5, -2)]
    [InlineData(10, 5, 5)]
    public void CalculateDelta_ShouldReturnDifferenceBetweenActualAndPlanned(
        int actualReps, int targetReps, int expectedDelta)
    {
        var completed = new CompletedSet(1, Weight.Kilograms(80m), actualReps, wasAmrap: true);
        var planned = new PlannedSet(1, Weight.Kilograms(80m), targetReps, isAmrap: true);

        var delta = completed.CalculateDelta(planned);

        delta.Should().Be(expectedDelta);
    }

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new CompletedSet(1, weight, 5, false);
        var s2 = new CompletedSet(1, weight, 5, false);

        s1.Should().Be(s2);
        (s1 == s2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentReps_ShouldNotBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new CompletedSet(1, weight, 5);
        var s2 = new CompletedSet(1, weight, 6);

        s1.Should().NotBe(s2);
    }

    [Fact]
    public void Equals_WhenDifferentAmrapFlag_ShouldNotBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new CompletedSet(1, weight, 5, false);
        var s2 = new CompletedSet(1, weight, 5, true);

        s1.Should().NotBe(s2);
    }

    [Fact]
    public void ToString_WhenNonAmrap_ShouldFormatWithoutIndicator()
    {
        var set = new CompletedSet(1, Weight.Kilograms(80m), 5);

        set.ToString().Should().Contain("Set 1");
        set.ToString().Should().Contain("5");
        set.ToString().Should().NotContain("AMRAP");
    }

    [Fact]
    public void ToString_WhenAmrap_ShouldIncludeAmrapIndicator()
    {
        var set = new CompletedSet(1, Weight.Kilograms(80m), 5, wasAmrap: true);

        set.ToString().Should().Contain("(AMRAP)");
    }
}
