using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class EntityTests
{
    [Fact]
    public void Equals_WhenSameId_ShouldBeEqual()
    {
        var workout1 = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        var workout2 = workout1;

        workout1.Equals(workout2).Should().BeTrue();
        (workout1 == workout2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentId_ShouldNotBeEqual()
    {
        var workout1 = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        var workout2 = new WorkoutBuilder().WithDefaultLinearExercise().Build();

        workout1.Equals(workout2).Should().BeFalse();
        (workout1 != workout2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenComparedWithNull_ShouldNotBeEqual()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();

        workout.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenSameReference_ShouldBeEqual()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();

        workout.Equals(workout).Should().BeTrue();
        ReferenceEquals(workout, workout).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WhenSameId_ShouldBeSame()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        var sameRef = workout;

        workout.GetHashCode().Should().Be(sameRef.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_WhenBothNull_ShouldReturnTrue()
    {
        Workout? w1 = null;
        Workout? w2 = null;

        (w1 == w2).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_WhenOneNull_ShouldReturnFalse()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        Workout? nullWorkout = null;

        (workout == nullWorkout).Should().BeFalse();
        (nullWorkout == workout).Should().BeFalse();
    }

    [Fact]
    public void CheckRule_WhenConditionTrue_ShouldNotThrow()
    {
        Action act = () => new WorkoutBuilder()
            .WithName("Valid Name")
            .WithDefaultLinearExercise()
            .Build();

        act.Should().NotThrow();
    }

    [Fact]
    public void CheckRule_WhenConditionFalse_ShouldThrowBusinessRuleViolation()
    {
        Action act = () => new WorkoutBuilder()
            .WithName("")
            .WithDefaultLinearExercise()
            .Build();

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
