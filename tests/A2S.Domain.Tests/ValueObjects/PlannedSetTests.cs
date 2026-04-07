using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.ValueObjects;

public class PlannedSetTests
{
    [Fact]
    public void Constructor_WhenValidValues_ShouldCreateSuccessfully()
    {
        var weight = Weight.Kilograms(80m);
        var set = new PlannedSet(1, weight, 5);

        set.SetNumber.Should().Be(1);
        set.Weight.Should().Be(weight);
        set.TargetReps.Should().Be(5);
        set.IsAmrap.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenAmrapSet_ShouldStoreAmrapFlag()
    {
        var set = new PlannedSet(5, Weight.Kilograms(80m), 5, isAmrap: true);

        set.IsAmrap.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenSetNumberIsZero_ShouldThrow()
    {
        Action act = () => new PlannedSet(0, Weight.Kilograms(80m), 5);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Set number must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenNegativeSetNumber_ShouldThrow()
    {
        Action act = () => new PlannedSet(-1, Weight.Kilograms(80m), 5);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Set number must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenZeroTargetReps_ShouldThrow()
    {
        Action act = () => new PlannedSet(1, Weight.Kilograms(80m), 0);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Target reps must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenNegativeTargetReps_ShouldThrow()
    {
        Action act = () => new PlannedSet(1, Weight.Kilograms(80m), -1);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Target reps must be greater than zero*");
    }

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new PlannedSet(1, weight, 5, false);
        var s2 = new PlannedSet(1, weight, 5, false);

        s1.Should().Be(s2);
        (s1 == s2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentTargetReps_ShouldNotBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new PlannedSet(1, weight, 5);
        var s2 = new PlannedSet(1, weight, 6);

        s1.Should().NotBe(s2);
    }

    [Fact]
    public void Equals_WhenDifferentAmrapFlag_ShouldNotBeEqual()
    {
        var weight = Weight.Kilograms(80m);
        var s1 = new PlannedSet(1, weight, 5, false);
        var s2 = new PlannedSet(1, weight, 5, true);

        s1.Should().NotBe(s2);
    }

    [Fact]
    public void ToString_WhenNonAmrap_ShouldFormatWithoutPlus()
    {
        var set = new PlannedSet(1, Weight.Kilograms(80m), 5);

        var result = set.ToString();

        result.Should().Contain("Set 1");
        result.Should().Contain("5");
        result.Should().NotContain("+");
    }

    [Fact]
    public void ToString_WhenAmrap_ShouldIncludePlusIndicator()
    {
        var set = new PlannedSet(1, Weight.Kilograms(80m), 5, isAmrap: true);

        var result = set.ToString();

        result.Should().Contain("5+");
    }
}
