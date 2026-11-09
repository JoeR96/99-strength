using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.SuggestedPlans;

/// <summary>
/// The "Cut to 85 — Nov 2026" plan from brief § 5, line by line. Pure: given the inputs it returns a
/// <see cref="SuggestedPlanSpec"/>; it touches no repository.
/// </summary>
public static class CutTo85Nov2026
{
    public const string Key = "cut-to-85";
    public const string Name = "Cut to 85 — Nov 2026";
    public const string Description =
        "A2S2 on the four main lifts with three accessories a day, Norwegian 4×4 and C25K/LISS cardio, " +
        "Mobility A as every warm-up, five daily habits and trend-weight checkpoints to 85 kg by 8 March 2027.";

    public const decimal DefaultStartWeightKg = 95m;
    public const decimal MinimumTrainingMaxKg = 20m;
    public const decimal MaximumTrainingMaxKg = 400m;

    public const string MobilityAName = "Mobility A";
    public const string MobilityBName = "Mobility B";

    // Hevy exercise template ids (src/A2S.Web/src/data/hevyExercises.ts). "custom:" ids are placeholders
    // for exercises Hevy has no template for; Phase 6's template catalogue creates the real ones.
    private const string BenchPressId = "79D0BB3A";
    private const string ChestSupportedRowId = "914F3A96";
    private const string LandminePressId = "1837BA23";
    private const string SingleArmCableRowId = "D0C4A899";
    private const string FacePullId = "BE640BA0";
    private const string SquatId = "D04AC939";
    private const string RomanianDeadliftId = "2B4B7310";
    private const string BulgarianSplitSquatId = "B5D3A742";
    private const string SidePlankId = "E3EDA509";
    private const string PallofPressId = "CC55119B"; // Hevy spells it "Cable Core Palloff Press"
    private const string OverheadPressId = "7B8D84E8";
    private const string LatPulldownId = "6A6C31A5";
    private const string InclineDumbbellPressId = "07B38369";
    private const string SealRowId = "custom:seal-row-barbell";
    private const string WallSlideId = "custom:wall-slide-with-lift-off";
    private const string DeadliftId = "C6272009";
    private const string LegPressId = "C7973E0E";
    private const string SingleLegRdlId = "937292AB";
    private const string CopenhagenPlankId = "custom:copenhagen-plank-short-lever";
    private const string DeadBugId = "D8911FC4";

    public static readonly IReadOnlyList<SuggestedPlanInputSpec> Inputs =
    [
        new("benchTm", "Bench press training max", "kg", null, MinimumTrainingMaxKg, MaximumTrainingMaxKg),
        new("squatTm", "Squat training max", "kg", null, MinimumTrainingMaxKg, MaximumTrainingMaxKg),
        new("ohpTm", "Overhead press training max", "kg", null, MinimumTrainingMaxKg, MaximumTrainingMaxKg),
        new("deadliftTm", "Deadlift training max", "kg", null, MinimumTrainingMaxKg, MaximumTrainingMaxKg),
        new("startWeightKg", "Start weight", "kg", DefaultStartWeightKg, TrainingPlan.MinimumStartWeightKg, TrainingPlan.MaximumStartWeightKg)
    ];

    public static SuggestedPlanSpec Build(SuggestedPlanInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var startDate = LocalDates.ComingMonday(inputs.Today);
        var exercises = Exercises(inputs);
        var supersets = Supersets();

        return new SuggestedPlanSpec(
            Key,
            Name,
            Description,
            startDate,
            inputs.StartWeightKg,
            ProgramVariant.FourDay,
            exercises,
            ExerciseNotes(),
            supersets,
            LiftingDays(exercises, supersets),
            Schedule(),
            MobilityA(),
            MobilityB(),
            Cardio(),
            Habits(),
            Phases(startDate),
            Checkpoints(startDate),
            VolumeCaps.Default,
            new WeeklyAllowances(flexibleMeals: 1, alcoholOccasions: 1));
    }

    // ---- Weekly schedule (brief § 5 table) ---------------------------------------------------------

