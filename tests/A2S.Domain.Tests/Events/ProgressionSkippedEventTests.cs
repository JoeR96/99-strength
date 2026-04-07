using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class ProgressionSkippedEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompleteDay_WhenSkipProgressionTrue_ShouldRaiseProgressionSkippedEvent()
    {
        var workout = CreateActiveWorkout();
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var weight = Weight.Kilograms(80m);
        var planned = new[] { new PlannedSet(1, weight, 5) };
        var completed = new[] { new CompletedSet(1, weight, 5) };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate, skipProgression: true);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.Should().Contain(e => e is ProgressionSkipped);
    }

    [Fact]
    public void CompleteDay_WhenSkipProgressionTrue_ShouldHaveCorrectPayload()
    {
        var workout = CreateActiveWorkout();
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var weight = Weight.Kilograms(80m);
        var planned = new[] { new PlannedSet(1, weight, 5) };
        var completed = new[] { new CompletedSet(1, weight, 5) };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate, skipProgression: true);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        var @event = workout.DomainEvents.OfType<ProgressionSkipped>().First();
        @event.WorkoutId.Should().Be(workout.Id);
        @event.ExerciseId.Should().Be(exercise.Id.Value);
        @event.ExerciseName.Should().Be(exercise.Name);
        @event.WeekNumber.Should().Be(1);
        @event.Reason.Should().Be("Temporary substitution");
    }

    [Fact]
    public void CompleteDay_WhenNoSkipProgression_ShouldNotRaiseProgressionSkippedEvent()
    {
        var workout = CreateActiveWorkout();
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);
        var weight = Weight.Kilograms(80m);
        var planned = new[] { new PlannedSet(1, weight, 5, isAmrap: true) };
        var completed = new[] { new CompletedSet(1, weight, 5, wasAmrap: true) };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.Should().NotContain(e => e is ProgressionSkipped);
    }

    private static Workout CreateActiveWorkout()
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
        return workout;
    }
}
