using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.Services;

namespace A2S.Application.SuggestedPlans;

/// <summary>
/// "Plan v3 — Base/Build/Transition": 26 weeks of cut, cardio and mobility (Base weeks 1–8 and Build
/// 9–16 cutting, Transition 17–26 at maintenance). Four ~60-minute gym sessions (warm-up → A2S2 main
/// lift → two supersets → core → stair finisher or a 10-minute treadmill cool-down), walking on the
/// Walk builder track and three home routines in evening slots. A2S2 runs the standard 21-week block
/// sequence inside the 26 weeks. Starts with weights pending: training maxes are optional and
/// accessory starting weights are confirmed after the first session. Unilateral work is left side first.
/// Pure: returns data, touches no repository.
/// </summary>
public sealed class PlanV3BaseBuildTransition : IPlanTemplate
{
    public const string TemplateKey = "plan-v3";
    public const string TemplateName = "Plan v3 — Base/Build/Transition";

    public const string WarmUpUpper = "Warm-up (upper)";
    public const string WarmUpLower = "Warm-up (lower)";
    public const string HipsAndLowerBack = "Hips & lower back";
    public const string UpperBackAndChest = "Upper back & chest";
    public const string FullBodyFlow = "Full-body flow";

    public const int ProgrammeWeeks = 26;

    public string Key => TemplateKey;
    public string Name => TemplateName;
    public string Description =>
        "26 weeks: Base (1–8) and Build (9–16) cutting, Transition (17–26) at maintenance. Four gym sessions " +
        "(bench, pause squat, incline Smith, front squat on A2S2) with supersets, stair finishers and treadmill " +
        "cool-downs, the Walk builder track and three home mobility routines. Weights are filled in later.";
    public decimal DefaultStartWeightKg => 95m;

    public IReadOnlyList<SuggestedPlanInputSpec> Inputs =>
    [
        new("benchTm", "Bench press training max", "kg", null, CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg, Optional: true),
        new("pauseSquatTm", "Pause squat training max", "kg", null, CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg, Optional: true),
        new("inclineSmithTm", "Incline Smith press training max", "kg", null, CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg, Optional: true),
        new("frontSquatTm", "Front squat training max", "kg", null, CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg, Optional: true),
        new("startWeightKg", "Start weight", "kg", 95m, TrainingPlan.MinimumStartWeightKg, TrainingPlan.MaximumStartWeightKg)
    ];

    public SuggestedPlanSpec Build(PlanTemplateInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var start = inputs.StartDate;
        var exercises = Exercises(inputs);

        return new SuggestedPlanSpec(
            Key,
            Name,
            Description,
            start,
            inputs.StartWeightKg,
            ProgramVariant.FourDay,
            exercises,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            [],
            [],
            Schedule(),
            WarmUpUpperRoutine(),
            WarmUpLowerRoutine(),
            [],
            [],
            Phases(start),
            [new CheckpointDto(start.AddDays(16 * 7 - 1), 85m, "End of Build: about 85 kg")],
            VolumeCaps.Default,
            new WeeklyAllowances(flexibleMeals: 1, alcoholOccasions: 1),
            ExtraRoutines: [HipsRoutine(), UpperBackRoutine(), FlowRoutine()],
            CardioTracks: [CardioTrackTemplates.WalkBuilder, CardioTrackTemplates.StairFinisher, CardioTrackTemplates.WalkToRun],
            BlockSequence: [1, 2, 3]);
    }

    // ---- Week ------------------------------------------------------------------------------------

    private static readonly FinisherSpec Stairs = new(TrackKey: CardioTrackTemplates.StairFinisher);
    private static readonly FinisherSpec CoolDown = new(Fixed: FixedActivity.EasyInclineTreadmill(10));

