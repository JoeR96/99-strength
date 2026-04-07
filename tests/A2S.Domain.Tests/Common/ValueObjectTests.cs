using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class ValueObjectTests
{
    [Fact]
    public void Equals_WhenSameComponents_ShouldBeEqual()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        w1.Equals(w2).Should().BeTrue();
        w1.Equals((object)w2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentComponents_ShouldNotBeEqual()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(105m);

        w1.Equals(w2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenNull_ShouldReturnFalse()
    {
        var weight = Weight.Kilograms(100m);

        weight.Equals(null).Should().BeFalse();
        weight.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferentType_ShouldReturnFalse()
    {
        var weight = Weight.Kilograms(100m);
        var repRange = RepRange.Create(8, 12);

        weight.Equals(repRange).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WhenSameComponents_ShouldBeSame()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        w1.GetHashCode().Should().Be(w2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenDifferentComponents_ShouldLikelyDiffer()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(105m);

        w1.GetHashCode().Should().NotBe(w2.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_WhenBothSameValue_ShouldReturnTrue()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        (w1 == w2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_WhenDifferentValues_ShouldReturnTrue()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(105m);

        (w1 != w2).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_WhenBothNull_ShouldReturnTrue()
    {
        Weight? w1 = null;
        Weight? w2 = null;

        (w1 == w2).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_WhenOneNull_ShouldReturnFalse()
    {
        var w1 = Weight.Kilograms(100m);
        Weight? w2 = null;

        (w1 == w2).Should().BeFalse();
        (w2 == w1).Should().BeFalse();
    }

    [Fact]
    public void CheckRule_WhenViolated_ShouldThrowBusinessRuleViolationException()
    {
        Action act = () => Weight.Kilograms(-1m);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void CheckRule_WhenSatisfied_ShouldNotThrow()
    {
        Action act = () => Weight.Kilograms(50m);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValueObject_ShouldBeUsableAsDictionaryKey()
    {
        var w1 = Weight.Kilograms(100m);
        var w2 = Weight.Kilograms(100m);

        var dict = new Dictionary<Weight, string> { [w1] = "found" };

        dict.Should().ContainKey(w2);
        dict[w2].Should().Be("found");
    }

    [Fact]
    public void ValueObject_ShouldWorkInHashSet()
    {
        var set = new HashSet<Weight>
        {
            Weight.Kilograms(100m),
            Weight.Kilograms(100m),
            Weight.Kilograms(105m)
        };

        set.Should().HaveCount(2);
    }
}
