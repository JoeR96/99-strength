using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class WeekProgressedEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WhenAllDaysCompleted_ShouldRaiseWeekProgressedEvent()
    {
        var workout = CreateFiveDayWorkout();
        workout.Start();
        CompleteAllDaysInWeek(workout);

        workout.DomainEvents.Should().Contain(e => e is WeekProgressed);
    }

    [Fact]
    public void WhenWeekProgressed_ShouldHaveCorrectPreviousAndNewWeek()
    {
        var workout = CreateFiveDayWorkout();
        workout.Start();
        workout.ClearDomainEvents();
        CompleteAllDaysInWeek(workout);

        var @event = workout.DomainEvents.OfType<WeekProgressed>().First();

        @event.PreviousWeek.Should().Be(1);
        @event.NewWeek.Should().Be(2);
    }

    [Fact]
    public void WhenWeekProgressed_ShouldHaveCorrectBlockInfo()
    {
        var workout = CreateFiveDayWorkout();
        workout.Start();
        workout.ClearDomainEvents();
        CompleteAllDaysInWeek(workout);

        var @event = workout.DomainEvents.OfType<WeekProgressed>().First();

        @event.NewBlock.Should().Be(1);
        @event.IsDeloadWeek.Should().BeFalse();
    }

    [Fact]
    public void WhenProgressingToDeloadWeek_ShouldSetIsDeloadWeekTrue()
    {
        var workout = CreateFiveDayWorkout();
        workout.Start();

        // Complete weeks 1-6 to reach week 7 (deload)
        for (var week = 1; week <= 6; week++)
        {
            workout.ClearDomainEvents();
            CompleteAllDaysInWeek(workout);
        }

        var @event = workout.DomainEvents.OfType<WeekProgressed>().Last();

        @event.IsDeloadWeek.Should().BeTrue();
        @event.NewWeek.Should().Be(7);
    }

    [Fact]
    public void WhenWeekProgressed_ShouldHaveCorrectWorkoutId()
    {
        var workout = CreateFiveDayWorkout();
        workout.Start();
        workout.ClearDomainEvents();
        CompleteAllDaysInWeek(workout);

        var @event = workout.DomainEvents.OfType<WeekProgressed>().First();

        @event.WorkoutId.Should().Be(workout.Id);
    }

    private static Workout CreateFiveDayWorkout()
    {
        return new WorkoutBuilder()
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1)
            .WithDefaultLinearExercise("Bench", DayNumber.Day2, 1)
            .WithDefaultLinearExercise("Deadlift", DayNumber.Day3, 1)
            .WithDefaultLinearExercise("OHP", DayNumber.Day4, 1)
            .WithDefaultLinearExercise("Row", DayNumber.Day5, 1)
            .Build();
    }

    private static void CompleteAllDaysInWeek(Workout workout)
    {
        var daysPerWeek = workout.GetDaysPerWeek();
        for (var d = 1; d <= daysPerWeek; d++)
        {
            var day = (DayNumber)d;
            if (workout.IsDayCompletedInCurrentWeek(day))
            {
                continue;
            }

            var exercise = workout.Exercises.First(e => e.AssignedDay == day);
            var weight = Weight.Kilograms(80m);
            var planned = new[] { new PlannedSet(1, weight, 5, isAmrap: true) };
            var completed = new[] { new CompletedSet(1, weight, 5, wasAmrap: true) };
            var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
            workout.CompleteDay(day, new[] { performance });
        }
    }
}
