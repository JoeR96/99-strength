using A2S.Application.DTOs;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Application.Tests.SuggestedPlans;

/// <summary>
/// Brief § 5, line by line. Every table row and bullet in the brief is a row here; a deviation is a
/// failing test, not a judgement call.
/// </summary>
public class CutTo85Nov2026Tests
{
    private static readonly DateOnly Wed11Nov2026 = new(2026, 11, 11);
    private static readonly DateOnly Mon16Nov2026 = new(2026, 11, 16);

    private static SuggestedPlanInputs Inputs(DateOnly? today = null, decimal startWeight = 95m) =>
        new(BenchTm: 100m, SquatTm: 140m, OhpTm: 60m, DeadliftTm: 180m, StartWeightKg: startWeight, Today: today ?? Wed11Nov2026);

    private static readonly SuggestedPlanSpec Spec = CutTo85Nov2026.Build(Inputs());

    // ---- Identity and start ----------------------------------------------------------------------

    [Fact]
    public void Build_NamesThePlanAsTheBriefDoes()
    {
        Spec.Key.Should().Be("cut-to-85");
        Spec.Name.Should().Be("Cut to 85 — Nov 2026");
        Spec.Variant.Should().Be(ProgramVariant.FourDay);
        Spec.StartWeightKg.Should().Be(95m);
    }

