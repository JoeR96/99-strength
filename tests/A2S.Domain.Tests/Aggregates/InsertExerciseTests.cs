using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Adding an exercise to a running plan slots it in and keeps each day contiguous (1, 2, 3, ...).
/// </summary>
public class InsertExerciseTests
{
    private static readonly UserId TestUserId = new("eee22222-2222-2222-2222-222222222222");

    private static Exercise Accessory(string name, DayNumber day, int order) =>
        Exercise.CreateWithRepsPerSetProgression(
            name, ExerciseCategory.Accessory, EquipmentType.Dumbbell,
            day, order, $"tpl-{name}", RepRange.Create(10, 12));

    private static Workout UpperA() => Workout.Create(TestUserId, "Test", ProgramVariant.FourDay,
    [
        Accessory("Bench", DayNumber.Day1, 1),
        Accessory("Row", DayNumber.Day1, 2),
        Accessory("Skullcrusher", DayNumber.Day1, 3),
        Accessory("Pallof", DayNumber.Day1, 4),
        Accessory("Squat", DayNumber.Day2, 1),
    ]);

    private static IEnumerable<string> Day(Workout workout, DayNumber day) =>
        workout.Exercises.Where(e => e.AssignedDay == day).OrderBy(e => e.OrderInDay).Select(e => $"{e.OrderInDay} {e.Name}");

    [Fact]
    public void InsertExercise_InTheMiddle_MovesLaterExercisesDownOne()
    {
        var workout = UpperA();

        workout.InsertExercise(Accessory("Curl", DayNumber.Day1, 4));

        Day(workout, DayNumber.Day1).Should().Equal("1 Bench", "2 Row", "3 Skullcrusher", "4 Curl", "5 Pallof");
        Day(workout, DayNumber.Day2).Should().Equal("1 Squat");
    }

    [Fact]
    public void InsertExercise_PastTheEnd_GoesLast()
    {
        var workout = UpperA();

        workout.InsertExercise(Accessory("Curl", DayNumber.Day1, 9));

        Day(workout, DayNumber.Day1).Should().Equal("1 Bench", "2 Row", "3 Skullcrusher", "4 Pallof", "5 Curl");
    }

    [Fact]
    public void InsertExercise_OnAnEmptyDay_IsFirst()
    {
        var workout = UpperA();

        workout.InsertExercise(Accessory("Curl", DayNumber.Day3, 3));

        Day(workout, DayNumber.Day3).Should().Equal("1 Curl");
    }

    [Fact]
    public void InsertExercise_Twice_WithTheSameExercise_IsRejected()
    {
        var workout = UpperA();
        var curl = Accessory("Curl", DayNumber.Day1, 4);
        workout.InsertExercise(curl);

        var again = () => workout.InsertExercise(curl);

        again.Should().Throw<BusinessRuleViolationException>();
    }
}
