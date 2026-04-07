using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Events;

public class TrainingMaxAdjustedEventTests
{
    private static readonly DateTime FixedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompleteDay_WhenAmrapExceedsTarget_ShouldRaiseTrainingMaxAdjustedEvent()
    {
        var workout = CreateActiveWorkout(trainingMax: 100m);
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);

        // Week 1 T1: reps=5, repOutTarget=10. If user does 13 reps on AMRAP (delta=+3), TM adjusts.
        var weight = Weight.Kilograms(79m); // 79% of 100 for week 1
        var planned = new[]
        {
            new PlannedSet(1, weight, 5),
            new PlannedSet(2, weight, 5),
            new PlannedSet(3, weight, 5),
            new PlannedSet(4, weight, 5, isAmrap: true)
        };
        var completed = new[]
        {
            new CompletedSet(1, weight, 5),
            new CompletedSet(2, weight, 5),
            new CompletedSet(3, weight, 5),
            new CompletedSet(4, weight, 13, wasAmrap: true) // 13 reps, target was 10 → delta = +3
        };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.Should().Contain(e => e is TrainingMaxAdjusted);
    }

    [Fact]
    public void CompleteDay_WhenAmrapExceedsTarget_ShouldHaveCorrectAmrapDelta()
    {
        var workout = CreateActiveWorkout(trainingMax: 100m);
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);

        var weight = Weight.Kilograms(79m);
        var planned = new[]
        {
            new PlannedSet(1, weight, 5),
            new PlannedSet(2, weight, 5, isAmrap: true)
        };
        var completed = new[]
        {
            new CompletedSet(1, weight, 5),
            new CompletedSet(2, weight, 8, wasAmrap: true) // delta = +3
        };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        var @event = workout.DomainEvents.OfType<TrainingMaxAdjusted>().First();
        @event.AmrapDelta.Should().Be(3);
    }

    [Fact]
    public void CompleteDay_WhenAmrapMatchesTarget_ShouldNotRaiseTrainingMaxAdjustedEvent()
    {
        var workout = CreateActiveWorkout(trainingMax: 100m);
        var exercise = workout.Exercises.First(e => e.AssignedDay == DayNumber.Day1);

        var weight = Weight.Kilograms(79m);
        var planned = new[]
        {
            new PlannedSet(1, weight, 5),
            new PlannedSet(2, weight, 5, isAmrap: true)
        };
        var completed = new[]
        {
            new CompletedSet(1, weight, 5),
            new CompletedSet(2, weight, 5, wasAmrap: true) // delta = 0
        };
        var performance = new ExercisePerformance(exercise.Id, planned, completed, FixedDate);
        workout.ClearDomainEvents();

        workout.CompleteDay(DayNumber.Day1, new[] { performance });

        workout.DomainEvents.Should().NotContain(e => e is TrainingMaxAdjusted);
    }

    private static Workout CreateActiveWorkout(decimal trainingMax)
    {
        var workout = new WorkoutBuilder()
            .WithVariant(ProgramVariant.FiveDay)
            .WithDefaultLinearExercise("Squat", DayNumber.Day1, 1, trainingMax)
            .WithDefaultLinearExercise("Bench", DayNumber.Day2, 1)
            .WithDefaultLinearExercise("Deadlift", DayNumber.Day3, 1)
            .WithDefaultLinearExercise("OHP", DayNumber.Day4, 1)
            .WithDefaultLinearExercise("Row", DayNumber.Day5, 1)
            .Build();
        workout.Start();
        return workout;
    }
}
