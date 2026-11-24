using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.GymPlanFit;

public class PlanV3TemplateTests
{
    private static readonly DateOnly Mon23Nov2026 = new(2026, 11, 23);
    private static readonly IPlanTemplate Template = new PlanV3BaseBuildTransition();

    private static SuggestedPlanSpec Spec(decimal? benchTm = null) => Template.Build(new PlanTemplateInputs(
        new Dictionary<string, decimal?> { ["benchTm"] = benchTm }, 95m, Mon23Nov2026));

    [Fact]
    public void Both_templates_are_registered()
    {
        PlanTemplates.All.Select(t => t.Key).Should().Equal(CutTo85Nov2026.Key, PlanV3BaseBuildTransition.TemplateKey);
    }

    [Fact]
    public void Phases_are_base_build_transition_over_26_weeks()
    {
        Spec().Phases.Select(p => (p.Type, p.From, p.To)).Should().Equal(
            (PhaseType.Cut, Mon23Nov2026, new DateOnly(2027, 1, 17)),
            (PhaseType.Cut, new DateOnly(2027, 1, 18), new DateOnly(2027, 3, 14)),
            (PhaseType.Maintenance, new DateOnly(2027, 3, 15), new DateOnly(2027, 5, 23)));
    }

    [Fact]
    public void Main_lifts_start_pending_with_the_right_a2s2_tier_unless_a_tm_is_given()
    {
        var mains = Spec(benchTm: 90m).Exercises.Where(e => e.ProgressionType == "Linear").ToList();

        mains.Select(e => (e.TemplateName, e.Tier, e.TrainingMaxPending)).Should().Equal(
            ("Bench Press (Barbell)", (ProgramTier?)ProgramTier.Primary, false),
            ("Incline Bench Press (Smith Machine)", (ProgramTier?)ProgramTier.Auxiliary, true),
            ("Front Squat", (ProgramTier?)ProgramTier.Auxiliary, true));
        Spec().BlockSequence.Should().Equal(1, 2, 3);
        Template.Inputs.Select(i => i.Key).Should().NotContain("pauseSquatTm");
    }

    [Fact]
    public void Pause_squat_is_reps_per_set_4_x_4_to_6_first_on_lower_a_with_weight_pending()
    {
        var pause = Spec().Exercises.Single(e => e.TemplateName == "Pause Squat (Barbell)");

        pause.ProgressionType.Should().Be("RepsPerSet");
        pause.Category.Should().Be(ExerciseCategory.MainLift);
        (pause.AssignedDay, pause.OrderInDay).Should().Be((DayNumber.Day2, 1));
        (pause.StartingSets, pause.RepRangeMinimum, pause.RepRangeMaximum).Should().Be((4, 4, 6));
        pause.StartingWeight.Should().BeNull();
        pause.TrainingMaxValue.Should().BeNull();
    }

    [Fact]
    public void No_supersets_and_no_walk_builder_anywhere()
    {
        var spec = Spec();

        spec.Exercises.Should().OnlyContain(e => e.SupersetGroup == null);
        spec.Supersets.Should().BeEmpty();
        spec.CardioTracks.Should().Equal(CardioTrackTemplates.WalkToRun);
        spec.Schedule.Should().NotContain(s => s.Kind == SessionKind.Cardio);
        spec.Schedule.Where(s => s.Day is DayOfWeek.Wednesday or DayOfWeek.Saturday).Select(s => (s.Kind, s.MobilityRoutine, s.Label)).Should().Equal(
            (SessionKind.Mobility, PlanV3BaseBuildTransition.FullBodyFlow, "Full-body flow (20 min)"),
            (SessionKind.Mobility, PlanV3BaseBuildTransition.FullBodyFlow, "Full-body flow (20 min), or a follow-along video"));
        spec.TrackGates![CardioTrackTemplates.WalkToRun].Should().BeEquivalentTo(StartGate.WeightAndPainRunningGate(), o => o.WithStrictOrdering());
    }

    [Fact]
    public void PlanV3_EachLiftDay_EndsWithItsCardioExercise()
    {
        var spec = Spec();

        var last = new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 }
            .Select(day => spec.Exercises.Where(e => e.AssignedDay == day).MaxBy(e => e.OrderInDay)!)
            .Select(e => (e.AssignedDay, e.TemplateName, e.ExternalTemplateId, e.ProgressionType, e.Category, e.CardioLane))
            .ToList();

