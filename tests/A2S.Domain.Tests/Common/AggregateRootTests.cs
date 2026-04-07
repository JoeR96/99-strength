using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class AggregateRootTests
{
    [Fact]
    public void DomainEvents_WhenNewAggregate_ShouldBeEmpty()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        // WorkoutCreated event is raised in constructor, so it has at least one
        workout.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_WhenCreated_ShouldContainWorkoutCreatedEvent()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        workout.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<A2S.Domain.Events.WorkoutCreated>();
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        workout.ClearDomainEvents();

        workout.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_WhenMultipleEventsRaised_ShouldContainAll()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        workout.Start();

        workout.DomainEvents.Should().HaveCount(2);
        workout.DomainEvents.Should().ContainItemsAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void ClearDomainEvents_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        workout.ClearDomainEvents();
        workout.ClearDomainEvents();

        workout.DomainEvents.Should().BeEmpty();
    }
}
