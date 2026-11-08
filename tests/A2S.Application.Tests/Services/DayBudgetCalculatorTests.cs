using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Services;

/// <summary>
/// Brief § 6 "Volume budget": cap warnings fire correctly when adding, swapping or reordering
/// exercises. Adding and reordering go through the aggregate and <see cref="DayBudgetCalculator"/>
/// (the path every exercise-changing command ends on); swapping goes through
/// <see cref="SubstituteExerciseCommandHandler"/> in both directions.
/// </summary>
public class DayBudgetCalculatorTests
{
    private static readonly UserId Joe = new("db011111-1111-1111-1111-111111111111");

    private readonly IExerciseLibraryProvider _library = Substitute.For<IExerciseLibraryProvider>();
    private readonly DayBudgetCalculator _calculator;

    public DayBudgetCalculatorTests()
    {
        _library.GetAllTemplatesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExerciseTemplate("Bench Press (Barbell)", EquipmentType.Barbell, MuscleGroup: "chest", SecondaryMuscleGroups: ["triceps", "shoulders"]),
            new ExerciseTemplate("Cable Fly", EquipmentType.Cable, MuscleGroup: "chest"),
            new ExerciseTemplate("Pec Deck (Machine)", EquipmentType.Machine, MuscleGroup: "chest"),
            new ExerciseTemplate("Incline Bench Press (Dumbbell)", EquipmentType.Dumbbell, MuscleGroup: "chest", SecondaryMuscleGroups: ["shoulders"]),
            new ExerciseTemplate("Chest Dip", EquipmentType.Bodyweight, MuscleGroup: "chest", SecondaryMuscleGroups: ["triceps"]),
            new ExerciseTemplate("Seated Row (Cable)", EquipmentType.Cable, MuscleGroup: "lats", SecondaryMuscleGroups: ["biceps"]),
            new ExerciseTemplate("Face Pull", EquipmentType.Cable, MuscleGroup: "shoulders", SecondaryMuscleGroups: ["upper_back"]),
            new ExerciseTemplate("Squat (Barbell)", EquipmentType.Barbell, MuscleGroup: "quadriceps", SecondaryMuscleGroups: ["glutes", "hamstrings"])
        });
        _calculator = new DayBudgetCalculator(new VolumeBudgetService(new A2SProgramProvider()), _library);
    }

    // ---------------------------------------------------------------------------------------
    // Adding
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Adding_AChestAccessoryThatTipsTheWeekOverTheCap_RaisesExactlyOneChestWarning()
    {
        // Bench 5 + Cable Fly 4 = 9 chest sets: under the cap of 10.
        var workout = UpperDay(
            Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 4));
        var before = await _calculator.AfterChangeAsync(workout, [DayNumber.Day1]);
        before.Warnings.Should().BeEmpty("9 chest sets are under the cap");

        workout.AddExercise(Accessory("Pec Deck (Machine)", EquipmentType.Machine, order: 3, sets: 3));
        var after = await _calculator.AfterChangeAsync(workout, [DayNumber.Day1]);

        after.Warnings.Should().ContainSingle();
        after.Warnings[0].Bucket.Should().Be("Chest");
        after.Warnings[0].Sets.Should().Be(12, "5 + 4 + 3");
        after.Warnings[0].Cap.Should().Be(10);
        after.Warnings[0].Message.Should().Be("Chest: 12 hard sets this week, cap 10");
    }

    [Fact]
    public async Task Adding_AnExerciseOnAnotherDay_StillCountsTowardsTheWeeklyCap()
    {
        // Day 1: bench 5. Day 3: incline press 4 → 9 chest. Adding dips 3 on day 3 → 12.
        var workout = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithDay(DayNumber.Day1).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithName("Incline Bench Press (Dumbbell)").WithCategory(ExerciseCategory.Accessory).WithEquipment(EquipmentType.Dumbbell).WithDay(DayNumber.Day3).WithOrder(1).AsRepsPerSet(startingSets: 4, targetSets: 4))
            .Build();
        workout.Start();
        (await _calculator.AfterChangeAsync(workout, [DayNumber.Day3])).Warnings.Should().BeEmpty();

        workout.AddExercise(Accessory("Chest Dip", EquipmentType.Bodyweight, order: 2, sets: 3, day: DayNumber.Day3));
        var after = await _calculator.AfterChangeAsync(workout, [DayNumber.Day3]);

        after.Warnings.Should().ContainSingle().Which.Sets.Should().Be(12, "the budget is per week, not per day");
        var dayThree = await _calculator.ForDayAsync(workout, DayNumber.Day3, 1);
        dayThree.HardSetsThisWeek["Chest"].Should().Be(12);
        dayThree.Time.OverBudget.Should().BeFalse("7 accessory sets on day 3 are 16 minutes plus the warm-up");
    }

    [Fact]
    public async Task Adding_UncappedArmWork_NeverWarns()
    {
        var workout = UpperDay(Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 3));
        _library.GetAllTemplatesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExerciseTemplate("Bench Press (Barbell)", EquipmentType.Barbell, MuscleGroup: "chest"),
            new ExerciseTemplate("Cable Fly", EquipmentType.Cable, MuscleGroup: "chest"),
            new ExerciseTemplate("Triceps Pushdown", EquipmentType.Cable, MuscleGroup: "triceps"),
            new ExerciseTemplate("Bicep Curl (Cable)", EquipmentType.Cable, MuscleGroup: "biceps")
        });

        workout.AddExercise(Accessory("Triceps Pushdown", EquipmentType.Cable, order: 3, sets: 6));
        workout.AddExercise(Accessory("Bicep Curl (Cable)", EquipmentType.Cable, order: 4, sets: 6));
        var after = await _calculator.AfterChangeAsync(workout, [DayNumber.Day1]);
        var day = await _calculator.ForDayAsync(workout, DayNumber.Day1, 1);

        after.Warnings.Should().BeEmpty("arms have no cap in the brief");
        day.HardSetsThisWeek["Arms"].Should().Be(12);
    }

    // ---------------------------------------------------------------------------------------
    // Swapping
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Swapping_AChestAccessoryForABackOne_ClearsTheChestWarning()
    {
        var workout = UpperDay(
            Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 4),
            Accessory("Pec Deck (Machine)", EquipmentType.Machine, order: 3, sets: 3));
        var handler = SubstituteHandlerFor(workout);
        (await _calculator.AfterChangeAsync(workout, [DayNumber.Day1])).Warnings.Should().ContainSingle("12 chest sets before the swap");
        var pecDeck = workout.Exercises.Single(e => e.Name == "Pec Deck (Machine)");

        var result = await handler.Handle(
            new SubstituteExerciseCommand(workout.Id.Value, pecDeck.Id.Value, "Seated Row (Cable)", "seated-row-cable"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().BeEmpty("9 chest sets after the swap");
        var day = await _calculator.ForDayAsync(workout, DayNumber.Day1, 1);
        day.HardSetsThisWeek["Chest"].Should().Be(9);
        day.HardSetsThisWeek["Back"].Should().Be(3);
    }

    [Fact]
    public async Task Swapping_ABackAccessoryForAChestOne_RaisesTheChestWarning()
    {
        var workout = UpperDay(
            Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 4),
            Accessory("Seated Row (Cable)", EquipmentType.Cable, order: 3, sets: 3));
        var handler = SubstituteHandlerFor(workout);
        var row = workout.Exercises.Single(e => e.Name == "Seated Row (Cable)");

        var result = await handler.Handle(
            new SubstituteExerciseCommand(workout.Id.Value, row.Id.Value, "Pec Deck (Machine)", "pec-deck"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("warnings never block");
        result.Value.Warnings.Should().ContainSingle().Which.Message.Should().Be("Chest: 12 hard sets this week, cap 10");
    }

    [Fact]
    public async Task Swapping_ToAnExerciseTheLibraryDoesNotKnow_DropsItFromTheCountRatherThanGuessing()
    {
        var workout = UpperDay(
            Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 4),
            Accessory("Pec Deck (Machine)", EquipmentType.Machine, order: 3, sets: 3));
        var handler = SubstituteHandlerFor(workout);
        var pecDeck = workout.Exercises.Single(e => e.Name == "Pec Deck (Machine)");

        var result = await handler.Handle(
            new SubstituteExerciseCommand(workout.Id.Value, pecDeck.Id.Value, "Mystery Machine", "mystery"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().BeEmpty();
        (await _calculator.ForDayAsync(workout, DayNumber.Day1, 1)).HardSetsThisWeek["Chest"].Should().Be(9);
    }

    // ---------------------------------------------------------------------------------------
    // Reordering
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Reordering_ExercisesWithinADay_LeavesVolumeWarningsAndTimeUnchanged()
    {
        var workout = UpperDay(
            Accessory("Cable Fly", EquipmentType.Cable, order: 2, sets: 4),
            Accessory("Pec Deck (Machine)", EquipmentType.Machine, order: 3, sets: 3),
            Accessory("Face Pull", EquipmentType.Cable, order: 4, sets: 3));
        var before = await _calculator.AfterChangeAsync(workout, [DayNumber.Day1]);
        var dayBefore = await _calculator.ForDayAsync(workout, DayNumber.Day1, 1);
        var fly = workout.Exercises.Single(e => e.Name == "Cable Fly");
        var facePull = workout.Exercises.Single(e => e.Name == "Face Pull");

        // Face pulls move to the front of the accessories, flies to the back.
        workout.ReorderExercise(facePull.Id, 2);
        workout.ReorderExercise(fly.Id, 4);
        var after = await _calculator.AfterChangeAsync(workout, [DayNumber.Day1]);
        var dayAfter = await _calculator.ForDayAsync(workout, DayNumber.Day1, 1);

        workout.GetExercisesForDay(DayNumber.Day1).Select(e => e.Name).Should().Equal(
            "Bench Press (Barbell)", "Face Pull", "Pec Deck (Machine)", "Cable Fly");
        before.Warnings.Should().ContainSingle().Which.Sets.Should().Be(12);
        after.Warnings.Should().BeEquivalentTo(before.Warnings, "moving exercises around changes no set counts");
        after.OverTimeBudget.Should().Be(before.OverTimeBudget);
        dayAfter.HardSetsThisWeek.Should().BeEquivalentTo(dayBefore.HardSetsThisWeek);
        dayAfter.Time.TotalMinutes.Should().Be(dayBefore.Time.TotalMinutes);
        dayAfter.Time.PerExercise.Select(e => e.Name).Should().Equal("Bench Press (Barbell)", "Face Pull", "Pec Deck (Machine)", "Cable Fly");
        dayAfter.Time.PerExercise.Sum(e => e.Minutes).Should().Be(dayBefore.Time.PerExercise.Sum(e => e.Minutes));
    }

    // ---------------------------------------------------------------------------------------
    // Time budget flag follows the affected days
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AfterChange_FlagsTheTimeBudgetOnlyWhenAnAffectedDayIsOver()
    {
        // Day 1 is long (bench + six accessories of 4 sets = 19 + 6 × 9 = 73 min + warm-up); day 2 is just a squat.
        var builder = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithDay(DayNumber.Day1).WithOrder(1).AsLinear())
            .WithExercise(e => e.WithName("Squat (Barbell)").WithDay(DayNumber.Day2).WithOrder(1).AsLinear());
        for (var i = 0; i < 6; i++)
        {
            var order = i + 2;
            builder.WithExercise(e => e.WithName($"Accessory {order}").WithCategory(ExerciseCategory.Accessory).WithDay(DayNumber.Day1).WithOrder(order).AsRepsPerSet(startingSets: 4, targetSets: 4));
        }
        var workout = builder.Build();
        workout.Start();

        var dayTwoOnly = await _calculator.AfterChangeAsync(workout, [DayNumber.Day2]);
        var dayOneToo = await _calculator.AfterChangeAsync(workout, [DayNumber.Day2, DayNumber.Day1]);

        dayTwoOnly.OverTimeBudget.Should().BeFalse("the squat day is 29 minutes");
        dayOneToo.OverTimeBudget.Should().BeTrue();
        (await _calculator.ForDayAsync(workout, DayNumber.Day1, 1)).Time.OverBudget.Should().BeTrue();
        (await _calculator.ForDayAsync(workout, DayNumber.Day2, 1)).Time.OverBudget.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------


    /// <summary>Day 1 of a four-day split: bench (5 A2S2 sets in week 1) plus the given accessories.</summary>
    private static Workout UpperDay(params Exercise[] accessories)
    {
        var builder = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithDay(DayNumber.Day1).WithOrder(1).AsLinear());
        foreach (var accessory in accessories)
        {
            builder.WithExercise(accessory);
        }

        var workout = builder.Build();
        workout.Start();
        return workout;
    }

    private static Exercise Accessory(string name, EquipmentType equipment, int order, int sets, DayNumber day = DayNumber.Day1) =>
        new ExerciseBuilder()
            .WithName(name)
            .WithCategory(ExerciseCategory.Accessory)
            .WithEquipment(equipment)
            .WithDay(day)
            .WithOrder(order)
            .WithTemplateId(name.ToLowerInvariant().Replace(' ', '-'))
            .AsRepsPerSet(startingSets: sets, targetSets: Math.Max(sets, 4), startingWeight: Weight.Create(20m, WeightUnit.Kilograms))
            .Build();

    private SubstituteExerciseCommandHandler SubstituteHandlerFor(Workout workout)
    {
        var repository = Substitute.For<IWorkoutRepository>();
        repository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(workout);
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(Joe.Value);
        return new SubstituteExerciseCommandHandler(repository, Substitute.For<IUnitOfWork>(), currentUser, _calculator);
    }
}
