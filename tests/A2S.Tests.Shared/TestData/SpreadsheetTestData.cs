using A2S.Domain.Enums;

namespace A2S.Tests.Shared.TestData;

/// <summary>
/// Test data based on the A2S 2024-2025 Program spreadsheet.
/// This file encodes the 21-week workout cycle data for testing.
/// </summary>
public static class SpreadsheetTestData
{
    /// <summary>
    /// All exercises from the 4-day split program.
    /// </summary>
    public static IReadOnlyList<ExerciseTestConfig> AllExercises => new[]
    {
        // Day 1 Exercises
        new ExerciseTestConfig("Lat Pulldown", DayNumber.Day1, 1, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 3, targetReps: 12),
        new ExerciseTestConfig("Overhead Press Smith Machine", DayNumber.Day1, 2, ProgressionTestType.Linear, EquipmentType.Machine,
            trainingMax: 65m),
        new ExerciseTestConfig("Cable Low Row", DayNumber.Day1, 3, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Cable Lateral Raise", DayNumber.Day1, 4, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 8),
        new ExerciseTestConfig("Cable Bicep Curl", DayNumber.Day1, 5, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 20),
        new ExerciseTestConfig("Cable Tricep Pushdown", DayNumber.Day1, 6, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 20),
        new ExerciseTestConfig("Rear Delt Flyes", DayNumber.Day1, 7, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 12),

        // Day 2 Exercises
        new ExerciseTestConfig("Smith Squat", DayNumber.Day2, 1, ProgressionTestType.Linear, EquipmentType.Machine,
            trainingMax: 107.5m),
        new ExerciseTestConfig("Single Leg Lunge Smith Machine", DayNumber.Day2, 2, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 9, isUnilateral: true),
        new ExerciseTestConfig("Lying Leg Curl", DayNumber.Day2, 3, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Hip Abduction", DayNumber.Day2, 4, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 12),
        new ExerciseTestConfig("Calf Raises", DayNumber.Day2, 5, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 15),

        // Day 3 Exercises
        new ExerciseTestConfig("Assisted Dips", DayNumber.Day3, 1, ProgressionTestType.MinimalSets, EquipmentType.Machine,
            startingWeight: 32m, targetTotalReps: 40, startingSets: 3),
        new ExerciseTestConfig("Assisted Pullups", DayNumber.Day3, 2, ProgressionTestType.MinimalSets, EquipmentType.Machine,
            startingWeight: 32m, targetTotalReps: 40, startingSets: 6),
        new ExerciseTestConfig("Concentration Curl", DayNumber.Day3, 3, ProgressionTestType.RepsPerSet, EquipmentType.Dumbbell,
            startingSets: 4, targetReps: 15, isUnilateral: true),
        new ExerciseTestConfig("Ez Curl", DayNumber.Day3, 4, ProgressionTestType.RepsPerSet, EquipmentType.Barbell,
            startingSets: 3, targetReps: 15),
        new ExerciseTestConfig("Single Arm Tricep Pushdown", DayNumber.Day3, 5, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 6, targetReps: 25, isUnilateral: true),
        new ExerciseTestConfig("Lateral Raises", DayNumber.Day3, 6, ProgressionTestType.RepsPerSet, EquipmentType.Dumbbell,
            startingSets: 3, targetReps: 20),
        new ExerciseTestConfig("Chest Flye", DayNumber.Day3, 7, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 8),

        // Day 4 Exercises
        new ExerciseTestConfig("Booty Builder", DayNumber.Day4, 1, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 8),
        new ExerciseTestConfig("Front Squat", DayNumber.Day4, 2, ProgressionTestType.Linear, EquipmentType.Barbell,
            trainingMax: 80m),
        new ExerciseTestConfig("Single Leg Press", DayNumber.Day4, 3, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12, isUnilateral: true),
        new ExerciseTestConfig("Leg Extension", DayNumber.Day4, 4, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Hip Adduction", DayNumber.Day4, 5, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12),
    };

    /// <summary>
    /// Gets the linear progression exercises only.
    /// </summary>
    public static IReadOnlyList<ExerciseTestConfig> LinearExercises =>
        AllExercises.Where(e => e.ProgressionType == ProgressionTestType.Linear).ToList();

    /// <summary>
    /// Gets the weekly data for a specific week and exercise.
    /// </summary>
    public static WeeklyExerciseTestData GetWeekData(int week, string exerciseName)
    {
        return AllWeeklyData.First(w => w.Week == week && w.ExerciseName == exerciseName);
    }

    /// <summary>
    /// All week-by-week data from the spreadsheet.
    /// This encodes the actual progression values.
    /// </summary>
    public static IReadOnlyList<WeeklyExerciseTestData> AllWeeklyData => GenerateWeeklyData();

    private static List<WeeklyExerciseTestData> GenerateWeeklyData()
    {
        var data = new List<WeeklyExerciseTestData>();

        // Week 1 Data (from spreadsheet)
        data.AddRange(GenerateWeek1Data());

        // Week 2 Data
        data.AddRange(GenerateWeek2Data());

        // Additional weeks follow the pattern from the spreadsheet
        // For brevity, key weeks are included (1, 2, 7 deload, 14 deload, 21 deload)

        // Week 7 - Deload
        data.AddRange(GenerateDeloadWeekData(7));

        // Week 14 - Deload
        data.AddRange(GenerateDeloadWeekData(14));

        // Week 21 - Deload
        data.AddRange(GenerateDeloadWeekData(21));

        return data;
    }

    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek1Data()
    {
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lat Pulldown",
            Day = DayNumber.Day1,
            Sets = 3,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Overhead Press Smith Machine",
            Day = DayNumber.Day1,
            Weight = 42.5m,
            RepsPerNormalSet = 12,
            RepOutTarget = 15,
            SetGoal = 4,
            AmrapResult = 19 // User exceeded by 4 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Cable Low Row",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Cable Lateral Raise",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 8,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Cable Bicep Curl",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Cable Tricep Pushdown",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 20,
            Completed = false // FAILED
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Rear Delt Flyes",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        // Day 2
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Smith Squat",
            Day = DayNumber.Day2,
            Weight = 75m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 5,
            AmrapResult = 16 // User exceeded by 6 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Single Leg Lunge Smith Machine",
            Day = DayNumber.Day2,
            Sets = 4,
            Reps = 9,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lying Leg Curl",
            Day = DayNumber.Day2,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Hip Abduction",
            Day = DayNumber.Day2,
            Sets = 3,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Calf Raises",
            Day = DayNumber.Day2,
            Sets = 3,
            Reps = 15,
            Completed = true
        };

        // Day 3
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Assisted Dips",
            Day = DayNumber.Day3,
            Weight = 32m,
            MinimalSetsUsed = 3,
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Assisted Pullups",
            Day = DayNumber.Day3,
            Weight = 32m,
            MinimalSetsUsed = 6,
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Concentration Curl",
            Day = DayNumber.Day3,
            Sets = 4,
            Reps = 15,
            Completed = false // FAILED
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Ez Curl",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 15,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Single Arm Tricep Pushdown",
            Day = DayNumber.Day3,
            Sets = 6,
            Reps = 25,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lateral Raises",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Chest Flye",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 8,
            Completed = true
        };

        // Day 4
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Booty Builder",
            Day = DayNumber.Day4,
            Sets = 3,
            Reps = 8,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 47.5m,
            RepsPerNormalSet = 7,
            RepOutTarget = 14,
            SetGoal = 5,
            AmrapResult = 17 // User exceeded by 3 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Single Leg Press",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Leg Extension",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12,
            Completed = null // No completion status in spreadsheet
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Hip Adduction",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12,
            Completed = null // No completion status in spreadsheet
        };
    }

    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek2Data()
    {
        // Week 2 shows progression from Week 1
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lat Pulldown",
            Day = DayNumber.Day1,
            Sets = 4, // Progressed from 3
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Overhead Press Smith Machine",
            Day = DayNumber.Day1,
            Weight = 45m, // Slight increase
            RepsPerNormalSet = 11,
            RepOutTarget = 13,
            SetGoal = 4
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Cable Low Row",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Cable Lateral Raise",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 8,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Cable Bicep Curl",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Cable Tricep Pushdown",
            Day = DayNumber.Day1,
            Sets = 4, // Maintained (failed week 1)
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Rear Delt Flyes",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 12,
            Completed = true
        };

