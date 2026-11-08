using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;

namespace A2S.Application.Tests.Hevy;

public class RoutineBuilderTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly A2SProgramProvider Program = new();

    private static Workout UpperA()
    {
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        var cableRow = Exercise.CreateWithRepsPerSetProgression("Single Arm Cable Row", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 2, "D0C4A899", RepRange.Create(10, 10), 3, 3, true, Weight.Create(20m, WeightUnit.Kilograms));
        var facePull = Exercise.CreateWithRepsPerSetProgression("Face Pull", ExerciseCategory.Accessory, EquipmentType.Cable,
            DayNumber.Day1, 3, "BE640BA0", RepRange.Create(15, 15), 3, 5, false, Weight.Create(15m, WeightUnit.Kilograms));
        var wallSlide = Exercise.CreateWithRepsPerSetProgression("Wall Slide with Lift-Off", ExerciseCategory.Accessory, EquipmentType.Bodyweight,
            DayNumber.Day1, 4, "custom:wall-slide-with-lift-off", RepRange.Create(10, 10), 2, 4, false, Weight.Create(0m, WeightUnit.Kilograms));
        var workout = new WorkoutBuilder().WithUserId(Joe).WithName(CutTo85Nov2026.Name).WithVariant(ProgramVariant.FourDay)
            .WithExercise(bench).WithExercise(cableRow).WithExercise(facePull).WithExercise(wallSlide).Build();
        workout.Start();
        return workout;
    }

    private static MobilityRoutine MobilityA() => MobilityRoutine.Create(Joe, "Mobility A", 10,
    [
        new MobilityDrill("90/90 breathing", 2, DrillDose.Breaths(5), SideBias.None, null, "Back-to-wall breathing", false),
        new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, "Extra set opening the left", null, true)
    ]);

    private static Dictionary<string, string> DrillTemplates() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["90/90 breathing"] = "T-breathing",
        ["Open books"] = "T-open-books"
    };

    [Fact]
    public void Lift_PutsMobilityAFirst_ThenTheDaysExercises_WithAmrapAsFailure_RestAndSupersets()
    {
        var workout = UpperA();
        var templates = workout.Exercises.ToDictionary(e => e.Id, e => e.ExternalTemplateId.StartsWith("custom:") ? "T-wall-slide" : e.ExternalTemplateId);

        var draft = RoutineBuilder.Lift(
            RoutineBuilder.LiftTitle(CutTo85Nov2026.Name, workout.CurrentWeek, "Upper A"), 42, workout, DayNumber.Day1,
            Program.GetWeekParameters(1), MobilityA(), DrillTemplates(), templates, CutTo85Nov2026.Supersets(), CutTo85Nov2026.ExerciseNotes());

        draft.Title.Should().Be("Cut to 85 — Nov 2026 · W1 Upper A");
        draft.FolderId.Should().Be(42);
        draft.Exercises.Select(e => e.ExerciseTemplateId).Should().Equal("T-breathing", "T-open-books", "79D0BB3A", "D0C4A899", "BE640BA0", "T-wall-slide");

        var breathing = draft.Exercises[0];
        breathing.Sets.Should().HaveCount(2).And.AllSatisfy(s => s.Reps.Should().Be(5));
        breathing.RestSeconds.Should().Be(0);
        breathing.Notes.Should().Be("2 × 5 breaths · Standing: Back-to-wall breathing");
        draft.Exercises[1].Sets.Should().HaveCount(3, "the left side gets an extra set");
        draft.Exercises[1].Notes.Should().Be("2 × 5 reps each side, extra set left · Extra set opening the left");

        var bench = draft.Exercises[2];
        bench.RestSeconds.Should().Be(RestDefaults.MainLiftSeconds);
        bench.SupersetId.Should().BeNull();
        bench.Sets.Should().HaveCountGreaterThan(1);
        bench.Sets.SkipLast(1).Should().AllSatisfy(s => s.Type.Should().Be(HevySetTypes.Normal));
        bench.Sets[^1].Type.Should().Be(HevySetTypes.Failure);
        bench.Sets.Should().AllSatisfy(s => s.WeightKg.Should().BeGreaterThan(0));
        bench.Notes.Should().StartWith("TM: ").And.Contain("Block 1 - Volume phase").And.Contain("AMRAP last set");

        var cableRow = draft.Exercises[3];
        var facePull = draft.Exercises[4];
        cableRow.SupersetId.Should().Be(0);
        facePull.SupersetId.Should().Be(0);
        cableRow.RestSeconds.Should().Be(RestDefaults.SupersetSeconds);
        facePull.RestSeconds.Should().Be(RestDefaults.SupersetSeconds);
        cableRow.Notes.Should().Contain("Rep range: 10-10").And.Contain("Unilateral").And.EndWith("10 per side, extra set left. Superset with face pulls.");
        cableRow.Sets.Should().HaveCount(3).And.AllSatisfy(s => { s.WeightKg.Should().Be(20m); s.Reps.Should().Be(10); s.Type.Should().Be(HevySetTypes.Normal); });

        var wallSlide = draft.Exercises[5];
        wallSlide.SupersetId.Should().BeNull("its partner is on Upper B in the brief, not this day");
        wallSlide.RestSeconds.Should().Be(RestDefaults.AccessorySeconds);
        wallSlide.Sets.Should().HaveCount(2).And.AllSatisfy(s => s.WeightKg.Should().BeNull("bodyweight sets carry no weight"));
    }

    [Fact]
    public void Lift_WithoutAWarmUpOrBriefData_IsJustTheExercises()
    {
        var workout = UpperA();
        var templates = workout.Exercises.ToDictionary(e => e.Id, e => e.ExternalTemplateId);

        var draft = RoutineBuilder.Lift("Plan · W1 Upper A", null, workout, DayNumber.Day1, Program.GetWeekParameters(1),
            null, new Dictionary<string, string>(), templates, [], new Dictionary<string, string>());

        draft.FolderId.Should().BeNull();
        draft.Exercises.Should().HaveCount(4);
        draft.Exercises.Should().AllSatisfy(e => e.SupersetId.Should().BeNull());
        draft.Exercises[1].RestSeconds.Should().Be(RestDefaults.AccessorySeconds);
    }

    [Fact]
    public void Lift_WithAMissingTemplate_Throws()
    {
        var workout = UpperA();

        var act = () => RoutineBuilder.Lift("x", null, workout, DayNumber.Day1, Program.GetWeekParameters(1),
            null, new Dictionary<string, string>(), new Dictionary<ExerciseId, string>(), [], new Dictionary<string, string>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bench Press (Barbell)*");
    }

    [Fact]
    public void Cardio_FourByFour_HasOneDurationSetPerInterval_WithTheSequenceInTheNotes()
    {
        var protocol = CardioProtocol.NorwegianFourByFour(Joe);

        var draft = RoutineBuilder.Cardio(RoutineBuilder.SlotTitle("Cut to 85", "Norwegian 4×4"), 42, protocol, "T-4x4");

        draft.Title.Should().Be("Cut to 85 · Norwegian 4×4");
        var exercise = draft.Exercises.Should().ContainSingle().Subject;
        exercise.ExerciseTemplateId.Should().Be("T-4x4");
        exercise.Sets.Should().HaveCount(9);
        exercise.Sets.Select(s => s.DurationSeconds).Should().Equal(600, 240, 180, 240, 180, 240, 180, 240, 300);
        exercise.Sets.Should().AllSatisfy(s => { s.Reps.Should().BeNull(); s.DistanceMeters.Should().BeNull(); });
        exercise.Notes.Should().StartWith(protocol.Notes).And.Contain("Warm-up 10:00 (warm-up)").And.Contain("Work 1 4:00 (work)").And.Contain("Recovery 1 3:00 (recovery)");
    }

    [Fact]
    public void Cardio_Liss_IsOneDistanceDurationSet()
    {
        var protocol = CardioProtocol.Liss(Joe, 35, "Incline walk");

        var draft = RoutineBuilder.Cardio("Cut to 85 · LISS", null, protocol, "T-liss");

        var set = draft.Exercises.Single().Sets.Should().ContainSingle().Subject;
        set.DurationSeconds.Should().Be(35 * 60);
    }

    [Fact]
    public void Cardio_CouchToFiveK_AlternatesRunAndWalkSets()
    {
        var protocol = CardioProtocol.CouchToFiveK(Joe, 1);

        var draft = RoutineBuilder.Cardio("Cut to 85 · C25K", null, protocol, "T-c25k");

        var sets = draft.Exercises.Single().Sets;
        sets.Should().HaveCount(2 + 16);
        sets[1].DurationSeconds.Should().Be(60);
        sets[2].DurationSeconds.Should().Be(90);
    }

    [Fact]
    public void Mobility_UsesDurationSetsForTimedDrills()
    {
        var routine = MobilityRoutine.Create(Joe, "Mobility B", 25,
        [
            new MobilityDrill("Side plank", 3, DrillDose.Seconds(30), SideBias.Left, "Extra set on the left", null, true),
            new MobilityDrill("Push-up plus", 3, DrillDose.Reps(12), SideBias.None, null, null, false)
        ]);
        var templates = new Dictionary<string, string> { ["Side plank"] = "T-side-plank", ["Push-up plus"] = "T-push-up-plus" };

        var draft = RoutineBuilder.Mobility("Cut to 85 · Mobility B", 42, routine, templates);

        draft.Notes.Should().Be("Mobility B · 25 min");
        draft.Exercises[0].Sets.Should().HaveCount(4).And.AllSatisfy(s => { s.DurationSeconds.Should().Be(30); s.Reps.Should().BeNull(); });
        draft.Exercises[0].Notes.Should().Be("3 × 30 s each side, extra set left · Extra set on the left");
        draft.Exercises[1].Sets.Should().HaveCount(3).And.AllSatisfy(s => s.Reps.Should().Be(12));
        draft.Exercises[1].Notes.Should().Be("3 × 12 reps");
    }
}