        last.Should().Equal(
            (DayNumber.Day1, "Stair Machine (Floors)", "4377A52B", CardioStageStrategy.TypeName, ExerciseCategory.Cardio, (CardioLane?)CardioLane.Hard),
            (DayNumber.Day2, "Treadmill", "243710DE", CardioStageStrategy.TypeName, ExerciseCategory.Cardio, (CardioLane?)CardioLane.Easy),
            (DayNumber.Day3, "Stair Machine (Floors)", "4377A52B", CardioStageStrategy.TypeName, ExerciseCategory.Cardio, (CardioLane?)CardioLane.Hard),
            (DayNumber.Day4, "Treadmill", "243710DE", CardioStageStrategy.TypeName, ExerciseCategory.Cardio, (CardioLane?)CardioLane.Easy));
        spec.Exercises.Where(e => e.Category == ExerciseCategory.Cardio).Should().HaveCount(4);
        spec.Exercises.GroupBy(e => e.AssignedDay).Should().OnlyContain(day =>
            day.Select(e => e.OrderInDay).OrderBy(o => o).SequenceEqual(Enumerable.Range(1, day.Count())));
    }

    [Fact]
    public void PlanV3_LiftSlots_HaveNoFinishers()
    {
        var spec = Spec();

        spec.Schedule.Where(s => s.Kind == SessionKind.Lift).Should().HaveCount(4).And.OnlyContain(s => s.Finisher == null);
        spec.CardioTracks.Should().NotContain(CardioTrackTemplates.StairFinisher);
    }

    [Fact]
    public void PlanV3_BuildPhaseNote_SaysIntervalsAreAutomatic()
    {
        var phases = Spec().Phases;

        phases[0].Note.Should().Contain("stairs and incline walk progress on their own each workout");
        phases[1].Changes.Should().Contain("Stair climber moves to 4 × 4 intervals on its own once continuous work reaches 12 min")
            .And.NotContain(c => c.Contains("finisher", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_extra_sets_and_no_starting_weights_anywhere()
    {
        var spec = Spec();
        spec.Exercises.Should().OnlyContain(e => e.ExtraSetSide == ExtraSetSide.None);
        spec.Exercises.Where(e => e.ProgressionType == "RepsPerSet").Should().OnlyContain(e => e.StartingWeight == null);
        spec.Routines.SelectMany(r => r.Drills).Should().OnlyContain(d => d.ExtraSetSide == SideBias.None);
    }

    [Fact]
    public void Preview_numbers_supersets_and_lists_cardio_warm_ups_routines_tracks_and_what_to_fill_in()
    {
        var preview = PlanTemplatePreview.Build(Template, Mon23Nov2026, alreadyStarted: false);

        var upperA = preview.Sessions.Single(s => s.Label == "Upper A");
        upperA.Exercises.Select(e => $"{e.Slot} {e.Name} {e.Prescription}").Should().Equal(
            "1 Bench Press (Barbell) 5 sets, last set AMRAP",
            "2 Bent Over Row (Barbell) 3 × 8–10",
            "3 Single Arm Lateral Raise (Cable) 2 × 12–15 each side, left first",
            "4 Single Arm Cable Row 2 × 10–12 each side, left first",
            "5 Skullcrusher (Dumbbell) 2 × 10–12",
            "6 Bicep Curl (Dumbbell) 2 × 10–12",
            "7 Cable Core Palloff Press 2 × 10 each side, left first",
            "8 Stair Machine (Floors) 3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
        preview.Sessions.Single(s => s.Label == "Lower A").Exercises[0].Should().BeEquivalentTo(new
        {
            Slot = "1", Name = "Pause Squat (Barbell)", Prescription = "4 × 4–6", Progression = "Reps per set"
        });
        upperA.WarmUp.Should().Be(PlanV3BaseBuildTransition.WarmUpUpper);
        upperA.Exercises[^1].Progression.Should().Be("Cardio (hard lane)");
        preview.Sessions.Should().OnlyContain(s => s.Finisher == null);
        preview.Sessions.Single(s => s.Label == "Lower B").Exercises[^1].Should().BeEquivalentTo(new
        {
            Name = "Treadmill", Prescription = "10 min @ ? % incline", Progression = "Cardio (easy lane)"
        });
        preview.Sessions.Single(s => s.Label == "Upper B").Exercises[0].Progression.Should().Be("A2S2 auxiliary scheme");
        preview.Routines.Select(r => r.Name).Should().Equal("Warm-up (upper)", "Warm-up (lower)", "Hips & lower back", "Upper back & chest", "Full-body flow");
        preview.CardioTracks.Select(t => t.Key).Should().Equal(CardioTrackTemplates.WalkToRun);
        preview.ToFillIn.Should().HaveCount(4).And.Contain(i => i.StartsWith("Starting weights for 20 accessories"));
        preview.ProgramWeeks.Should().Be(26);
        preview.Slots.Should().HaveCount(11);
    }

    [Theory]
    [InlineData("2026-11-25", "2026-11-30")]
    [InlineData("2026-11-30", "2026-11-30")]
    [InlineData("2026-01-01", "2026-11-23")]
    public void Start_date_snaps_to_monday_never_in_the_past(string requested, string expected)
    {
        PlanTemplateInputBinder.StartDate(DateOnly.Parse(requested), new DateOnly(2026, 11, 22)).Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void Required_inputs_of_cut_to_85_are_enforced_through_the_generic_binder()
    {
        var result = PlanTemplateInputBinder.Bind(new CutTo85Template(), new Dictionary<string, decimal?>(), null, null, new DateOnly(2026, 11, 22));

        result.IsFailure.Should().BeTrue();
    }
}

public class PlanTemplateApplierTests
{
    private static readonly UserId Joe = new("user_template_apply");
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly IMobilityRoutineRepository _routines = Substitute.For<IMobilityRoutineRepository>();
    private readonly IExerciseLibraryProvider _library = Substitute.For<IExerciseLibraryProvider>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly List<TrainingPlan> _added = [];
    private readonly List<Workout> _addedWorkouts = [];
    private readonly ApplyPlanTemplateCommandHandler _handler;

    public PlanTemplateApplierTests()
    {
        _user.UserId.Returns(Joe.Value);
        _library.GetByName(Arg.Any<string>()).Returns(ci => new ExerciseTemplate(ci.Arg<string>(), EquipmentType.Dumbbell, RepRange.Common.Medium, 3, "Test", "chest", []));
        _routines.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<MobilityRoutine>());
        _tracks.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<CardioTrack>());
        _plans.AddAsync(Arg.Do<TrainingPlan>(_added.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _workouts.AddAsync(Arg.Do<Workout>(_addedWorkouts.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        _handler = new ApplyPlanTemplateCommandHandler(_plans, _workouts, Substitute.For<ICardioProtocolRepository>(), _routines,
            Substitute.For<IHabitRepository>(), _library, Substitute.For<IUnitOfWork>(), _user, clock, _tracks);
    }

    private static ApplyPlanTemplateCommand Apply(bool confirm = false) =>
        new(PlanV3BaseBuildTransition.TemplateKey, new DateOnly(2026, 11, 25), new Dictionary<string, decimal?>(), null, confirm);

    [Fact]
    public async Task Applies_plan_v3_with_pending_tms_cardio_exercises_and_tracks_without_supersets()
    {
        var result = await _handler.Handle(Apply(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var plan = _added.Single();
        plan.StartDate.Should().Be(new DateOnly(2026, 11, 30));
        plan.Schedule.Where(s => s.Kind == SessionKind.Lift).Should().OnlyContain(s => s.Finisher == null);
        plan.Schedule.Single(s => s.Label == "Upper A").WarmUpRoutineId.Should().NotBeNull();
        plan.Schedule.Should().NotContain(s => s.Kind == SessionKind.Cardio);
        var workout = _addedWorkouts.Single();
        workout.Exercises.Where(e => e.Progression is LinearProgressionStrategy).Should().HaveCount(3).And.OnlyContain(e => e.IsTrainingMaxPending);
        var pause = workout.Exercises.Single(e => e.Name == "Pause Squat (Barbell)");
        pause.Progression.Should().BeOfType<RepsPerSetStrategy>();
        pause.Category.Should().Be(ExerciseCategory.MainLift);
        workout.Exercises.Should().OnlyContain(e => e.SupersetGroup == null);
        workout.BlockSequence.Should().Equal(1, 2, 3);
        workout.Exercises.Where(e => e.Category == ExerciseCategory.Cardio)
            .Select(e => (e.AssignedDay, e.Name, ((CardioStageStrategy)e.Progression).Lane)).Should().BeEquivalentTo(new[]
            {
                (DayNumber.Day1, "Stair Machine (Floors)", CardioLane.Hard), (DayNumber.Day2, "Treadmill", CardioLane.Easy),
                (DayNumber.Day3, "Stair Machine (Floors)", CardioLane.Hard), (DayNumber.Day4, "Treadmill", CardioLane.Easy)
            });
        await _tracks.Received(1).AddAsync(Arg.Any<CardioTrack>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Walk_to_run_waits_for_its_running_gate_and_phases_carry_their_changes_and_the_squat_swap()
    {
        var added = new List<CardioTrack>();
        _tracks.AddAsync(Arg.Do<CardioTrack>(added.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(Apply(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var run = added.Single(t => t.TemplateKey == CardioTrackTemplates.WalkToRun);
        run.Status.Should().Be(CardioTrackStatus.Gated);
        run.StartGate.Select(c => c.Type).Should().Equal(GateConditionType.TrendWeightAtOrBelow, GateConditionType.NoPainInAreas);
        added.Should().NotContain(t => t.TemplateKey == CardioTrackTemplates.WalkBuilder);
        added.Where(t => t.TemplateKey != CardioTrackTemplates.WalkToRun).Should().OnlyContain(t => t.StartGate.Count == 0);
        var phases = _added.Single().Phases;
        phases.Select(p => p.Label).Should().Equal("Base", "Build", "Transition");
        phases[1].Changes.Should().Contain(c => c.Contains("intervals"));
        phases[2].Swaps.Should().ContainSingle().Which.Should().Be(new PhaseSwap("Pause Squat (Barbell)", "Squat (Barbell)"));
    }

    [Fact]
    public async Task Replacing_another_active_plan_needs_confirmation_and_keeps_it()
    {
        var old = TrainingPlan.Create(Joe, "My old plan", new DateOnly(2026, 11, 2), [ScheduledSession.Mobility(DayOfWeek.Sunday, null, "Stretch")], 95m, new WeeklyAllowances(1, 1));
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(old);

        var refused = await _handler.Handle(Apply(), CancellationToken.None);
        refused.ErrorCode.Should().Be(ErrorCode.Conflict);
        refused.Error.Should().Contain("My old plan");
        _added.Should().BeEmpty();

        var confirmed = await _handler.Handle(Apply(confirm: true), CancellationToken.None);

        confirmed.IsSuccess.Should().BeTrue();
        old.IsActive.Should().BeFalse();
        _plans.Received(1).Update(old);
    }

    [Fact]
    public async Task Applying_the_active_template_again_is_idempotent()
    {
        var active = TrainingPlan.Create(Joe, PlanV3BaseBuildTransition.TemplateName, new DateOnly(2026, 11, 30), [ScheduledSession.Mobility(DayOfWeek.Sunday, null, "Stretch")], 95m, new WeeklyAllowances(1, 1));
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(active);

        var result = await _handler.Handle(Apply(), CancellationToken.None);

        result.Value.AlreadyStarted.Should().BeTrue();
        _added.Should().BeEmpty();
        active.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_plan_started_under_the_old_Plan_v3_name_still_counts_as_already_started()
    {
        PlanV3BaseBuildTransition.TemplateName.Should().Be("Falador Fat Furnace");
        PlanTemplates.IsSamePlan("Plan v3 — Base/Build/Transition", PlanV3BaseBuildTransition.TemplateName).Should().BeTrue();
        var active = TrainingPlan.Create(Joe, "Plan v3 — Base/Build/Transition", new DateOnly(2026, 11, 30), [ScheduledSession.Mobility(DayOfWeek.Sunday, null, "Stretch")], 95m, new WeeklyAllowances(1, 1));
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(active);

        var result = await _handler.Handle(Apply(), CancellationToken.None);

        result.Value.AlreadyStarted.Should().BeTrue();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Applying_the_active_template_again_with_confirm_replace_starts_a_fresh_copy_and_keeps_the_old_one()
    {
        var active = TrainingPlan.Create(Joe, PlanV3BaseBuildTransition.TemplateName, new DateOnly(2026, 11, 30), [ScheduledSession.Mobility(DayOfWeek.Sunday, null, "Stretch")], 95m, new WeeklyAllowances(1, 1));
        (await _handler.Handle(Apply(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var oldWorkout = _addedWorkouts.Single();
        _added.Clear();
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(active);
        _workouts.GetActiveWorkoutAsync(Joe, Arg.Any<CancellationToken>()).Returns(oldWorkout);

        var result = await _handler.Handle(Apply(confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.AlreadyStarted.Should().BeFalse();
        _added.Should().ContainSingle().Which.Id.Should().NotBe(active.Id);
        active.IsActive.Should().BeFalse();
        _plans.Received(1).Update(active);
        oldWorkout.Status.Should().Be(WorkoutStatus.Paused);
    }

    [Fact]
    public async Task A_fresh_copy_moves_an_existing_gated_walk_to_run_onto_the_plans_gate()
    {
        var oldRun = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Joe, new DateOnly(2026, 11, 2), withGate: true);
        _tracks.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<CardioTrack> { oldRun });

        var result = await _handler.Handle(Apply(confirm: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        oldRun.StartGate.Should().BeEquivalentTo(StartGate.WeightAndPainRunningGate(), o => o.WithStrictOrdering());
        oldRun.Status.Should().Be(CardioTrackStatus.Gated);
        _tracks.Received(1).Update(oldRun);
    }
}