        // Day 2
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Smith Squat",
            Day = DayNumber.Day2,
            Weight = 85m, // Increased
            RepsPerNormalSet = 4,
            RepOutTarget = 8,
            SetGoal = 5
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Single Leg Lunge Smith Machine",
            Day = DayNumber.Day2,
            Sets = 5, // Progressed
            Reps = 9,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lying Leg Curl",
            Day = DayNumber.Day2,
            Sets = 5, // Progressed
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Hip Abduction",
            Day = DayNumber.Day2,
            Sets = 4, // Progressed
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Calf Raises",
            Day = DayNumber.Day2,
            Sets = 4, // Progressed
            Reps = 15,
            Completed = false // Failed this week
        };

        // Day 3
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Assisted Dips",
            Day = DayNumber.Day3,
            Weight = 30m, // Reduced assistance
            MinimalSetsUsed = 4, // Increased sets (needed more)
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Assisted Pullups",
            Day = DayNumber.Day3,
            Weight = 30m, // Reduced assistance
            MinimalSetsUsed = 6, // Same sets
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Concentration Curl",
            Day = DayNumber.Day3,
            Sets = 4, // Maintained (failed week 1)
            Reps = 15,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Ez Curl",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 15,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Single Arm Tricep Pushdown",
            Day = DayNumber.Day3,
            Sets = 4, // Adjusted (was 6)
            Reps = 26, // Increased reps
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lateral Raises",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 20,
            Completed = false // Failed
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Chest Flye",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 8,
            Completed = true
        };

        // Day 4
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Booty Builder",
            Day = DayNumber.Day4,
            Sets = 4, // Progressed
            Reps = 8
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 52.5m, // Increased
            RepsPerNormalSet = 6,
            RepOutTarget = 12,
            SetGoal = 5
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Single Leg Press",
            Day = DayNumber.Day4,
            Sets = 5, // Progressed
            Reps = 12
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Leg Extension",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Hip Adduction",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12
        };
    }

    private static IEnumerable<WeeklyExerciseTestData> GenerateDeloadWeekData(int week)
    {
        // Deload weeks (7, 14, 21) have reduced volume/intensity
        // Linear exercises use 65% intensity, n/a for rep out target

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Overhead Press Smith Machine",
            Day = DayNumber.Day1,
            Weight = 40m, // Reduced for deload
            RepsPerNormalSet = 5,
            RepOutTarget = null, // n/a for deload
            SetGoal = 4,
            IsDeloadWeek = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Smith Squat",
            Day = DayNumber.Day2,
            Weight = 65m, // Reduced for deload
            RepsPerNormalSet = 5,
            RepOutTarget = null, // n/a for deload
            SetGoal = 4,
            IsDeloadWeek = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 47.5m, // Reduced for deload
            RepsPerNormalSet = 5,
            RepOutTarget = null, // n/a for deload
            SetGoal = 4,
            IsDeloadWeek = true
        };

        // Accessory exercises maintain sets but may have reduced volume
        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Lat Pulldown",
            Day = DayNumber.Day1,
            Sets = 5,
            Reps = 12,
            IsDeloadWeek = true
        };
    }
}