    [Theory]
    [InlineData(2026, 11, 11, 2026, 11, 16)] // Wednesday → coming Monday
    [InlineData(2026, 11, 16, 2026, 11, 16)] // Monday starts that day
    [InlineData(2026, 11, 15, 2026, 11, 16)] // Sunday → tomorrow
    public void Build_StartsOnTheComingMonday(int y, int m, int d, int ey, int em, int ed)
    {
        var spec = CutTo85Nov2026.Build(Inputs(new DateOnly(y, m, d)));

        spec.StartDate.Should().Be(new DateOnly(ey, em, ed));
        spec.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    // ---- Weekly schedule table -------------------------------------------------------------------

    [Theory]
    [InlineData(0, DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A")]
    [InlineData(1, DayOfWeek.Tuesday, SessionKind.Lift, DayNumber.Day2, null, null, "Lower A")]
    [InlineData(2, DayOfWeek.Wednesday, SessionKind.Cardio, null, CardioProtocolKind.NorwegianFourByFour, null, "Norwegian 4×4 — outdoors, hills")]
    [InlineData(3, DayOfWeek.Wednesday, SessionKind.Mobility, null, null, "Mobility B", "Mobility B (25 min)")]
    [InlineData(4, DayOfWeek.Thursday, SessionKind.Lift, DayNumber.Day3, null, null, "Upper B")]
    [InlineData(5, DayOfWeek.Friday, SessionKind.Lift, DayNumber.Day4, null, null, "Lower B")]
    [InlineData(6, DayOfWeek.Saturday, SessionKind.Cardio, null, null, null, "C25K run or LISS 30–40 min")]
    [InlineData(7, DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest — walk only")]
    public void Schedule_MatchesTheBriefTable(int index, DayOfWeek day, SessionKind kind, DayNumber? liftDay, CardioProtocolKind? cardio, string? mobility, string label)
    {
        Spec.Schedule.Should().HaveCount(8);

        var slot = Spec.Schedule[index];
        slot.Day.Should().Be(day);
        slot.Kind.Should().Be(kind);
        slot.LiftDay.Should().Be(liftDay);
        slot.Cardio.Should().Be(cardio);
        slot.MobilityRoutine.Should().Be(mobility);
        slot.Label.Should().Be(label);
    }

    [Fact]
    public void Schedule_SatisfiesTheNoFourByFourBeforeALowerDayRule()
    {
        var sessions = Spec.Schedule.Select(s => new ScheduledSession(s.Day, s.Kind, s.LiftDay, null, null, s.Label));

        PlanScheduleRules.Warnings(sessions).Should().BeEmpty();
    }

    // ---- Lifting ---------------------------------------------------------------------------------

    [Theory]
    // Upper A
    [InlineData(DayNumber.Day1, 1, "Bench Press (Barbell)", "79D0BB3A", "Linear", 0, 0, 0, false)]
    [InlineData(DayNumber.Day1, 2, "Chest Supported Incline Row (Dumbbell)", "914F3A96", "RepsPerSet", 8, 10, 3, false)]
    [InlineData(DayNumber.Day1, 3, "Single Arm Landmine Press (Barbell)", "1837BA23", "RepsPerSet", 8, 8, 3, true)]
    [InlineData(DayNumber.Day1, 4, "Single Arm Cable Row", "D0C4A899", "RepsPerSet", 10, 10, 3, true)]
    [InlineData(DayNumber.Day1, 5, "Face Pull", "BE640BA0", "RepsPerSet", 15, 15, 3, false)]
    // Lower A
    [InlineData(DayNumber.Day2, 1, "Squat (Barbell)", "D04AC939", "Linear", 0, 0, 0, false)]
    [InlineData(DayNumber.Day2, 2, "Romanian Deadlift (Barbell)", "2B4B7310", "RepsPerSet", 8, 8, 3, false)]
    [InlineData(DayNumber.Day2, 3, "Bulgarian Split Squat", "B5D3A742", "RepsPerSet", 8, 8, 3, true)]
    [InlineData(DayNumber.Day2, 4, "Side Plank", "E3EDA509", "RepsPerSet", 30, 30, 3, true)]
    [InlineData(DayNumber.Day2, 5, "Cable Core Pallof Press", "CC55119B", "RepsPerSet", 10, 10, 3, true)]
    // Upper B
    [InlineData(DayNumber.Day3, 1, "Overhead Press (Barbell)", "7B8D84E8", "Linear", 0, 0, 0, false)]
    [InlineData(DayNumber.Day3, 2, "Lat Pulldown (Cable)", "6A6C31A5", "RepsPerSet", 8, 10, 3, false)]
    [InlineData(DayNumber.Day3, 3, "Incline Bench Press (Dumbbell)", "07B38369", "RepsPerSet", 10, 10, 3, false)]
    [InlineData(DayNumber.Day3, 4, "Seal Row (Barbell)", "custom:seal-row-barbell", "RepsPerSet", 10, 10, 3, false)]
    [InlineData(DayNumber.Day3, 5, "Wall Slide with Lift-Off", "custom:wall-slide-with-lift-off", "RepsPerSet", 10, 10, 2, false)]
    // Lower B
    [InlineData(DayNumber.Day4, 1, "Deadlift (Barbell)", "C6272009", "Linear", 0, 0, 0, false)]
    [InlineData(DayNumber.Day4, 2, "Leg Press (Machine)", "C7973E0E", "RepsPerSet", 10, 10, 3, false)]
    [InlineData(DayNumber.Day4, 3, "Single Leg Romanian Deadlift (Dumbbell)", "937292AB", "RepsPerSet", 8, 8, 3, true)]
    [InlineData(DayNumber.Day4, 4, "Copenhagen Plank (Short Lever)", "custom:copenhagen-plank-short-lever", "RepsPerSet", 20, 20, 3, true)]
    [InlineData(DayNumber.Day4, 5, "Dead Bug", "D8911FC4", "RepsPerSet", 8, 8, 3, true)]
    public void Lifting_MatchesTheBrief(DayNumber day, int order, string name, string templateId, string progression, int repMin, int repMax, int sets, bool perSide)
    {
        Spec.Exercises.Should().HaveCount(20, "four days × (main lift + four accessories)");

        var exercise = Spec.Exercises.Single(e => e.AssignedDay == day && e.OrderInDay == order);
        exercise.TemplateName.Should().Be(name);
        exercise.ExternalTemplateId.Should().Be(templateId);
        exercise.ProgressionType.Should().Be(progression);
        exercise.Category.Should().Be(order == 1 ? ExerciseCategory.MainLift : ExerciseCategory.Accessory);
        exercise.IsUnilateral.Should().Be(perSide);

        if (progression == "RepsPerSet")
        {
            exercise.RepRangeMinimum.Should().Be(repMin);
            exercise.RepRangeMaximum.Should().Be(repMax);
            exercise.StartingSets.Should().Be(sets);
            exercise.TargetSets.Should().Be(perSide ? 3 : sets + 2, "a unilateral RepsPerSet exercise caps at 3 sets a side");
        }
    }

    [Theory]
    [InlineData("Bench Press (Barbell)", 100)]
    [InlineData("Squat (Barbell)", 140)]
    [InlineData("Overhead Press (Barbell)", 60)]
    [InlineData("Deadlift (Barbell)", 180)]
    public void MainLifts_TakeTheTrainingMaxesWithAmrap(string name, decimal trainingMax)
    {
        var lift = Spec.Exercises.Single(e => e.TemplateName == name);

        lift.TrainingMaxValue.Should().Be(trainingMax);
        lift.TrainingMaxUnit.Should().Be(WeightUnit.Kilograms);
        lift.UseAmrap.Should().BeTrue();
    }

    [Theory]
    [InlineData("Side Plank")]
    [InlineData("Wall Slide with Lift-Off")]
    [InlineData("Copenhagen Plank (Short Lever)")]
    [InlineData("Dead Bug")]
    public void BodyweightAccessories_StartAtZeroKgSoNoWeightIsPending(string name)
    {
        var exercise = Spec.Exercises.Single(e => e.TemplateName == name);

        exercise.StartingWeight.Should().Be(0m);
        exercise.WeightUnit.Should().Be(WeightUnit.Kilograms);
    }

    [Fact]
    public void LoadedAccessories_LeaveTheWeightToBeConfirmedAfterTheFirstSession()
    {
        Spec.Exercises.Single(e => e.TemplateName == "Romanian Deadlift (Barbell)").StartingWeight.Should().BeNull();
    }

    [Theory]
    [InlineData(DayNumber.Day1, "Single Arm Cable Row", "Face Pull")]
    [InlineData(DayNumber.Day2, "Side Plank", "Cable Core Pallof Press")]
    [InlineData(DayNumber.Day3, "Seal Row (Barbell)", "Wall Slide with Lift-Off")]
    [InlineData(DayNumber.Day4, "Copenhagen Plank (Short Lever)", "Dead Bug")]
    public void Supersets_MatchTheBrief(DayNumber day, string first, string second)
    {
        Spec.Supersets.Should().HaveCount(4);
        Spec.Supersets.Should().Contain(new SupersetSpec(day, first, second));
    }

    [Theory]
    [InlineData("Squat (Barbell)", "Pause the last rep of each set.")]
    [InlineData("Single Arm Landmine Press (Barbell)", "start left")]
    [InlineData("Single Arm Cable Row", "extra set left")]
    [InlineData("Bulgarian Split Squat", "start left, extra set left")]
    [InlineData("Side Plank", "extra set left")]
    [InlineData("Side Plank", "reps are seconds")]
    [InlineData("Copenhagen Plank (Short Lever)", "reps are seconds")]
    [InlineData("Seal Row (Barbell)", "Or Meadows row")]
    public void ExerciseNotes_CarryWhatTheWorkoutCannot(string name, string note)
    {
        Spec.ExerciseNotes.Should().ContainKey(name);
        Spec.ExerciseNotes[name].Should().Contain(note);
    }

    // ---- Time estimate and volume budget: the real services on the seed ------------------------

    /// <summary>
    /// Real <see cref="SessionTimeEstimator"/> with <see cref="RestDefaults"/> (main 180 s, accessory 90 s,
    /// superset 60 s, 40 s work, 10 min warm-up), both sides counted for per-side work, superset partners
    /// sharing a rest. Three of the four days as the brief writes them come out over the 55-minute budget;
    /// the brief's own rule is "flag any day estimated over 55 min", so the seed flags them rather than
    /// trimming the brief. This pins the figures.
    /// </summary>
    [Theory]
    [InlineData(DayNumber.Day1, "Upper A", 60, true)]
    [InlineData(DayNumber.Day2, "Lower A", 62, true)]
    [InlineData(DayNumber.Day3, "Upper B", 48, false)]
    [InlineData(DayNumber.Day4, "Lower B", 62, true)]
    public void LiftingDays_AreEstimatedWithTheRealEstimator_AndFlaggedOverFiftyFive(DayNumber day, string label, int minutes, bool overBudget)
    {
        var estimate = Spec.LiftingDays.Single(d => d.Day == day);

        estimate.Label.Should().Be(label);
        estimate.Estimate.WarmUpMinutes.Should().Be(SessionTimeEstimator.DefaultWarmUpMinutes);
        estimate.Estimate.BudgetMinutes.Should().Be(55);
        estimate.Estimate.TotalMinutes.Should().Be(minutes);
        estimate.Estimate.OverBudget.Should().Be(overBudget);
    }

    [Fact]
    public void Caps_AreTheBriefDefaults_AndAllowancesOneAndOne()
    {
        Spec.Caps.Should().BeSameAs(VolumeCaps.Default);
        Spec.Caps.CapFor(VolumeBucket.Chest).Should().Be(10);
        Spec.Caps.CapFor(VolumeBucket.Back).Should().Be(14);
        Spec.Caps.CapFor(VolumeBucket.Shoulders).Should().Be(10);
        Spec.Caps.CapFor(VolumeBucket.Quads).Should().Be(10);
        Spec.Caps.CapFor(VolumeBucket.Hamstrings).Should().Be(10);
        Spec.Caps.CapFor(VolumeBucket.Glutes).Should().Be(12);
        Spec.Caps.CapFor(VolumeBucket.Core).Should().Be(12);
        Spec.Allowances.Should().Be(new WeeklyAllowances(1, 1));
    }

    /// <summary>
    /// Real <see cref="VolumeBudgetService"/> over the seed's week-1 workout with the production muscle
    /// groups (primary 1.0, each secondary 0.5). The brief's exercises exceed five of its own seven caps;
    /// chest (10) and core (12) sit exactly on theirs. The budget warns, it never blocks, so the seed
    /// ships as written and this pins the warnings so a change in either direction is noticed.
    /// </summary>
    [Fact]
    public void VolumeBudget_WithTheProductionMuscleGroups_ReportsTheBriefsOwnCapOverruns()
    {
        var library = new SeedLibrary();
        var workout = Workout.Create(new UserId("user_seed"), Spec.Name, Spec.Variant, ConfiguredExerciseFactory.Create(library, Spec.Exercises));
        var volume = new VolumeBudgetService(new A2SProgramProvider());

        var sets = volume.HardSetsPerGroup(workout, weekNumber: 1, library.MuscleGroups);
        var warnings = volume.Warnings(sets, Spec.Caps);

        sets[VolumeBucket.Chest].Should().Be(10);
        sets[VolumeBucket.Core].Should().Be(12);
        warnings.Select(w => (w.Bucket, w.Sets, w.Cap)).Should().Equal(
            (VolumeBucket.Back, 33, 14),
            (VolumeBucket.Shoulders, 17, 10),
            (VolumeBucket.Quads, 14, 10),
            (VolumeBucket.Hamstrings, 14, 10),
            (VolumeBucket.Glutes, 17, 12));
    }

    [Fact]
    public void Exercises_AllResolveAgainstTheLibrary_AndBuildAValidWorkout()
    {
        var exercises = ConfiguredExerciseFactory.Create(new SeedLibrary(), Spec.Exercises);
        var workout = Workout.Create(new UserId("user_seed"), Spec.Name, Spec.Variant, exercises);

        workout.Exercises.Should().HaveCount(20);
        workout.GetDaysPerWeek().Should().Be(4);
        foreach (var day in new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
        {
            workout.GetExercisesForDay(day).Should().HaveCount(5);
        }
    }

    // ---- Cardio ----------------------------------------------------------------------------------

    [Fact]
    public void Cardio_IsFourByFour_C25KWeekOne_AndLissThirtyFive()
    {
        Spec.Cardio.Should().HaveCount(3);
        Spec.Cardio[0].Should().Be(new CardioProtocolSpec(CardioProtocolKind.NorwegianFourByFour, null, null, null));
        Spec.Cardio[1].Should().Be(new CardioProtocolSpec(CardioProtocolKind.CouchToFiveK, 1, null, null));
        Spec.Cardio[2].Kind.Should().Be(CardioProtocolKind.Liss);
        Spec.Cardio[2].Minutes.Should().Be(35, "LISS is 30–40 min; the seed picks the middle");
        Spec.Cardio[2].Note.Should().Contain("Fallback for any cardio slot");
    }

    // ---- Mobility --------------------------------------------------------------------------------

    [Fact]
    public void MobilityA_IsTheTenMinuteWarmUp()
    {
        Spec.MobilityA.Name.Should().Be("Mobility A");
        Spec.MobilityA.TargetMinutes.Should().Be(10);
        Spec.MobilityA.Drills.Select(d => d.Name).Should().Equal(
            "90/90 breathing", "Quadruped hip shift", "Open books", "Band walks + glute bridges", "Wall slides with lift-off");
    }

    [Theory]
    [InlineData("90/90 breathing", 2, DoseUnit.Breaths, 5, false, SideBias.None, "Back-to-wall breathing, pelvis tucked, ribs down")]
    [InlineData("Quadruped hip shift", 2, DoseUnit.Breaths, 5, false, SideBias.None, "Wall-supported hip shift")]
    [InlineData("Open books", 2, DoseUnit.Reps, 5, true, SideBias.Left, "Side-on wall rotation in split stance, hips square")]
    [InlineData("Band walks + glute bridges", 2, DoseUnit.Reps, 10, false, SideBias.None, null)]
    [InlineData("Wall slides with lift-off", 2, DoseUnit.Reps, 10, false, SideBias.None, null)]
    public void MobilityA_DrillsMatchTheBrief(string name, int sets, DoseUnit unit, int amount, bool perSide, SideBias extraSet, string? standing)
    {
        var drill = Spec.MobilityA.Drills.Single(d => d.Name == name);

        drill.Sets.Should().Be(sets);
        drill.Dose.Unit.Should().Be(unit);
        drill.Dose.Amount.Should().Be(amount);
        drill.PerSide.Should().Be(perSide);
        drill.ExtraSetSide.Should().Be(extraSet);
        drill.StandingAlternative.Should().Be(standing);
    }

    [Fact]
    public void MobilityB_IsMobilityAThenTheStandaloneExtras_TwentyFiveMinutes()
    {
        Spec.MobilityB.Name.Should().Be("Mobility B");
        Spec.MobilityB.TargetMinutes.Should().Be(25);
        Spec.MobilityB.Drills.Take(5).Should().BeEquivalentTo(Spec.MobilityA.Drills, o => o.WithStrictOrdering());
        Spec.MobilityB.Drills.Skip(5).Select(d => d.Name).Should().Equal(
            "Bird dog", "Side plank", "Push-up plus", "Slow calf raises", "Reverse plank", "Calf + pec stretches");
    }

    [Theory]
    [InlineData("Bird dog", 3, DoseUnit.Reps, 5, true, SideBias.Left, "right-arm / left-leg")]
    [InlineData("Side plank", 3, DoseUnit.Seconds, 30, true, SideBias.Left, "Extra set on the left")]
    [InlineData("Push-up plus", 3, DoseUnit.Reps, 12, false, SideBias.None, "cable serratus punch")]
    [InlineData("Slow calf raises", 3, DoseUnit.Reps, 10, true, SideBias.Left, "left emphasis")]
    [InlineData("Reverse plank", 3, DoseUnit.Seconds, 30, false, SideBias.None, "bench-supported, then floor")]
    [InlineData("Calf + pec stretches", 1, DoseUnit.Seconds, 30, true, SideBias.None, "To finish")]
    public void MobilityB_ExtrasMatchTheBrief(string name, int sets, DoseUnit unit, int amount, bool perSide, SideBias extraSet, string cue)
    {
        var drill = Spec.MobilityB.Drills.Single(d => d.Name == name);

        drill.Sets.Should().Be(sets);
        drill.Dose.Unit.Should().Be(unit);
        drill.Dose.Amount.Should().Be(amount);
        drill.PerSide.Should().Be(perSide);
        drill.ExtraSetSide.Should().Be(extraSet);
        drill.Cues.Should().Contain(cue);
    }

    [Fact]
    public void MobilityB_PushUpPlusHasTheCableSerratusPunchAsItsStandingAlternative()
    {
        Spec.MobilityB.Drills.Single(d => d.Name == "Push-up plus").StandingAlternative.Should().Be("Cable serratus punch");
    }

    [Fact]
    public void MobilityRoutines_BuildValidAggregates()
    {
        var user = new UserId("user_seed");

        var a = MobilityRoutine.Create(user, Spec.MobilityA.Name, Spec.MobilityA.TargetMinutes, Spec.MobilityA.Drills.Select(d => d.ToDomain()));
        var b = MobilityRoutine.Create(user, Spec.MobilityB.Name, Spec.MobilityB.TargetMinutes, Spec.MobilityB.Drills.Select(d => d.ToDomain()));

        a.Drills.Should().HaveCount(5);
        b.Drills.Should().HaveCount(11);
    }

    // ---- Habits table ----------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "Water (532ml bottle)", HabitType.Counter, 6, 4, "bottles", "seed:water")]
    [InlineData(1, "Walks", HabitType.Counter, 2, 1, null, "terran:walks")]
    [InlineData(2, "Morning weigh-in", HabitType.Numeric, 1, 1, "kg", "seed:weigh-in")]
    [InlineData(3, "Mobility A done", HabitType.Checkbox, 1, 1, null, "seed:mobility-a")]
    [InlineData(4, "Creatine (3–5g)", HabitType.Checkbox, 1, 1, null, "terran:creatine")]
    public void Habits_MatchTheBriefTable(int index, string name, HabitType type, decimal target, decimal minimum, string? unit, string externalKey)
    {
        Spec.Habits.Should().HaveCount(5);

        var habit = Spec.Habits[index];
        habit.Name.Should().Be(name);
        habit.Type.Should().Be(type);
        habit.Target.Should().Be(target);
        habit.Minimum.Should().Be(minimum);
        habit.Unit.Should().Be(unit);
        habit.Order.Should().Be(index + 1);
        habit.ExternalKey.Should().Be(externalKey);

        var created = HabitDefinition.Create(new UserId("user_seed"), habit.Name, habit.Type, habit.Target, habit.Minimum, habit.Unit, habit.Order, habit.Icon, habit.ExternalKey);
        created.Target.Should().Be(target);
        created.Minimum.Should().Be(minimum);
    }

    // ---- Phases and checkpoints ------------------------------------------------------------------

    [Fact]
    public void Phases_AreCutThenChristmasMaintenanceThenCut()
    {
        Spec.Phases.Select(p => (p.Type, p.From, p.To)).Should().Equal(
            (PhaseType.Cut, Mon16Nov2026, new DateOnly(2026, 12, 21)),
            (PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3)),
            (PhaseType.Cut, new DateOnly(2027, 1, 4), new DateOnly(2027, 3, 8)));
        Spec.Phases[1].Note.Should().Be("Hold, don't gain. A2S2 continues if training; habits still tracked.");
    }

    [Theory]
    [InlineData(0, 2026, 12, 7, 92, "4 cutting weeks")]
    [InlineData(1, 2026, 12, 21, 91, "Cut pauses after this")]
    [InlineData(2, 2027, 2, 1, 88, null)]
    [InlineData(3, 2027, 3, 8, 85, "Goal")]
    public void Checkpoints_MatchTheBriefTable(int index, int y, int m, int d, decimal targetKg, string? note)
    {
        Spec.Checkpoints.Should().HaveCount(4);

        var checkpoint = Spec.Checkpoints[index];
        checkpoint.Date.Should().Be(new DateOnly(y, m, d));
        checkpoint.TargetKg.Should().Be(targetKg);
        checkpoint.Note.Should().Be(note);
    }

    /// <summary>Review focus 5: 95 kg start, trend 95 on day one → on pace.</summary>
    [Fact]
    public void Checkpoints_OnDayOneAtTheStartWeight_ReadOnPace()
    {
        var checkpoints = Spec.Checkpoints.Select(c => (c.Date, c.TargetKg)).ToList();

        var pace = new TrendService().PaceAgainst(checkpoints, Spec.StartDate, Spec.StartWeightKg, today: Spec.StartDate, trendKg: 95m);

        pace.Pace.Should().Be(Pace.OnPace);
        pace.TargetKgToday.Should().Be(95m);
    }

    [Fact]
    public void StartingLate_DropsPassedCheckpointsAndClampsThePhaseInProgress()
    {
        var spec = CutTo85Nov2026.Build(Inputs(new DateOnly(2026, 12, 10)));

        spec.StartDate.Should().Be(new DateOnly(2026, 12, 14));
        spec.Checkpoints.Select(c => c.Date).Should().Equal(new DateOnly(2026, 12, 21), new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 8));
        spec.Phases[0].From.Should().Be(new DateOnly(2026, 12, 14));
        spec.Phases[0].To.Should().Be(new DateOnly(2026, 12, 21));
    }

    [Fact]
    public void StartingAfterChristmas_KeepsOnlyTheSecondCut()
    {
        var spec = CutTo85Nov2026.Build(Inputs(new DateOnly(2027, 1, 6)));

        spec.StartDate.Should().Be(new DateOnly(2027, 1, 11));
        spec.Phases.Should().ContainSingle().Which.Type.Should().Be(PhaseType.Cut);
        spec.Phases[0].From.Should().Be(new DateOnly(2027, 1, 11));
        spec.Checkpoints.Select(c => c.TargetKg).Should().Equal(88m, 85m);
    }

    // ---- Inputs ----------------------------------------------------------------------------------

    [Fact]
    public void Inputs_AreFourTrainingMaxesAndAStartWeightDefaultingToNinetyFive()
    {
        CutTo85Nov2026.Inputs.Select(i => i.Key).Should().Equal("benchTm", "squatTm", "ohpTm", "deadliftTm", "startWeightKg");
        CutTo85Nov2026.Inputs.Take(4).Should().OnlyContain(i => i.Default == null && i.Minimum == 20m && i.Maximum == 400m && i.Unit == "kg");
        CutTo85Nov2026.Inputs[4].Default.Should().Be(95m);
    }
}
