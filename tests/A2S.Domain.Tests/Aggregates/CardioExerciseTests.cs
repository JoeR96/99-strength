using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Cardio as an exercise on the workout: the stair climber (hard lane) and the incline walk (easy lane)
/// each progress once across both of their days.
/// </summary>
public class CardioExerciseTests
{
    private static readonly UserId TestUserId = new("ccc33333-3333-3333-3333-333333333333");
    private static readonly DateTime FixedDate = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Weight TestWeight = Weight.Kilograms(60m);

    private static Exercise Lift(string name, DayNumber day) =>
        new ExerciseBuilder().WithName(name).WithDay(day).WithOrder(1).WithTemplateId($"tpl-{name}")
            .AsRepsPerSet(startingWeight: TestWeight).Build();

    private static Exercise Stairs(DayNumber day) =>
        new ExerciseBuilder().WithDay(day).WithOrder(2).AsCardioStage(CardioLane.Hard).Build();

    private static Exercise Walk(DayNumber day) =>
        new ExerciseBuilder().WithDay(day).WithOrder(2).AsCardioStage(CardioLane.Easy).Build();

    private static Workout Week(bool start = true)
    {
        var workout = Workout.Create(TestUserId, "Cardio", ProgramVariant.FourDay,
        [
            Lift("Bench", DayNumber.Day1), Stairs(DayNumber.Day1),
            Lift("Squat", DayNumber.Day2), Walk(DayNumber.Day2),
            Lift("Press", DayNumber.Day3), Stairs(DayNumber.Day3),
            Lift("Deadlift", DayNumber.Day4), Walk(DayNumber.Day4),
        ]);
        if (start)
        {
            workout.Start();
        }

        return workout;
    }

    private static Exercise Cardio(Workout workout, DayNumber day) =>
        workout.Exercises.Single(e => e.AssignedDay == day && e.Category == ExerciseCategory.Cardio);

    private static string BlockText(Workout workout, DayNumber day) =>
        Cardio(workout, day).Progression.CalculateCardioBlock()!.Text;

    private static ExercisePerformance Session(Exercise exercise, EffortRating effort) =>
        new(exercise.Id, [], [], FixedDate,
            plannedCardio: exercise.Progression.CalculateCardioBlock(),
            cardio: new CardioResult(true, effort));

    [Fact]
    public void CreateWithCardioStageProgression_IsAMachineCardioExercise()
    {
        var exercise = Exercise.CreateWithCardioStageProgression(
            "Stair Machine (Floors)", DayNumber.Day1, 3, "4377A52B", CardioLane.Hard);

        exercise.Name.Should().Be("Stair Machine (Floors)");
        exercise.Category.Should().Be(ExerciseCategory.Cardio);
        exercise.Equipment.Should().Be(EquipmentType.Machine);
        exercise.ExternalTemplateId.Should().Be("4377A52B");
        exercise.Progression.Should().BeOfType<CardioStageStrategy>().Which.Lane.Should().Be(CardioLane.Hard);
        exercise.IsCardioSettingsPending.Should().BeFalse();
        RestDefaults.For(exercise).Should().Be(0);
    }