/// <summary>
/// Configuration for setting up a test exercise.
/// </summary>
public sealed record ExerciseTestConfig
{
    public string Name { get; }
    public DayNumber Day { get; }
    public int Order { get; }
    public ProgressionTestType ProgressionType { get; }
    public EquipmentType Equipment { get; }

    // Linear progression
    public decimal? TrainingMax { get; }

    // RepsPerSet progression
    public int? StartingSets { get; }
    public int? TargetReps { get; }
    public bool IsUnilateral { get; }

    // MinimalSets progression
    public decimal? StartingWeight { get; }
    public int? TargetTotalReps { get; }

    public ExerciseTestConfig(
        string name,
        DayNumber day,
        int order,
        ProgressionTestType progressionType,
        EquipmentType equipment,
        decimal? trainingMax = null,
        int? startingSets = null,
        int? targetReps = null,
        bool isUnilateral = false,
        decimal? startingWeight = null,
        int? targetTotalReps = null)
    {
        Name = name;
        Day = day;
        Order = order;
        ProgressionType = progressionType;
        Equipment = equipment;
        TrainingMax = trainingMax;
        StartingSets = startingSets;
        TargetReps = targetReps;
        IsUnilateral = isUnilateral;
        StartingWeight = startingWeight;
        TargetTotalReps = targetTotalReps;
    }
}

/// <summary>
/// Weekly exercise data from the spreadsheet.
/// </summary>
public sealed class WeeklyExerciseTestData
{
    public int Week { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public DayNumber Day { get; init; }
    public bool IsDeloadWeek { get; init; }

    // Linear progression data
    public decimal? Weight { get; init; }
    public int? RepsPerNormalSet { get; init; }
    public int? RepOutTarget { get; init; }
    public int? SetGoal { get; init; }
    public int? AmrapResult { get; init; }

    // RepsPerSet data
    public int? Sets { get; init; }
    public int? Reps { get; init; }
    public bool? Completed { get; init; }

    // MinimalSets data
    public int? MinimalSetsUsed { get; init; }
    public int? TargetTotalReps { get; init; }
}

/// <summary>
/// Type of progression for test exercises.
/// </summary>
public enum ProgressionTestType
{
    Linear,      // RTF/AMRAP based (red in spreadsheet)
    RepsPerSet,  // Set/rep based (green in spreadsheet)
    MinimalSets  // Total reps in minimal sets (yellow in spreadsheet)
}
