using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class ProgramRestartedEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UpdateBlockSequence_WhenWorkoutIsCompleted_ShouldRaiseProgramRestartedEvent()
    {
        var workout = CreateCompletedWorkout();
        workout.ClearDomainEvents();

        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        workout.DomainEvents.Should().Contain(e => e is ProgramRestarted);
    }

    [Fact]
    public void UpdateBlockSequence_WhenWorkoutIsCompleted_ShouldHaveCorrectWorkoutId()
    {
        var workout = CreateCompletedWorkout();
        workout.ClearDomainEvents();

        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        var @event = workout.DomainEvents.OfType<ProgramRestarted>().Single();
        @event.WorkoutId.Should().Be(workout.Id);
    }

    [Fact]
    public void UpdateBlockSequence_WhenWorkoutIsCompleted_ShouldHaveCorrectArchivedCount()
    {
        var workout = CreateCompletedWorkout();
        var completedCount = workout.CompletedActivities.Count;
        workout.ClearDomainEvents();

        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        var @event = workout.DomainEvents.OfType<ProgramRestarted>().Single();
        @event.ArchivedActivitiesCount.Should().Be(completedCount);
    }

    [Fact]
    public void UpdateBlockSequence_WhenWorkoutIsActive_ShouldNotRaiseProgramRestartedEvent()
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
        workout.ClearDomainEvents();

        workout.UpdateBlockSequence(new List<int> { 1, 2, 3 });

        workout.DomainEvents.Should().NotContain(e => e is ProgramRestarted);
    }

    private static Workout CreateCompletedWorkout()
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

        for (var week = 1; week <= 21; week++)
        {
            CompleteAllDaysInWeek(workout);
        }

        return workout;
    }

    private static void CompleteAllDaysInWeek(Workout workout)
    {
        for (var d = 1; d <= 5; d++)
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
