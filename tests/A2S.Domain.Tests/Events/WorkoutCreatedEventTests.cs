using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class WorkoutCreatedEventTests
{
    [Fact]
    public void Create_ShouldRaiseWorkoutCreatedEvent()
    {
        var workout = new WorkoutBuilder()
            .WithName("Test Workout")
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise()
            .Build();

        workout.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkoutCreated>();
    }

    [Fact]
    public void Create_ShouldRaiseEventWithCorrectName()
    {
        var workout = new WorkoutBuilder()
            .WithName("My Program")
            .WithDefaultLinearExercise()
            .Build();

        var @event = workout.DomainEvents.OfType<WorkoutCreated>().Single();

        @event.Name.Should().Be("My Program");
    }

    [Fact]
    public void Create_ShouldRaiseEventWithCorrectVariant()
    {
        var workout = new WorkoutBuilder()
            .WithVariant(ProgramVariant.FourDay)
            .WithDefaultLinearExercise()
            .Build();

        var @event = workout.DomainEvents.OfType<WorkoutCreated>().Single();

        @event.Variant.Should().Be(ProgramVariant.FourDay);
    }

    [Fact]
    public void Create_ShouldRaiseEventWithCorrectExerciseCount()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1)
            .WithDefaultLinearExercise("Bench", DayNumber.Day2, 1)
            .Build();

        var @event = workout.DomainEvents.OfType<WorkoutCreated>().Single();

        @event.ExerciseCount.Should().Be(2);
    }

    [Fact]
    public void Create_ShouldRaiseEventWithCorrectWorkoutId()
    {
        var workout = new WorkoutBuilder()
            .WithDefaultLinearExercise()
            .Build();

        var @event = workout.DomainEvents.OfType<WorkoutCreated>().Single();

        @event.WorkoutId.Should().Be(workout.Id);
    }
}