    private static IReadOnlyList<ScheduledSessionSpec> Schedule() =>
    [
        new(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A", WarmUpUpper, Stairs),
        new(DayOfWeek.Monday, SessionKind.Mobility, null, null, HipsAndLowerBack, "Hips & lower back (15 min)"),
        new(DayOfWeek.Tuesday, SessionKind.Lift, DayNumber.Day2, null, null, "Lower A", WarmUpLower, CoolDown),
        new(DayOfWeek.Tuesday, SessionKind.Mobility, null, null, UpperBackAndChest, "Upper back & chest (15 min)"),
        new(DayOfWeek.Wednesday, SessionKind.Cardio, null, null, null, "Walk builder, 30–45 min", CardioTrack: CardioTrackTemplates.WalkBuilder),
        new(DayOfWeek.Wednesday, SessionKind.Mobility, null, null, FullBodyFlow, "Full-body flow (20 min)"),
        new(DayOfWeek.Thursday, SessionKind.Lift, DayNumber.Day3, null, null, "Upper B", WarmUpUpper, Stairs),
        new(DayOfWeek.Thursday, SessionKind.Mobility, null, null, HipsAndLowerBack, "Hips & lower back (15 min)"),
        new(DayOfWeek.Friday, SessionKind.Lift, DayNumber.Day4, null, null, "Lower B", WarmUpLower, CoolDown),
        new(DayOfWeek.Friday, SessionKind.Mobility, null, null, UpperBackAndChest, "Upper back & chest (15 min)"),
        new(DayOfWeek.Saturday, SessionKind.Cardio, null, null, null, "Long walk outdoors, 45–75 min", CardioTrack: CardioTrackTemplates.WalkBuilder),
        new(DayOfWeek.Saturday, SessionKind.Mobility, null, null, FullBodyFlow, "Full-body flow (20 min)"),
        new(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest — daily steps")
    ];

    private static IReadOnlyList<PlanPhaseDto> Phases(DateOnly start) =>
    [
        new(PhaseType.Cut, start, start.AddDays(8 * 7 - 1),
            "Base: learn the sessions, pause squat for pattern confidence, walking and continuous stair finishers, level 1 home routines.",
            Label: "Base"),
        new(PhaseType.Cut, start.AddDays(8 * 7), start.AddDays(16 * 7 - 1),
            "Build: cut to about 85 kg, incline walking, stair intervals, level 2 home routines.",
            Label: "Build",
            Changes:
            [
                "Stair finisher: move on to 1 min hard / 1 min easy intervals when the track suggests it",
                "Walk builder: incline walking",
                "Home routines: move to the harder versions where the cues held, and add 15 s to holds"
            ]),
        new(PhaseType.Maintenance, start.AddDays(16 * 7), start.AddDays(ProgrammeWeeks * 7 - 1),
            "Transition: eat at maintenance; back squat becomes the main lift; start walk → run once the running gate is passed.",
            Label: "Transition",
            Changes:
            [
                "Eat at maintenance",
                "Back squat replaces the pause squat as Lower A's main lift",
                "Walk → run starts once the running gate is passed",
                "Home routines: hold the harder versions; add a second round of the warm-up flow"
            ],
            Swaps: [new PhaseSwapDto("Pause Squat (Barbell)", "Squat (Barbell)")])
    ];

    // ---- Gym sessions ----------------------------------------------------------------------------

    private static IReadOnlyList<CreateExerciseRequest> Exercises(PlanTemplateInputs inputs) =>
    [
        // Upper A: bench
        MainLift("Bench Press (Barbell)", "79D0BB3A", DayNumber.Day1, ProgramTier.Primary, inputs.Get("benchTm")),
        Accessory("Bent Over Row (Barbell)", "55E6546F", DayNumber.Day1, 2, 3, 8, 10, group: 2),
        Accessory("Single Arm Lateral Raise (Cable)", "DE68C825", DayNumber.Day1, 3, 2, 12, 15, group: 2, perSide: true),
        Accessory("Single Arm Cable Row", "D0C4A899", DayNumber.Day1, 4, 2, 10, 12, group: 3, perSide: true),
        Accessory("Skullcrusher (Dumbbell)", "68F8A292", DayNumber.Day1, 5, 2, 10, 12, group: 3),
        Accessory("Cable Core Palloff Press", "CC55119B", DayNumber.Day1, 6, 2, 10, 10, perSide: true),

        // Lower A: squat pattern
        MainLift("Pause Squat (Barbell)", "CE1054CE", DayNumber.Day2, ProgramTier.Primary, inputs.Get("pauseSquatTm")),
        Accessory("Romanian Deadlift (Dumbbell)", "72CFFAD5", DayNumber.Day2, 2, 3, 8, 10),
        Accessory("Split Squat (Dumbbell)", "20C1A3CB", DayNumber.Day2, 3, 2, 8, 10, group: 3, perSide: true),
        Accessory("Hip Abduction (Machine)", "F4B4C6EE", DayNumber.Day2, 4, 2, 12, 15, group: 3),

        // Upper B: incline (A2S2 auxiliary scheme as the main lift)
        MainLift("Incline Bench Press (Smith Machine)", "3A6FA3D1", DayNumber.Day3, ProgramTier.Auxiliary, inputs.Get("inclineSmithTm")),
        Accessory("Lat Pulldown Neutral Grip", "cd39c85d-7449-43b7-8722-417f5fc97e8b", DayNumber.Day3, 2, 3, 8, 12, group: 2),
        Accessory("Face Pull", "BE640BA0", DayNumber.Day3, 3, 2, 12, 15, group: 2),
        Accessory("Rope Cable Curl", "234897AB", DayNumber.Day3, 4, 2, 10, 15, group: 3),
        Accessory("Triceps Pushdown", "93A552C6", DayNumber.Day3, 5, 2, 12, 15, group: 3),
        Accessory("Cable Twist (Down to up)", "92D858EA", DayNumber.Day3, 6, 2, 10, 10, perSide: true),

        // Lower B: front squat (A2S2 auxiliary scheme)
        MainLift("Front Squat", "5046D0A9", DayNumber.Day4, ProgramTier.Auxiliary, inputs.Get("frontSquatTm")),
        Accessory("Leg Press (Machine)", "C7973E0E", DayNumber.Day4, 2, 3, 10, 12),
        Accessory("Hip Thrust (Machine)", "68CE0B9B", DayNumber.Day4, 3, 2, 10, 12, group: 3),
        Accessory("Hip Adduction (Machine)", "8BEBFED6", DayNumber.Day4, 4, 2, 12, 15, group: 3),
        Accessory("Seated Leg Curl (Machine)", "11A123F3", DayNumber.Day4, 5, 2, 10, 12)
    ];

    /// <summary>A2S2 main lift; no training max given = pending (set before its first session).</summary>
    private static CreateExerciseRequest MainLift(string name, string templateId, DayNumber day, ProgramTier tier, decimal? trainingMax) => new()
    {
        TemplateName = name,
        ExternalTemplateId = templateId,
        Category = ExerciseCategory.MainLift,
        ProgressionType = "Linear",
        AssignedDay = day,
        OrderInDay = 1,
        TrainingMaxValue = trainingMax,
        TrainingMaxUnit = WeightUnit.Kilograms,
        TrainingMaxPending = trainingMax is null,
        Tier = tier,
        UseAmrap = true
    };

    /// <summary>
    /// Reps-per-set accessory with no starting weight (confirmed after the first session). Per-side work
    /// is unilateral, capped at the strategy's 3 sets a side.
    /// </summary>
    private static CreateExerciseRequest Accessory(
        string name, string templateId, DayNumber day, int order, int sets, int repMin, int repMax, int? group = null, bool perSide = false) => new()
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
        TargetSets = perSide ? 3 : sets + 2,
        IsUnilateral = perSide,
        SupersetGroup = group
    };

    // ---- Warm-ups (8 min, one set of each) -------------------------------------------------------

    private static MobilityRoutineSpec WarmUpUpperRoutine() => new(WarmUpUpper, 8,
    [
        Drill("90/90 breathing, feet on a bench", 1, DrillDose.Breaths(5),
            "Exhale fully until the ribs drop; tongue on the roof of your mouth, jaw loose"),
        Drill("Open books", 1, DrillDose.Reps(5), "Left side first. Knees stacked and pinned; rotate from the upper back, not the lower", perSide: true),
        Drill("Wall slide with lift-off", 1, DrillDose.Reps(8), "Lower back stays on the wall; lift the hands off at the top"),
        Drill("Band pull-apart", 1, DrillDose.Reps(15), "Arms straight, shoulders down away from the ears")
    ]);

    private static MobilityRoutineSpec WarmUpLowerRoutine() => new(WarmUpLower, 8,
    [
        Drill("90/90 breathing, feet on a bench", 1, DrillDose.Breaths(5),
            "Exhale fully until the ribs drop; tongue on the roof of your mouth, jaw loose"),
        Drill("Quadruped hip shift", 1, DrillDose.Breaths(5), "Sit the hips back and left without the lower back rounding"),
        Drill("Glute bridge + mini-band walk", 1, DrillDose.Reps(10),
            "10 bridges + 10 steps each way. Ribs down; feel it in the side of the hip, not the lower back"),
        Drill("Bodyweight pause squat (3 s hold)", 1, DrillDose.Reps(5), "Spread the floor with your feet; same depth and hip path every rep")
    ]);

    // ---- Home routines (easier/harder in the cues until drill levels exist) -----------------------

    private static MobilityRoutineSpec HipsRoutine() => new(HipsAndLowerBack, 15,
    [
        Drill("90/90 breathing, feet on a wall or sofa", 1, DrillDose.Breaths(6),
            Cue("Flatten the lower back gently; exhale until the ribs drop; feel the hamstrings switch on lightly",
                "Feet on the floor, knees bent", "Hold a balloon or foam roller between the knees")),
        Drill("Half-kneeling hip flexor stretch", 2, DrillDose.Seconds(30),
            Cue("Left side first. Tuck the tail under first; squeeze the back glute; only then shift forward a few centimetres",
                "Back knee on a cushion, smaller forward shift", "Reach the same-side arm overhead and lean slightly away"), perSide: true),
        Drill("90/90 hip switches", 1, DrillDose.Reps(6),
            Cue("Sit tall, hands behind you; rotate both knees side to side; move from the hips, not the lower back",
                "Hands supporting, small range", "Hands off the floor, then lift the back knee at each end"), perSide: true),
        Drill("Side-lying clamshell", 2, DrillDose.Reps(12),
            Cue("Left side first. Heels together, hips stacked; open the top knee without rolling back; feel it in the side of the hip",
                "Smaller range", "Mini band above the knees, or in a side plank on the knees"), perSide: true),
        Drill("Child's pose with side reach", 1, DrillDose.Seconds(30),
            Cue("Left side first. Walk both hands to one side; sink the hips back; breathe into the opposite ribs",
                "Cushion under the hips", "Cross the far wrist over the near one for more lat"), perSide: true),
        Drill("Figure-4 on your back", 1, DrillDose.Seconds(30),
            Cue("Ankle on the opposite knee; draw the thigh toward you; keep the tailbone down",
                "Foot against a wall instead of pulling", "Seated figure-4 with a tall spine"), perSide: true),
        Drill("Side plank", 2, DrillDose.Seconds(20),
            Cue("20–30 s. Left side first. Elbow under shoulder; hips pushed forward in one straight line; bottom-side waist lifted off the floor",
                "Knees bent and down", "Lift the top leg, or reach the top arm up"), perSide: true),
        Drill("Copenhagen plank, short lever", 2, DrillDose.Seconds(15),
            Cue("15–20 s. Top knee on a sofa or bench edge; squeeze it down to lift the hips; body stays in one line",
                "Bottom foot lightly touching the floor", "Ankle on the edge instead of the knee (long lever)"), perSide: true)
    ]);

    private static MobilityRoutineSpec UpperBackRoutine() => new(UpperBackAndChest, 15,
    [
        Drill("Cat-cow", 1, DrillDose.Reps(8),
            Cue("Slow reps. Move one vertebra at a time; exhale to round, inhale to lift the chest; keep the elbows straight",
                "Smaller range", "Hold each end for 2 breaths")),
        Drill("Thread the needle", 1, DrillDose.Reps(5),
            Cue("5 reps + 20 s hold. Hips over knees; slide the arm through and rest the shoulder down; look under the arm",
                "Stay on the forearm", "Top arm reaches up and over the head on the hold"), perSide: true),
        Drill("Open book", 1, DrillDose.Reps(5),
            Cue("Left side first. Knees stacked and pinned with the bottom hand; let the top arm open toward the floor; breathe out at the end",
                "Top arm bent, hand on the chest", "Pause 3 s at the open end, reaching longer"), perSide: true),
        Drill("Doorway chest stretch", 2, DrillDose.Seconds(30),
            Cue("One set at each angle. Elbow at shoulder height, then higher; step through gently; shoulder stays down and back",
                "Smaller step through", "Rotate the chest away from the arm"), perSide: true),
        Drill("Puppy pose (upper-back extension)", 1, DrillDose.Breaths(6),
            Cue("Hips over knees, walk the hands forward; melt the chest down; ribs stay in, not flaring",
                "Forearms on the floor", "Hands on a block or book, chest lower")),
        Drill("Reverse plank ladder", 3, DrillDose.Seconds(15),
            "At your level, 15 → 30 s. Hands under shoulders, fingers toward your feet; press the floor away; hips level, chest open. " +
            "Level 1: reverse tabletop, knees bent. Level 2: hands on a bench or sofa edge, legs straight. Level 3: full reverse plank on the floor. " +
            "Add 5 s per set after 2 clean sessions; move up a level from the top of the range."),
        Drill("Dead bug", 2, DrillDose.Reps(6),
            Cue("6–8 each side. Lower back pressed gently into the floor; exhale as the arm and leg reach; stop before the back lifts",
                "Move only the legs, heels tapping", "Hold a light weight or squeeze a ball between hand and knee"), perSide: true)
    ]);

    private static MobilityRoutineSpec FlowRoutine() => new(FullBodyFlow, 20,
    [
        Drill("Warm-up flow", 1, DrillDose.Reps(5),
            Cue("5 slow rounds: reach up → fold → half lift → plank → knees down → cobra → downward dog. One breath per move; soft knees in the fold; in downward dog push the hips back rather than the heels down",
                "Hands on a chair; skip the plank", "Low plank instead of knees down; hold downward dog for 5 breaths")),
        Drill("Low lunge with overhead reach", 1, DrillDose.Breaths(5),
            Cue("Left side first. Back knee down, tail tucked; reach up and slightly away from the front leg; ribs down",
                "Hands on the front thigh", "Lift the back knee (high lunge)"), perSide: true),
        Drill("Warrior 2 → reverse warrior", 1, DrillDose.Breaths(5),
            Cue("5 breaths in each pose. Front knee tracks over the middle toes; hips open to the side; slide the back hand down the back leg on the reverse",
                "Shorter stance", "Deeper front knee, longer stance"), perSide: true),
        Drill("Lizard", 1, DrillDose.Breaths(5),
            Cue("Front foot outside the hand; back knee down; sink the hips forward and down",
                "Forearms on a block or the sofa", "Forearms to the floor"), perSide: true),
        Drill("Bridge", 3, DrillDose.Breaths(5),
            Cue("Tilt the pelvis first, then peel up one vertebra at a time; push through the heels; no lower-back arch at the top",
                "Smaller lift", "Single-leg bridge, left side first")),
        Drill("Supine twist, then rest", 1, DrillDose.Seconds(30),
            Cue("Then 2 min rest. Knees together; both shoulders heavy on the floor; let go of the jaw",
                "Cushion under the knees", "Straight top leg"), perSide: true)
    ]);

    private static string Cue(string cues, string easier, string harder) => $"{cues}. Easier: {easier}. Harder: {harder}.";

    private static MobilityDrillDto Drill(string name, int sets, DrillDose dose, string cues, bool perSide = false) =>
        new(name, sets, new DrillDoseDto(dose.Unit, dose.Amount), SideBias.None, cues, null, perSide);
}
