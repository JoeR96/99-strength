using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class DayCompletedEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompleteDay_ShouldRaiseDayCompletedEvent()
    {
        var workout = CreateActiveWorkoutWithExercise(DayNumber.Day1);
        var exercise = workout.Exercises.First();
        var performances = CreatePerformances(exercise);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, performances);

        workout.DomainEvents.Should().Contain(e => e is DayCompleted);
    }

    [Fact]
    public void CompleteDay_ShouldRaiseEventWithCorrectWorkoutId()
    {
        var workout = CreateActiveWorkoutWithExercise(DayNumber.Day1);
        var exercise = workout.Exercises.First();
        var performances = CreatePerformances(exercise);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, performances);

        var @event = workout.DomainEvents.OfType<DayCompleted>().First();
        @event.WorkoutId.Should().Be(workout.Id);
    }

    [Fact]
    public void CompleteDay_ShouldRaiseEventWithCorrectDayAndWeek()
    {
        var workout = CreateActiveWorkoutWithExercise(DayNumber.Day1);
        var exercise = workout.Exercises.First();
        var performances = CreatePerformances(exercise);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, performances);

        var @event = workout.DomainEvents.OfType<DayCompleted>().First();
        @event.Day.Should().Be(DayNumber.Day1);
        @event.WeekNumber.Should().Be(1);
    }

    [Fact]
    public void CompleteDay_ShouldRaiseEventWithCorrectExerciseCount()
    {
        var workout = CreateActiveWorkoutWithExercises(DayNumber.Day1, 2);
        var exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();
        var performances = exercises.Select(e => CreatePerformance(e)).ToList();
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, performances);

        var @event = workout.DomainEvents.OfType<DayCompleted>().First();
        @event.ExerciseCount.Should().Be(2);
    }

    private static Workout CreateActiveWorkoutWithExercise(DayNumber day)
    {
        var workout = new WorkoutBuilder()
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Bench Press", day, 1)
            .WithDefaultLinearExercise("Squat", DayNumber.Day2, 1)
            .WithDefaultLinearExercise("Deadlift", DayNumber.Day3, 1)
            .WithDefaultLinearExercise("OHP", DayNumber.Day4, 1)
            .WithDefaultLinearExercise("Row", DayNumber.Day5, 1)
            .Build();
        workout.Start();
        return workout;
    }

    private static Workout CreateActiveWorkoutWithExercises(DayNumber day, int count)
    {
        var builder = new WorkoutBuilder().WithVariant(ProgramVariant.FiveDay);
        for (var i = 1; i <= count; i++)
        {
            builder.WithDefaultLinearExercise($"Exercise {i}", day, i);
        }

        // Fill remaining days
        builder.WithDefaultLinearExercise("D2", DayNumber.Day2, 1);
        builder.WithDefaultLinearExercise("D3", DayNumber.Day3, 1);
        builder.WithDefaultLinearExercise("D4", DayNumber.Day4, 1);
        builder.WithDefaultLinearExercise("D5", DayNumber.Day5, 1);

        var workout = builder.Build();
        workout.Start();
        return workout;
    }

    private static List<ExercisePerformance> CreatePerformances(Exercise exercise)
    {
        return new List<ExercisePerformance> { CreatePerformance(exercise) };
    }

    private static ExercisePerformance CreatePerformance(Exercise exercise)
    {
        var weight = Weight.Kilograms(80m);
        var planned = new[] { new PlannedSet(1, weight, 5, isAmrap: true) };
        var completed = new[] { new CompletedSet(1, weight, 5, wasAmrap: true) };
        return new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
    }
}
