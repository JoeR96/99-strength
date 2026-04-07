using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class CompletionUndoneEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UndoLastCompletion_ShouldRaiseCompletionUndoneEvent()
    {
        var workout = CreateActiveWorkoutAndCompleteDay1();
        workout.ClearDomainEvents();

        workout.UndoLastCompletion();

        workout.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CompletionUndone>();
    }

    [Fact]
    public void UndoLastCompletion_ShouldRaiseEventWithCorrectDay()
    {
        var workout = CreateActiveWorkoutAndCompleteDay1();
        workout.ClearDomainEvents();

        workout.UndoLastCompletion();

        var @event = workout.DomainEvents.OfType<CompletionUndone>().Single();

        @event.Day.Should().Be(DayNumber.Day1);
        @event.WeekNumber.Should().Be(1);
    }

    [Fact]
    public void UndoLastCompletion_ShouldRaiseEventWithCorrectWorkoutId()
    {
        var workout = CreateActiveWorkoutAndCompleteDay1();
        workout.ClearDomainEvents();

        workout.UndoLastCompletion();

        var @event = workout.DomainEvents.OfType<CompletionUndone>().Single();

        @event.WorkoutId.Should().Be(workout.Id);
    }

    private static Workout CreateActiveWorkoutAndCompleteDay1()
    {
        var workout = new WorkoutBuilder()
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1)
            .WithDefaultLinearExercise("Bench", DayNumber.Day2, 1)
            .WithDefaultLinearExercise("Deadlift", DayNumber.Day3, 1)
            .WithDefaultLinearExercise("OHP", DayNumber.Day4, 1)
            .WithDefaultLinearExercise("Row", DayNumber.Day5, 1)
            .Build();
        workout.Start();

        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var weight = Weight.Kilograms(80m);
        var planned = new[] { new PlannedSet(1, weight, 5, isAmrap: true) };
        var completed = new[] { new CompletedSet(1, weight, 5, wasAmrap: true) };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        return workout;
    }
}