    private static IReadOnlyList<ScheduledSessionSpec> Schedule() =>
    [
        new(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A"),
        new(DayOfWeek.Tuesday, SessionKind.Lift, DayNumber.Day2, null, null, "Lower A"),
        new(DayOfWeek.Wednesday, SessionKind.Cardio, null, CardioProtocolKind.NorwegianFourByFour, null, "Norwegian 4×4 — outdoors, hills"),
        new(DayOfWeek.Wednesday, SessionKind.Mobility, null, null, MobilityBName, "Mobility B (25 min)"),
        new(DayOfWeek.Thursday, SessionKind.Lift, DayNumber.Day3, null, null, "Upper B"),
        new(DayOfWeek.Friday, SessionKind.Lift, DayNumber.Day4, null, null, "Lower B"),
        // Open cardio slot: either a C25K run or LISS counts (brief: "C25K run or LISS 30–40 min").
        new(DayOfWeek.Saturday, SessionKind.Cardio, null, null, null, "C25K run or LISS 30–40 min"),
        new(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest — walk only")
    ];

    // ---- Lifting (brief § 5 "Lifting") -----------------------------------------------------------

    private static IReadOnlyList<CreateExerciseRequest> Exercises(SuggestedPlanInputs inputs) =>
    [
        // Upper A
        MainLift("Bench Press (Barbell)", BenchPressId, DayNumber.Day1, inputs.BenchTm),
        Accessory("Chest Supported Incline Row (Dumbbell)", ChestSupportedRowId, DayNumber.Day1, 2, 8, 10),
        Accessory("Single Arm Landmine Press (Barbell)", LandminePressId, DayNumber.Day1, 3, 8, 8, perSide: true),
        Accessory("Single Arm Cable Row", SingleArmCableRowId, DayNumber.Day1, 4, 10, 10, perSide: true),
        Accessory("Face Pull", FacePullId, DayNumber.Day1, 5, 15, 15),

        // Lower A
        MainLift("Squat (Barbell)", SquatId, DayNumber.Day2, inputs.SquatTm),
        Accessory("Romanian Deadlift (Barbell)", RomanianDeadliftId, DayNumber.Day2, 2, 8, 8),
        Accessory("Bulgarian Split Squat", BulgarianSplitSquatId, DayNumber.Day2, 3, 8, 8, perSide: true),
        Accessory("Side Plank", SidePlankId, DayNumber.Day2, 4, 30, 30, perSide: true, bodyweight: true),
        Accessory("Cable Core Pallof Press", PallofPressId, DayNumber.Day2, 5, 10, 10, perSide: true),

        // Upper B
        MainLift("Overhead Press (Barbell)", OverheadPressId, DayNumber.Day3, inputs.OhpTm),
        Accessory("Lat Pulldown (Cable)", LatPulldownId, DayNumber.Day3, 2, 8, 10),
        Accessory("Incline Bench Press (Dumbbell)", InclineDumbbellPressId, DayNumber.Day3, 3, 10, 10),
        Accessory("Seal Row (Barbell)", SealRowId, DayNumber.Day3, 4, 10, 10),
        Accessory("Wall Slide with Lift-Off", WallSlideId, DayNumber.Day3, 5, 10, 10, sets: 2, bodyweight: true),

        // Lower B
        MainLift("Deadlift (Barbell)", DeadliftId, DayNumber.Day4, inputs.DeadliftTm),
        Accessory("Leg Press (Machine)", LegPressId, DayNumber.Day4, 2, 10, 10),
        Accessory("Single Leg Romanian Deadlift (Dumbbell)", SingleLegRdlId, DayNumber.Day4, 3, 8, 8, perSide: true),
        Accessory("Copenhagen Plank (Short Lever)", CopenhagenPlankId, DayNumber.Day4, 4, 20, 20, perSide: true, bodyweight: true),
        Accessory("Dead Bug", DeadBugId, DayNumber.Day4, 5, 8, 8, perSide: true, bodyweight: true)
    ];

    /// <summary>
    /// What the brief says beyond sets × reps and the Workout cannot hold: side bias, pauses, timed
    /// holds and the superset partner. Keyed by template name. Public so the Hevy routine push can
    /// carry them when the plan was started from this spec.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ExerciseNotes() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Single Arm Landmine Press (Barbell)"] = "Half-kneeling. 8 per side, start left.",
        ["Single Arm Cable Row"] = "10 per side, extra set left. Superset with face pulls.",
        ["Face Pull"] = "Superset with single-arm cable row.",
        ["Squat (Barbell)"] = "Pause the last rep of each set.",
        ["Bulgarian Split Squat"] = "Rear-foot elevated. 8 per side, start left, extra set left.",
        ["Side Plank"] = "30 s per side (reps are seconds), extra set left. Superset with Pallof press.",
        ["Cable Core Pallof Press"] = "10 per side. Superset with side plank.",
        ["Seal Row (Barbell)"] = "Or Meadows row. Superset with wall slides.",
        ["Wall Slide with Lift-Off"] = "Superset with seal row.",
        ["Single Leg Romanian Deadlift (Dumbbell)"] = "8 per side.",
        ["Copenhagen Plank (Short Lever)"] = "20 s per side (reps are seconds). Superset with dead bug.",
        ["Dead Bug"] = "8 per side. Superset with Copenhagen plank."
    };

    public static IReadOnlyList<SupersetSpec> Supersets() =>
    [
        new(DayNumber.Day1, "Single Arm Cable Row", "Face Pull"),
        new(DayNumber.Day2, "Side Plank", "Cable Core Pallof Press"),
        new(DayNumber.Day3, "Seal Row (Barbell)", "Wall Slide with Lift-Off"),
        new(DayNumber.Day4, "Copenhagen Plank (Short Lever)", "Dead Bug")
    ];

    /// <summary>
    /// Week-1 time estimate per lifting day with <see cref="RestDefaults"/>: main lifts take the
    /// program table's set count, accessories their starting sets (both sides for per-side work),
    /// and superset partners share one rest per round.
    /// </summary>
    private static IReadOnlyList<LiftingDaySpec> LiftingDays(IReadOnlyList<CreateExerciseRequest> exercises, IReadOnlyList<SupersetSpec> supersets)
    {
        var mainLiftSets = A2SHypertrophyProgram.GetWeekData(1, ProgramTier.Primary).Sets;
        var labels = new Dictionary<DayNumber, string>
        {
            [DayNumber.Day1] = "Upper A",
            [DayNumber.Day2] = "Lower A",
            [DayNumber.Day3] = "Upper B",
            [DayNumber.Day4] = "Lower B"
        };

        return labels.Select(pair =>
        {
            var timings = exercises
                .Where(e => e.AssignedDay == pair.Key)
                .OrderBy(e => e.OrderInDay)
                .Select(e =>
                {
                    var group = supersets.Select((s, i) => (s, i))
                        .Where(x => x.s.Day == pair.Key && (x.s.First == e.TemplateName || x.s.Second == e.TemplateName))
                        .Select(x => (int?)x.i)
                        .FirstOrDefault();

                    var sets = e.ProgressionType == "Linear" ? mainLiftSets : e.StartingSets ?? 3;
                    if (e.IsUnilateral)
                    {
                        sets *= 2;
                    }

                    var rest = e.Category == ExerciseCategory.MainLift
                        ? RestDefaults.MainLiftSeconds
                        : group is null ? RestDefaults.AccessorySeconds : RestDefaults.SupersetSeconds;

                    return new ExerciseTiming(e.TemplateName, sets, rest, group);
                });

            return new LiftingDaySpec(pair.Key, pair.Value, SessionTimeEstimator.Estimate(timings));
        }).ToList();
    }

    private static CreateExerciseRequest MainLift(string name, string templateId, DayNumber day, decimal trainingMax) => new()
    {
        TemplateName = name,
        ExternalTemplateId = templateId,
        Category = ExerciseCategory.MainLift,
        ProgressionType = "Linear",
        AssignedDay = day,
        OrderInDay = 1,
        TrainingMaxValue = trainingMax,
        TrainingMaxUnit = WeightUnit.Kilograms,
        UseAmrap = true
    };

    /// <summary>
    /// RepsPerSet accessory. Per-side work is unilateral and capped at 3 sets a side (the strategy's
    /// own ceiling); bodyweight work starts at 0 kg so there is no weight to confirm after session one.
    /// </summary>
    private static CreateExerciseRequest Accessory(
        string name,
        string templateId,
        DayNumber day,
        int order,
        int repMin,
        int repMax,
        int sets = 3,
        bool perSide = false,
        bool bodyweight = false) => new()
    {
        TemplateName = name,
        ExternalTemplateId = templateId,
        Category = ExerciseCategory.Accessory,
        ProgressionType = "RepsPerSet",
        AssignedDay = day,
        OrderInDay = order,
        RepRangeMinimum = repMin,
        RepRangeMaximum = repMax,
        StartingSets = sets,
        TargetSets = perSide ? Math.Min(sets, 3) : sets + 2,
        IsUnilateral = perSide,
        StartingWeight = bodyweight ? 0m : null,
        WeightUnit = bodyweight ? Domain.Enums.WeightUnit.Kilograms : null
    };

    // ---- Mobility (brief § 5 "Mobility") ---------------------------------------------------------

    private static MobilityRoutineSpec MobilityA() => new(MobilityAName, 10, MobilityADrills());

    private static IReadOnlyList<MobilityDrillDto> MobilityADrills() =>
    [
        Drill("90/90 breathing", 2, DrillDose.Breaths(5), standing: "Back-to-wall breathing, pelvis tucked, ribs down"),
        Drill("Quadruped hip shift", 2, DrillDose.Breaths(5), standing: "Wall-supported hip shift"),
        Drill("Open books", 2, DrillDose.Reps(5), perSide: true, extraSet: SideBias.Left,
            cues: "Extra set opening the left",
            standing: "Side-on wall rotation in split stance, hips square"),
        Drill("Band walks + glute bridges", 2, DrillDose.Reps(10), cues: "10 of each"),
        Drill("Wall slides with lift-off", 2, DrillDose.Reps(10))
    ];

    /// <summary>Mobility A, then the standalone extras. 25 minutes; Wednesday and any rest day.</summary>
    private static MobilityRoutineSpec MobilityB() => new(MobilityBName, 25,
    [
        .. MobilityADrills(),
        Drill("Bird dog", 3, DrillDose.Reps(5), perSide: true, extraSet: SideBias.Left,
            cues: "Extra set on the right-arm / left-leg pair"),
        Drill("Side plank", 3, DrillDose.Seconds(30), perSide: true, extraSet: SideBias.Left,
            cues: "Extra set on the left"),
        Drill("Push-up plus", 3, DrillDose.Reps(12),
            cues: "Or cable serratus punch",
            standing: "Cable serratus punch"),
        Drill("Slow calf raises", 3, DrillDose.Reps(10), perSide: true, extraSet: SideBias.Left,
            cues: "Slow, left emphasis"),
        Drill("Reverse plank", 3, DrillDose.Seconds(30),
            cues: "Regression → progression: bench-supported, then floor; 15–30 s"),
        Drill("Calf + pec stretches", 1, DrillDose.Seconds(30), perSide: true,
            cues: "To finish")
    ]);

    private static MobilityDrillDto Drill(
        string name,
        int sets,
        DrillDose dose,
        bool perSide = false,
        SideBias extraSet = SideBias.None,
        string? cues = null,
        string? standing = null) =>
        new(name, sets, new DrillDoseDto(dose.Unit, dose.Amount), extraSet, cues, standing, perSide);

    // ---- Cardio (brief § 5 "Cardio") -------------------------------------------------------------

    private static IReadOnlyList<CardioProtocolSpec> Cardio() =>
    [
        new(CardioProtocolKind.NorwegianFourByFour, null, null, null),
        new(CardioProtocolKind.CouchToFiveK, CardioProtocol.CouchToFiveKFirstWeek, null, null),
        new(CardioProtocolKind.Liss, null, 35, "Incline walk at conversational pace. Fallback for any cardio slot (30–40 min).")
    ];

    // ---- Habits (brief § 5 "Habits") -------------------------------------------------------------

    /// <summary>
    /// Walks and creatine keep Terran CC's keys so an import and the seed land on one habit each.
    /// The weigh-in is Numeric with target/minimum 1: any logged weight counts as "logged".
    /// </summary>
    private static IReadOnlyList<HabitSpec> Habits() =>
    [
        new("Water (532ml bottle)", HabitType.Counter, 6, 4, "bottles", 1, "droplets", "seed:water"),
        new("Walks", HabitType.Counter, 2, 1, null, 2, "footprints", "terran:walks"),
        new("Morning weigh-in", HabitType.Numeric, 1, 1, "kg", 3, "scale", "seed:weigh-in"),
        new("Mobility A done", HabitType.Checkbox, 1, 1, null, 4, "stretch-horizontal", "seed:mobility-a"),
        new("Creatine (3–5g)", HabitType.Checkbox, 1, 1, null, 5, "pill", "terran:creatine")
    ];

    // ---- Phases and checkpoints (brief § 5 "Checkpoints") ----------------------------------------

    private static readonly DateOnly CutOneEnds = new(2026, 12, 21);
    private static readonly DateOnly MaintenanceFrom = new(2026, 12, 22);
    private static readonly DateOnly MaintenanceTo = new(2027, 1, 3);
    private static readonly DateOnly CutTwoFrom = new(2027, 1, 4);
    private static readonly DateOnly Goal = new(2027, 3, 8);

    /// <summary>Phases that end before the start are dropped; a phase in progress starts on the start date.</summary>
    private static IReadOnlyList<PlanPhaseDto> Phases(DateOnly startDate)
    {
        PlanPhaseDto[] all =
        [
            new(PhaseType.Cut, startDate, CutOneEnds, "4 cutting weeks to 92 kg, then 91 kg. Cut pauses after this."),
            new(PhaseType.Maintenance, MaintenanceFrom, MaintenanceTo, "Hold, don't gain. A2S2 continues if training; habits still tracked."),
            new(PhaseType.Cut, CutTwoFrom, Goal, "Cut to 85 kg by 8 March 2027.")
        ];

        return all
            .Where(p => p.To >= startDate)
            .Select(p => p.From < startDate ? p with { From = startDate } : p)
            .ToList();
    }

    /// <summary>Checkpoints on or before the start date are dropped: the pace line starts at the start weight.</summary>
    private static IReadOnlyList<CheckpointDto> Checkpoints(DateOnly startDate)
    {
        CheckpointDto[] all =
        [
            new(new DateOnly(2026, 12, 7), 92m, "4 cutting weeks"),
            new(CutOneEnds, 91m, "Cut pauses after this"),
            new(new DateOnly(2027, 2, 1), 88m, null),
            new(Goal, 85m, "Goal")
        ];

        return all.Where(c => c.Date > startDate).ToList();
    }
}
