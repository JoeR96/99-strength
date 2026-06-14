using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Regression tests: removing an exercise must keep each day's OrderInDay contiguous
/// (1, 2, 3, ...). A gap previously violated the sequential-ordering invariant and
/// surfaced as "Exercise ordering for DayN must be sequential starting from 1".
/// </summary>
public class RemoveExerciseOrderingTests
{
    private static readonly UserId TestUserId = new("eee11111-1111-1111-1111-111111111111");

    private static Exercise Accessory(string name, DayNumber day, int order) =>
        Exercise.CreateWithRepsPerSetProgression(
            name, ExerciseCategory.Accessory, EquipmentType.Cable,
            day, order, $"tpl-{name}", RepRange.Create(8, 12));

    [Fact]
    public void RemoveExercise_FromMiddleOfDay_ResequencesRemainingContiguously()
    {
        var day2 = new[]
        {
            Accessory("A", DayNumber.Day2, 1),
            Accessory("B", DayNumber.Day2, 2),
            Accessory("C", DayNumber.Day2, 3),
            Accessory("D", DayNumber.Day2, 4),
            Accessory("E", DayNumber.Day2, 5),
        };
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, day2);

        // Remove the 3rd exercise — previously left orders [1,2,4,5] (gap at 3).
        workout.RemoveExercise(day2[2].Id);

        var remaining = workout.Exercises
            .Where(e => e.AssignedDay == DayNumber.Day2)
            .OrderBy(e => e.OrderInDay)
            .ToList();

        remaining.Select(e => e.OrderInDay).Should().Equal(1, 2, 3, 4);
        remaining.Select(e => e.Name).Should().Equal("A", "B", "D", "E");
    }

    [Fact]
    public void RemoveExercise_OnlyResequencesAffectedDay()
    {
        var exercises = new[]
        {
            Accessory("D1A", DayNumber.Day1, 1),
            Accessory("D1B", DayNumber.Day1, 2),
            Accessory("D2A", DayNumber.Day2, 1),
            Accessory("D2B", DayNumber.Day2, 2),
        };
        var workout = Workout.Create(TestUserId, "Test", ProgramVariant.FourDay, exercises);

        workout.RemoveExercise(exercises[0].Id); // remove first of Day 1

        workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1)
            .OrderBy(e => e.OrderInDay).Select(e => e.OrderInDay)
            .Should().Equal(1);
        workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day2)
            .OrderBy(e => e.OrderInDay).Select(e => e.OrderInDay)
            .Should().Equal(1, 2);
    }
}