    [Fact]
    public void CompleteDay_EasyStairs_StepsUpBothUpperDays()
    {
        var workout = Week();

        workout.CompleteDay(DayNumber.Day1, [Session(Cardio(workout, DayNumber.Day1), EffortRating.Easy)]);

        BlockText(workout, DayNumber.Day1).Should().Be("3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)");
        BlockText(workout, DayNumber.Day3).Should().Be("3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void CompleteDay_WalkDoesNotTouchStairs()
    {
        var workout = Week();
        workout.SetCardioSettings(Cardio(workout, DayNumber.Day2).Id, 5m, 5.5m);

        workout.CompleteDay(DayNumber.Day2, [Session(Cardio(workout, DayNumber.Day2), EffortRating.Easy)]);

        BlockText(workout, DayNumber.Day2).Should().Be("12 min @ 5 % incline, 5.5 km/h");
        BlockText(workout, DayNumber.Day4).Should().Be("12 min @ 5 % incline, 5.5 km/h");
        BlockText(workout, DayNumber.Day1).Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
        BlockText(workout, DayNumber.Day3).Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void Undo_RestoresLaneSiblingToo()
    {
        var workout = Week();
        workout.CompleteDay(DayNumber.Day1, [Session(Cardio(workout, DayNumber.Day1), EffortRating.Easy)]);

        workout.UndoLastCompletion();

        BlockText(workout, DayNumber.Day1).Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
        BlockText(workout, DayNumber.Day3).Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void Complete_WhileSettingsPending_Throws()
    {
        var workout = Week();
        workout.SetCardioSettings(Cardio(workout, DayNumber.Day2).Id, 5m, null);
        var walk = Cardio(workout, DayNumber.Day2);

        var act = () => workout.CompleteDay(DayNumber.Day2, [Session(walk, EffortRating.Easy)]);

        walk.IsCardioSettingsPending.Should().BeTrue();
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Set the Treadmill settings before finishing it.");
    }

    [Fact]
    public void Complete_CardioResultOnALift_Throws()
    {
        var workout = Week();
        var bench = workout.Exercises.Single(e => e.Name == "Bench");
        var performance = new ExercisePerformance(
            bench.Id, [new PlannedSet(1, TestWeight, 10)], [new CompletedSet(1, TestWeight, 10)], FixedDate,
            cardio: new CardioResult(true, EffortRating.Easy));

        var act = () => workout.CompleteDay(DayNumber.Day1, [performance]);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Only a cardio exercise can take a cardio result.");
    }

    [Fact]
    public void SetCardioSettings_AddsAuditEntry_AndSyncsSibling()
    {
        var workout = Week();
        var walk = Cardio(workout, DayNumber.Day4);

        workout.SetCardioSettings(walk.Id, 5m, 5.5m);

        var entry = workout.AuditEntries.Should().ContainSingle().Subject;
        entry.Type.Should().Be(AuditEntryType.CardioSettingsChanged);
        entry.ExerciseId.Should().Be(walk.Id);
        entry.ExerciseName.Should().Be("Treadmill");
        entry.WeekNumber.Should().Be(1);
        entry.DayNumber.Should().Be(4);
        entry.OldValue.Should().Be("10 min @ ? % incline");
        entry.NewValue.Should().Be("10 min @ 5 % incline, 5.5 km/h");
        BlockText(workout, DayNumber.Day2).Should().Be("10 min @ 5 % incline, 5.5 km/h");
        Cardio(workout, DayNumber.Day2).IsCardioSettingsPending.Should().BeFalse();
    }

    [Fact]
    public void SetCardioSettings_Unchanged_AddsNoAuditEntry_AndLeavesTheStateAlone()
    {
        var workout = Week();
        var stairs = Cardio(workout, DayNumber.Day1);
        var stateBefore = ((CardioStageStrategy)stairs.Progression).State;
        var siblingBefore = ((CardioStageStrategy)Cardio(workout, DayNumber.Day3).Progression).State;

        workout.SetCardioSettings(stairs.Id, 6m, null);

        workout.AuditEntries.Should().BeEmpty();
        ((CardioStageStrategy)stairs.Progression).State.Should().BeSameAs(stateBefore);
        ((CardioStageStrategy)Cardio(workout, DayNumber.Day3).Progression).State.Should().BeSameAs(siblingBefore);
    }

    [Fact]
    public void SetCardioSettings_BeforeTheWorkoutStarts_IsAllowed()
    {
        var workout = Week(start: false);

        workout.SetCardioSettings(Cardio(workout, DayNumber.Day2).Id, 5m, 5.5m);

        BlockText(workout, DayNumber.Day4).Should().Be("10 min @ 5 % incline, 5.5 km/h");
    }

    [Fact]
    public void SetCardioSettings_OnAPausedWorkout_Throws()
    {
        var workout = Week();
        workout.Pause();

        var act = () => workout.SetCardioSettings(Cardio(workout, DayNumber.Day2).Id, 5m, 5.5m);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SetCardioSettings_OnNonCardioExercise_Throws()
    {
        var workout = Week();
        var bench = workout.Exercises.Single(e => e.Name == "Bench");

        var act = () => workout.SetCardioSettings(bench.Id, 5m, null);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Bench is not a cardio exercise");
    }

    [Fact]
    public void InsertExercise_Cardio_GoesLastOnTheDay()
    {
        var workout = Workout.Create(TestUserId, "Cardio", ProgramVariant.FourDay,
        [
            Lift("Bench", DayNumber.Day1),
            new ExerciseBuilder().WithName("Row").WithDay(DayNumber.Day1).WithOrder(2).WithTemplateId("tpl-row")
                .AsRepsPerSet(startingWeight: TestWeight).Build(),
        ]);

        workout.InsertExercise(Exercise.CreateWithCardioStageProgression(
            "Stair Machine (Floors)", DayNumber.Day1, 99, "4377A52B", CardioLane.Hard));

        workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).OrderBy(e => e.OrderInDay)
            .Select(e => $"{e.OrderInDay} {e.Name}")
            .Should().Equal("1 Bench", "2 Row", "3 Stair Machine (Floors)");
    }
}
