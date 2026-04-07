using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class WorkoutStartedEventTests
{
    [Fact]
    public void Start_ShouldRaiseWorkoutStartedEvent()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        workout.ClearDomainEvents();

        workout.Start();

        workout.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkoutStarted>();
    }

    [Fact]
    public void Start_ShouldRaiseEventWithCorrectWorkoutId()
    {
        var workout = new WorkoutBuilder().WithDefaultLinearExercise().Build();
        workout.ClearDomainEvents();

        workout.Start();

        var @event = workout.DomainEvents.OfType<WorkoutStarted>().Single();

        @event.WorkoutId.Should().Be(workout.Id);
    }
}
