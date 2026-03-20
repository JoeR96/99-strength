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
    /// Names must match ExerciseLibrary exactly.
    /// </summary>
    public static IReadOnlyList<ExerciseTestConfig> AllExercises => new[]
    {
        // Day 1 Exercises
        new ExerciseTestConfig("Lat Pulldown (Cable)", DayNumber.Day1, 1, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 3, targetReps: 12),
        new ExerciseTestConfig("Overhead Press (Smith Machine)", DayNumber.Day1, 2, ProgressionTestType.Linear, EquipmentType.Machine,
            trainingMax: 65m),
        new ExerciseTestConfig("Seated Cable Row - V Grip (Cable)", DayNumber.Day1, 3, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Lateral Raise (Cable)", DayNumber.Day1, 4, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 8),
        new ExerciseTestConfig("Bicep Curl (Cable)", DayNumber.Day1, 5, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 20),
        new ExerciseTestConfig("Triceps Pushdown", DayNumber.Day1, 6, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 20),
        new ExerciseTestConfig("Rear Delt Reverse Fly (Cable)", DayNumber.Day1, 7, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 4, targetReps: 12),

        // Day 2 Exercises
        new ExerciseTestConfig("Squat (Smith Machine)", DayNumber.Day2, 1, ProgressionTestType.Linear, EquipmentType.Machine,
            trainingMax: 107.5m),
        new ExerciseTestConfig("Single Leg Smith Lunge", DayNumber.Day2, 2, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 9, isUnilateral: true),
        new ExerciseTestConfig("Lying Leg Curl (Machine)", DayNumber.Day2, 3, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Hip Abduction (Machine)", DayNumber.Day2, 4, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 12),
        new ExerciseTestConfig("Standing Calf Raise (Machine)", DayNumber.Day2, 5, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 15),

        // Day 3 Exercises
        new ExerciseTestConfig("Triceps Dip (Assisted)", DayNumber.Day3, 1, ProgressionTestType.MinimalSets, EquipmentType.Machine,
            startingWeight: 32m, targetTotalReps: 40, startingSets: 3),
        new ExerciseTestConfig("Pull Up (Assisted)", DayNumber.Day3, 2, ProgressionTestType.MinimalSets, EquipmentType.Machine,
            startingWeight: 32m, targetTotalReps: 40, startingSets: 6),
        new ExerciseTestConfig("Concentration Curl", DayNumber.Day3, 3, ProgressionTestType.RepsPerSet, EquipmentType.Dumbbell,
            startingSets: 4, targetReps: 15, isUnilateral: true),
        new ExerciseTestConfig("Bicep Curl (Barbell)", DayNumber.Day3, 4, ProgressionTestType.RepsPerSet, EquipmentType.Barbell,
            startingSets: 3, targetReps: 15),
        new ExerciseTestConfig("Single Arm Triceps Pushdown (Cable)", DayNumber.Day3, 5, ProgressionTestType.RepsPerSet, EquipmentType.Cable,
            startingSets: 6, targetReps: 25, isUnilateral: true),
        new ExerciseTestConfig("Lateral Raise (Dumbbell)", DayNumber.Day3, 6, ProgressionTestType.RepsPerSet, EquipmentType.Dumbbell,
            startingSets: 3, targetReps: 20),
        new ExerciseTestConfig("Chest Fly (Machine)", DayNumber.Day3, 7, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 8),

        // Day 4 Exercises
        new ExerciseTestConfig("Hip Thrust (Machine)", DayNumber.Day4, 1, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 3, targetReps: 8),
        new ExerciseTestConfig("Front Squat", DayNumber.Day4, 2, ProgressionTestType.Linear, EquipmentType.Barbell,
            trainingMax: 80m),
        new ExerciseTestConfig("Single Leg Press (Machine)", DayNumber.Day4, 3, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12, isUnilateral: true),
        new ExerciseTestConfig("Leg Extension (Machine)", DayNumber.Day4, 4, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
            startingSets: 4, targetReps: 12),
        new ExerciseTestConfig("Hip Adduction (Machine)", DayNumber.Day4, 5, ProgressionTestType.RepsPerSet, EquipmentType.Machine,
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

        // Block 1: Weeks 1-7
        data.AddRange(GenerateWeek1Data());
        data.AddRange(GenerateWeek2Data());
        data.AddRange(GenerateWeek3Data());
        data.AddRange(GenerateWeek4Data());
        data.AddRange(GenerateWeek5Data());
        data.AddRange(GenerateWeek6Data());
        data.AddRange(GenerateDeloadWeekData(7));

        // Block 2: Weeks 8-14
        data.AddRange(GenerateWeek8Data());
        data.AddRange(GenerateWeek9Data());
        data.AddRange(GenerateWeek10Data());
        data.AddRange(GenerateWeek11Data());
        data.AddRange(GenerateWeek12Data());
        data.AddRange(GenerateWeek13Data());
        data.AddRange(GenerateDeloadWeekData(14));

        // Block 3: Weeks 15-21
        data.AddRange(GenerateWeek15Data());
        data.AddRange(GenerateWeek16Data());
        data.AddRange(GenerateWeek17Data());
        data.AddRange(GenerateWeek18Data());
        data.AddRange(GenerateWeek19Data());
        data.AddRange(GenerateWeek20Data());
        data.AddRange(GenerateDeloadWeekData(21));

        return data;
    }

    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek1Data()
    {
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lat Pulldown (Cable)",
            Day = DayNumber.Day1,
            Sets = 3,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Overhead Press (Smith Machine)",
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
            ExerciseName = "Seated Cable Row - V Grip (Cable)",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lateral Raise (Cable)",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 8,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Bicep Curl (Cable)",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Triceps Pushdown",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 20,
            Completed = false // FAILED
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Rear Delt Reverse Fly (Cable)",
            Day = DayNumber.Day1,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        // Day 2
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Squat (Smith Machine)",
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
            ExerciseName = "Single Leg Smith Lunge",
            Day = DayNumber.Day2,
            Sets = 4,
            Reps = 9,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lying Leg Curl (Machine)",
            Day = DayNumber.Day2,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Hip Abduction (Machine)",
            Day = DayNumber.Day2,
            Sets = 3,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Standing Calf Raise (Machine)",
            Day = DayNumber.Day2,
            Sets = 3,
            Reps = 15,
            Completed = true
        };

        // Day 3
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Triceps Dip (Assisted)",
            Day = DayNumber.Day3,
            Weight = 32m,
            MinimalSetsUsed = 3,
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Pull Up (Assisted)",
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
            ExerciseName = "Bicep Curl (Barbell)",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 15,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Single Arm Triceps Pushdown (Cable)",
            Day = DayNumber.Day3,
            Sets = 6,
            Reps = 25,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Lateral Raise (Dumbbell)",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Chest Fly (Machine)",
            Day = DayNumber.Day3,
            Sets = 3,
            Reps = 8,
            Completed = true
        };

        // Day 4
        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Hip Thrust (Machine)",
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
            ExerciseName = "Single Leg Press (Machine)",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Leg Extension (Machine)",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12,
            Completed = null // No completion status in spreadsheet
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 1,
            ExerciseName = "Hip Adduction (Machine)",
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
            ExerciseName = "Lat Pulldown (Cable)",
            Day = DayNumber.Day1,
            Sets = 4, // Progressed from 3
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 45m, // Slight increase
            RepsPerNormalSet = 11,
            RepOutTarget = 13,
            SetGoal = 4
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Seated Cable Row - V Grip (Cable)",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lateral Raise (Cable)",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 8,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Bicep Curl (Cable)",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Triceps Pushdown",
            Day = DayNumber.Day1,
            Sets = 4, // Maintained (failed week 1)
            Reps = 20,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Rear Delt Reverse Fly (Cable)",
            Day = DayNumber.Day1,
            Sets = 5, // Progressed from 4
            Reps = 12,
            Completed = true
        };

        // Day 2
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 85m, // Increased
            RepsPerNormalSet = 4,
            RepOutTarget = 8,
            SetGoal = 5
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Single Leg Smith Lunge",
            Day = DayNumber.Day2,
            Sets = 5, // Progressed
            Reps = 9,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lying Leg Curl (Machine)",
            Day = DayNumber.Day2,
            Sets = 5, // Progressed
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Hip Abduction (Machine)",
            Day = DayNumber.Day2,
            Sets = 4, // Progressed
            Reps = 12,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Standing Calf Raise (Machine)",
            Day = DayNumber.Day2,
            Sets = 4, // Progressed
            Reps = 15,
            Completed = false // Failed this week
        };

        // Day 3
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Triceps Dip (Assisted)",
            Day = DayNumber.Day3,
            Weight = 30m, // Reduced assistance
            MinimalSetsUsed = 4, // Increased sets (needed more)
            TargetTotalReps = 40
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Pull Up (Assisted)",
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
            ExerciseName = "Bicep Curl (Barbell)",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 15,
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Single Arm Triceps Pushdown (Cable)",
            Day = DayNumber.Day3,
            Sets = 4, // Adjusted (was 6)
            Reps = 26, // Increased reps
            Completed = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Lateral Raise (Dumbbell)",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 20,
            Completed = false // Failed
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Chest Fly (Machine)",
            Day = DayNumber.Day3,
            Sets = 4, // Progressed
            Reps = 8,
            Completed = true
        };

        // Day 4
        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Hip Thrust (Machine)",
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
            ExerciseName = "Single Leg Press (Machine)",
            Day = DayNumber.Day4,
            Sets = 5, // Progressed
            Reps = 12
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Leg Extension (Machine)",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 2,
            ExerciseName = "Hip Adduction (Machine)",
            Day = DayNumber.Day4,
            Sets = 4,
            Reps = 12
        };
    }

    /// <summary>
    /// Week 3: 90% intensity, 3 sets, 6 reps
    /// TM adjustments from Week 2 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek3Data()
    {
        // Week 3 Linear pattern: 90%, 3 sets, 6 reps, RepOutTarget = 11
        // OHP TM after Week 2: ~66.3kg (good week), Weight = 66.3 * 0.90 = 60kg rounded
        // Smith Squat TM after Week 2: ~110.7kg (excellent week), Weight = 110.7 * 0.90 = 100kg rounded
        // Front Squat TM after Week 2: ~81.2kg (good week), Weight = 81.2 * 0.90 = 72.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 3,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 60m, // 66.3 * 0.90 rounded to 2.5kg
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 13 // +2 reps, good performance
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 3,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 100m, // 110.7 * 0.90 rounded to 2.5kg
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 14 // +3 reps, excellent
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 3,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 72.5m, // 81.2 * 0.90 rounded to 2.5kg
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 12 // +1 rep, solid
        };
    }

    /// <summary>
    /// Week 4: 80% intensity, 5 sets, 9 reps
    /// TM adjustments from Week 3 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek4Data()
    {
        // Week 4 Linear pattern: 80%, 5 sets, 9 reps, RepOutTarget = 14
        // OHP TM after Week 3: ~66.96kg (+1% from +2 reps), Weight = 66.96 * 0.80 = 52.5kg rounded
        // Smith Squat TM after Week 3: ~112.4kg (+1.5% from +3 reps), Weight = 112.4 * 0.80 = 90kg rounded
        // Front Squat TM after Week 3: ~81.6kg (+0.5% from +1 rep), Weight = 81.6 * 0.80 = 65kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 4,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 52.5m,
            RepsPerNormalSet = 9,
            RepOutTarget = 14,
            SetGoal = 5,
            AmrapResult = 18 // +4 reps, great session
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 4,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 90m,
            RepsPerNormalSet = 9,
            RepOutTarget = 14,
            SetGoal = 5,
            AmrapResult = 16 // +2 reps, solid
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 4,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 65m,
            RepsPerNormalSet = 9,
            RepOutTarget = 14,
            SetGoal = 5,
            AmrapResult = 17 // +3 reps, good
        };
    }

    /// <summary>
    /// Week 5: 85% intensity, 4 sets, 7 reps
    /// TM adjustments from Week 4 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek5Data()
    {
        // Week 5 Linear pattern: 85%, 4 sets, 7 reps, RepOutTarget = 12
        // OHP TM after Week 4: ~68.3kg (+2% from +4 reps), Weight = 68.3 * 0.85 = 57.5kg rounded
        // Smith Squat TM after Week 4: ~113.5kg (+1% from +2 reps), Weight = 113.5 * 0.85 = 97.5kg rounded
        // Front Squat TM after Week 4: ~82.8kg (+1.5% from +3 reps), Weight = 82.8 * 0.85 = 70kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 5,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 57.5m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 14 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 5,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 97.5m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 15 // +3 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 5,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 70m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 13 // +1 rep
        };
    }

    /// <summary>
    /// Week 6: 90% intensity, 3 sets, 5 reps
    /// TM adjustments from Week 5 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek6Data()
    {
        // Week 6 Linear pattern: 90%, 3 sets, 5 reps, RepOutTarget = 10
        // OHP TM after Week 5: ~68.98kg (+1% from +2 reps), Weight = 68.98 * 0.90 = 62.5kg rounded
        // Smith Squat TM after Week 5: ~115.2kg (+1.5% from +3 reps), Weight = 115.2 * 0.90 = 102.5kg rounded
        // Front Squat TM after Week 5: ~83.2kg (+0.5% from +1 rep), Weight = 83.2 * 0.90 = 75kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 6,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 62.5m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 12 // +2 reps, finishing block 1 strong
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 6,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 102.5m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 11 // +1 rep, hard week
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 6,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 75m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 10 // exact target, tough
        };
    }

    // Week 7 is deload - handled by GenerateDeloadWeekData(7)

    /// <summary>
    /// Week 8: 85% intensity, 4 sets, 8 reps (Block 2 starts)
    /// TM adjustments from Week 6 AMRAP results applied (deload week 7 doesn't affect TM)
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek8Data()
    {
        // Week 8 Linear pattern: 85%, 4 sets, 8 reps, RepOutTarget = 13
        // OHP TM after Week 6: ~69.67kg (+1% from +2 reps), Weight = 69.67 * 0.85 = 60kg rounded
        // Smith Squat TM after Week 6: ~115.78kg (+0.5% from +1 rep), Weight = 115.78 * 0.85 = 97.5kg rounded
        // Front Squat TM after Week 6: ~83.2kg (no change from 0 reps), Weight = 83.2 * 0.85 = 70kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 8,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 60m,
            RepsPerNormalSet = 8,
            RepOutTarget = 13,
            SetGoal = 4,
            AmrapResult = 16 // +3 reps, fresh after deload
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 8,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 97.5m,
            RepsPerNormalSet = 8,
            RepOutTarget = 13,
            SetGoal = 4,
            AmrapResult = 17 // +4 reps, excellent post-deload
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 8,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 70m,
            RepsPerNormalSet = 8,
            RepOutTarget = 13,
            SetGoal = 4,
            AmrapResult = 15 // +2 reps
        };
    }

    /// <summary>
    /// Week 9: 90% intensity, 3 sets, 6 reps
    /// TM adjustments from Week 8 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek9Data()
    {
        // Week 9 Linear pattern: 90%, 3 sets, 6 reps, RepOutTarget = 11
        // OHP TM after Week 8: ~70.7kg (+1.5% from +3 reps), Weight = 70.7 * 0.90 = 62.5kg rounded
        // Smith Squat TM after Week 8: ~118.1kg (+2% from +4 reps), Weight = 118.1 * 0.90 = 107.5kg rounded
        // Front Squat TM after Week 8: ~84kg (+1% from +2 reps), Weight = 84 * 0.90 = 75kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 9,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 62.5m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 13 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 9,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 107.5m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 12 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 9,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 75m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 14 // +3 reps, strong session
        };
    }

    /// <summary>
    /// Week 10: 95% intensity, 2 sets, 4 reps
    /// TM adjustments from Week 9 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek10Data()
    {
        // Week 10 Linear pattern: 95%, 2 sets, 4 reps, RepOutTarget = 9
        // OHP TM after Week 9: ~71.4kg (+1% from +2 reps), Weight = 71.4 * 0.95 = 67.5kg rounded
        // Smith Squat TM after Week 9: ~118.7kg (+0.5% from +1 rep), Weight = 118.7 * 0.95 = 112.5kg rounded
        // Front Squat TM after Week 9: ~85.3kg (+1.5% from +3 reps), Weight = 85.3 * 0.95 = 80kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 10,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 67.5m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 10 // +1 rep, heavy week
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 10,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 112.5m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 11 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 10,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 80m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 9 // exact target, tough
        };
    }

    /// <summary>
    /// Week 11: 85% intensity, 4 sets, 7 reps
    /// TM adjustments from Week 10 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek11Data()
    {
        // Week 11 Linear pattern: 85%, 4 sets, 7 reps, RepOutTarget = 12
        // OHP TM after Week 10: ~71.8kg (+0.5% from +1 rep), Weight = 71.8 * 0.85 = 60kg rounded
        // Smith Squat TM after Week 10: ~119.9kg (+1% from +2 reps), Weight = 119.9 * 0.85 = 102.5kg rounded
        // Front Squat TM after Week 10: ~85.3kg (no change from 0 reps), Weight = 85.3 * 0.85 = 72.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 11,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 60m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 15 // +3 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 11,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 102.5m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 14 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 11,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 72.5m,
            RepsPerNormalSet = 7,
            RepOutTarget = 12,
            SetGoal = 4,
            AmrapResult = 16 // +4 reps, great session
        };
    }

    /// <summary>
    /// Week 12: 90% intensity, 3 sets, 5 reps
    /// TM adjustments from Week 11 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek12Data()
    {
        // Week 12 Linear pattern: 90%, 3 sets, 5 reps, RepOutTarget = 10
        // OHP TM after Week 11: ~72.9kg (+1.5% from +3 reps), Weight = 72.9 * 0.90 = 65kg rounded
        // Smith Squat TM after Week 11: ~121.1kg (+1% from +2 reps), Weight = 121.1 * 0.90 = 110kg rounded
        // Front Squat TM after Week 11: ~87kg (+2% from +4 reps), Weight = 87 * 0.90 = 77.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 12,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 65m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 11 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 12,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 110m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 12 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 12,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 77.5m,
            RepsPerNormalSet = 5,
            RepOutTarget = 10,
            SetGoal = 3,
            AmrapResult = 11 // +1 rep
        };
    }

    /// <summary>
    /// Week 13: 95% intensity, 2 sets, 3 reps
    /// TM adjustments from Week 12 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek13Data()
    {
        // Week 13 Linear pattern: 95%, 2 sets, 3 reps, RepOutTarget = 8
        // OHP TM after Week 12: ~73.3kg (+0.5% from +1 rep), Weight = 73.3 * 0.95 = 70kg rounded
        // Smith Squat TM after Week 12: ~122.3kg (+1% from +2 reps), Weight = 122.3 * 0.95 = 115kg rounded
        // Front Squat TM after Week 12: ~87.4kg (+0.5% from +1 rep), Weight = 87.4 * 0.95 = 82.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 13,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 70m,
            RepsPerNormalSet = 3,
            RepOutTarget = 8,
            SetGoal = 2,
            AmrapResult = 8 // exact target, hard week
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 13,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 115m,
            RepsPerNormalSet = 3,
            RepOutTarget = 8,
            SetGoal = 2,
            AmrapResult = 9 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 13,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 82.5m,
            RepsPerNormalSet = 3,
            RepOutTarget = 8,
            SetGoal = 2,
            AmrapResult = 7 // -1 rep, fatigue setting in
        };
    }

    // Week 14 is deload - handled by GenerateDeloadWeekData(14)

    /// <summary>
    /// Week 15: 90% intensity, 3 sets, 6 reps (Block 3 starts)
    /// TM adjustments from Week 13 AMRAP results applied (deload week 14 doesn't affect TM)
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek15Data()
    {
        // Week 15 Linear pattern: 90%, 3 sets, 6 reps, RepOutTarget = 11
        // OHP TM after Week 13: ~73.3kg (no change from 0 reps), Weight = 73.3 * 0.90 = 65kg rounded
        // Smith Squat TM after Week 13: ~122.9kg (+0.5% from +1 rep), Weight = 122.9 * 0.90 = 110kg rounded
        // Front Squat TM after Week 13: ~85.6kg (-2% from -1 rep), Weight = 85.6 * 0.90 = 77.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 15,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 65m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 14 // +3 reps, fresh after deload
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 15,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 110m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 13 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 15,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 77.5m,
            RepsPerNormalSet = 6,
            RepOutTarget = 11,
            SetGoal = 3,
            AmrapResult = 15 // +4 reps, recovered well
        };
    }

    /// <summary>
    /// Week 16: 95% intensity, 2 sets, 4 reps
    /// TM adjustments from Week 15 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek16Data()
    {
        // Week 16 Linear pattern: 95%, 2 sets, 4 reps, RepOutTarget = 9
        // OHP TM after Week 15: ~74.4kg (+1.5% from +3 reps), Weight = 74.4 * 0.95 = 70kg rounded
        // Smith Squat TM after Week 15: ~124.1kg (+1% from +2 reps), Weight = 124.1 * 0.95 = 117.5kg rounded
        // Front Squat TM after Week 15: ~87.3kg (+2% from +4 reps), Weight = 87.3 * 0.95 = 82.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 16,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 70m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 11 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 16,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 117.5m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 10 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 16,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 82.5m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 12 // +3 reps
        };
    }

    /// <summary>
    /// Week 17: 100% intensity, 1 set, 2 reps
    /// TM adjustments from Week 16 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek17Data()
    {
        // Week 17 Linear pattern: 100%, 1 set, 2 reps, RepOutTarget = 7
        // OHP TM after Week 16: ~75.1kg (+1% from +2 reps), Weight = 75.1 * 1.00 = 75kg rounded
        // Smith Squat TM after Week 16: ~124.7kg (+0.5% from +1 rep), Weight = 124.7 * 1.00 = 125kg rounded
        // Front Squat TM after Week 16: ~88.6kg (+1.5% from +3 reps), Weight = 88.6 * 1.00 = 87.5kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 17,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 75m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 8 // +1 rep, heavy
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 17,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 125m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 9 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 17,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 87.5m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 7 // exact target
        };
    }

    /// <summary>
    /// Week 18: 95% intensity, 2 sets, 4 reps
    /// TM adjustments from Week 17 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek18Data()
    {
        // Week 18 Linear pattern: 95%, 2 sets, 4 reps, RepOutTarget = 9
        // OHP TM after Week 17: ~75.5kg (+0.5% from +1 rep), Weight = 75.5 * 0.95 = 72.5kg rounded
        // Smith Squat TM after Week 17: ~126kg (+1% from +2 reps), Weight = 126 * 0.95 = 120kg rounded
        // Front Squat TM after Week 17: ~88.6kg (no change from 0 reps), Weight = 88.6 * 0.95 = 85kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 18,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 72.5m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 11 // +2 reps
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 18,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 120m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 10 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 18,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 85m,
            RepsPerNormalSet = 4,
            RepOutTarget = 9,
            SetGoal = 2,
            AmrapResult = 11 // +2 reps
        };
    }

    /// <summary>
    /// Week 19: 100% intensity, 1 set, 2 reps
    /// TM adjustments from Week 18 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek19Data()
    {
        // Week 19 Linear pattern: 100%, 1 set, 2 reps, RepOutTarget = 7
        // OHP TM after Week 18: ~76.3kg (+1% from +2 reps), Weight = 76.3 * 1.00 = 77.5kg rounded
        // Smith Squat TM after Week 18: ~126.6kg (+0.5% from +1 rep), Weight = 126.6 * 1.00 = 127.5kg rounded
        // Front Squat TM after Week 18: ~89.5kg (+1% from +2 reps), Weight = 89.5 * 1.00 = 90kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 19,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 77.5m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 8 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 19,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 127.5m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 8 // +1 rep
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 19,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 90m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 9 // +2 reps
        };
    }

    /// <summary>
    /// Week 20: 105% intensity, 1 set, 2 reps (PEAK WEEK)
    /// TM adjustments from Week 19 AMRAP results applied
    /// </summary>
    private static IEnumerable<WeeklyExerciseTestData> GenerateWeek20Data()
    {
        // Week 20 Linear pattern: 105%, 1 set, 2 reps, RepOutTarget = 7 (PEAK)
        // OHP TM after Week 19: ~76.7kg (+0.5% from +1 rep), Weight = 76.7 * 1.05 = 80kg rounded
        // Smith Squat TM after Week 19: ~127.2kg (+0.5% from +1 rep), Weight = 127.2 * 1.05 = 132.5kg rounded
        // Front Squat TM after Week 19: ~90.4kg (+1% from +2 reps), Weight = 90.4 * 1.05 = 95kg rounded

        yield return new WeeklyExerciseTestData
        {
            Week = 20,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = 80m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 6 // -1 rep, peak week is hard
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 20,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = 132.5m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 7 // exact target, peak achieved
        };

        yield return new WeeklyExerciseTestData
        {
            Week = 20,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = 95m,
            RepsPerNormalSet = 2,
            RepOutTarget = 7,
            SetGoal = 1,
            AmrapResult = 8 // +1 rep, strong finish
        };
    }

    // Week 21 is deload - handled by GenerateDeloadWeekData(21)

    private static IEnumerable<WeeklyExerciseTestData> GenerateDeloadWeekData(int week)
    {
        // Deload weeks (7, 14, 21) have reduced volume/intensity
        // Linear exercises use 65% intensity, 5 sets, 10 reps, n/a for rep out target

        // Calculate TM based on which deload week
        // Week 7: OHP ~69.67kg, Smith Squat ~115.78kg, Front Squat ~83.2kg
        // Week 14: OHP ~73.3kg, Smith Squat ~122.9kg, Front Squat ~85.6kg
        // Week 21: OHP ~75.2kg, Smith Squat ~127.2kg, Front Squat ~90.4kg

        decimal ohpWeight, squatWeight, frontSquatWeight;

        if (week == 7)
        {
            ohpWeight = 45m;      // 69.67 * 0.65 = 45.3 -> 45
            squatWeight = 75m;    // 115.78 * 0.65 = 75.3 -> 75
            frontSquatWeight = 52.5m; // 83.2 * 0.65 = 54.1 -> 52.5
        }
        else if (week == 14)
        {
            ohpWeight = 47.5m;    // 73.3 * 0.65 = 47.6 -> 47.5
            squatWeight = 80m;    // 122.9 * 0.65 = 79.9 -> 80
            frontSquatWeight = 55m; // 85.6 * 0.65 = 55.6 -> 55
        }
        else // week == 21
        {
            ohpWeight = 50m;      // 75.2 * 0.65 = 48.9 -> 50
            squatWeight = 82.5m;  // 127.2 * 0.65 = 82.7 -> 82.5
            frontSquatWeight = 60m; // 90.4 * 0.65 = 58.8 -> 60
        }

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Overhead Press (Smith Machine)",
            Day = DayNumber.Day1,
            Weight = ohpWeight,
            RepsPerNormalSet = 10,
            RepOutTarget = null, // n/a for deload
            SetGoal = 5,
            IsDeloadWeek = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Squat (Smith Machine)",
            Day = DayNumber.Day2,
            Weight = squatWeight,
            RepsPerNormalSet = 10,
            RepOutTarget = null, // n/a for deload
            SetGoal = 5,
            IsDeloadWeek = true
        };

        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Front Squat",
            Day = DayNumber.Day4,
            Weight = frontSquatWeight,
            RepsPerNormalSet = 10,
            RepOutTarget = null, // n/a for deload
            SetGoal = 5,
            IsDeloadWeek = true
        };

        // Accessory exercises maintain sets but may have reduced volume
        yield return new WeeklyExerciseTestData
        {
            Week = week,
            ExerciseName = "Lat Pulldown (Cable)",
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

/// <summary>
/// A single week's expected state for a RepsPerSet progression scenario.
/// Reps=-1 signals FAILED: first set at 7 reps (below minimum), rest at 10.
/// </summary>
public sealed record RepsPerSetWeekScenario(
    int Reps,
    int ExpectedSets,
    decimal ExpectedWeight,
    string Description)
{
    /// <summary>Sentinel value indicating a failed week (first set below minimum reps).</summary>
    public const int FailedWeekSentinel = -1;
}

/// <summary>
/// Hand-computed RepsPerSet validation scenarios extracted from E2E test data.
/// </summary>
public static class RepsPerSetValidationData
{
    /// <summary>
    /// Bilateral Bent Over Row (Barbell): +2.5kg increments.
    /// StartingSets=3, TargetSets=5, MaxSets=5 (bilateral).
    /// RepRange: Min=8, Target=10, Max=12.
    /// SUCCESS (all sets ≥ 12): add set. At MaxSets + SUCCESS: weight +2.5kg, reset to StartingSets.
    /// MAINTAINED (all sets ≥ 8 but not all ≥ 12): no change.
    /// FAILED (any set < 8): remove set.
    /// </summary>
    public static IReadOnlyList<RepsPerSetWeekScenario> BilateralBarbellScenarios { get; } = new[]
    {
        new RepsPerSetWeekScenario(12, 4, 60m,   "S: 3→4"),
        new RepsPerSetWeekScenario(12, 5, 60m,   "S: 4→5"),
        new RepsPerSetWeekScenario(12, 3, 62.5m, "S at target: +2.5kg, reset 3"),
        new RepsPerSetWeekScenario(10, 3, 62.5m, "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 62.5m, "S: 3→4"),
        new RepsPerSetWeekScenario(12, 5, 62.5m, "S: 4→5"),
        new RepsPerSetWeekScenario(12, 3, 65m,   "S at target: +2.5kg, reset 3"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 2, 65m,   "F: 3→2"),
        new RepsPerSetWeekScenario(10, 2, 65m,   "M: no change"),
        new RepsPerSetWeekScenario(12, 3, 65m,   "S: 2→3"),
        new RepsPerSetWeekScenario(12, 4, 65m,   "S: 3→4"),
        new RepsPerSetWeekScenario(12, 5, 65m,   "S: 4→5"),
        new RepsPerSetWeekScenario(12, 3, 67.5m, "S at target: +2.5kg, reset 3"),
        new RepsPerSetWeekScenario(10, 3, 67.5m, "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 67.5m, "S: 3→4"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 3, 67.5m, "F: 4→3"),
        new RepsPerSetWeekScenario(12, 4, 67.5m, "S: 3→4"),
        new RepsPerSetWeekScenario(12, 5, 67.5m, "S: 4→5"),
        new RepsPerSetWeekScenario(12, 3, 70m,   "S at target: +2.5kg, reset 3"),
        new RepsPerSetWeekScenario(10, 3, 70m,   "M: no change"),
        new RepsPerSetWeekScenario(10, 3, 70m,   "M: no change"),
    };

    /// <summary>
    /// Unilateral Single Arm Lat Pulldown (Cable): +2.5kg increments.
    /// StartingSets=4, TargetSets=6, MaxSets=3 (unilateral cap).
    /// EffectiveMaxSets = min(6, 3) = 3. StartingSets(4) > EffectiveMaxSets(3),
    /// so SUCCESS (sets ≥ 3) always increases weight and resets to StartingSets(4).
    /// FAILED drops sets. Only when sets < 3 does SUCCESS add a set instead.
    /// RepRange: Min=8, Target=10, Max=12.
    /// </summary>
    public static IReadOnlyList<RepsPerSetWeekScenario> UnilateralCableScenarios { get; } = new[]
    {
        new RepsPerSetWeekScenario(12, 4, 81.5m,  "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(12, 4, 84m,    "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(10, 4, 84m,    "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 86.5m,  "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 3, 86.5m,  "F: 4→3"),
        new RepsPerSetWeekScenario(10, 3, 86.5m,  "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 89m,    "S: 3>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 3, 89m,    "F: 4→3"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 2, 89m,    "F: 3→2"),
        new RepsPerSetWeekScenario(12, 3, 89m,    "S: 2<3, add set 2→3"),
        new RepsPerSetWeekScenario(12, 4, 91.5m,  "S: 3>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(10, 4, 91.5m,  "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 94m,    "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(10, 4, 94m,    "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 96.5m,  "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(10, 4, 96.5m,  "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 99m,    "S: 4>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(RepsPerSetWeekScenario.FailedWeekSentinel, 3, 99m,    "F: 4→3"),
        new RepsPerSetWeekScenario(10, 3, 99m,    "M: no change"),
        new RepsPerSetWeekScenario(12, 4, 101.5m, "S: 3>=3, +2.5kg, reset 4"),
        new RepsPerSetWeekScenario(10, 4, 101.5m, "M: no change"),
    };

    /// <summary>
    /// Smith Squat Linear Progression validation data from A2S2_Validation_Data.md.
    /// Initial TM=110kg. Hand-crafted deltas to exercise ALL 8 TM adjustment multipliers.
    /// </summary>
    public static IReadOnlyList<int> SmithSquatAmrapDeltas { get; } = new[]
    {
        +5, +2, +2, +2, +2, +1, 0,   // Block 1 (week 7 = deload)
        +3, +4, 0, -1, -3, 0, 0,     // Block 2 (week 14 = deload, weeks 13+ no AMRAP)
        0, 0, 0, 0, 0, 0, 0          // Block 3 (no AMRAP entered)
    };

    /// <summary>
    /// Expected Smith Squat TM values after each week (0-indexed: [0]=initial, [1]=after week 1, etc.).
    /// From A2S2_Validation_Data.md scenario summary.
    /// </summary>
    public static IReadOnlyList<decimal> SmithSquatExpectedTms { get; } = new[]
    {
        110.00m,   // Initial
        113.30m,   // After week 1: +5 → ×1.03
        114.43m,   // After week 2: +2 → ×1.01 (114.433 rounded to 2dp)
        115.58m,   // After week 3: +2 → ×1.01 (115.57733)
        116.73m,   // After week 4: +2 → ×1.01 (116.7331)
        117.90m,   // After week 5: +2 → ×1.01 (117.90043)
        118.49m,   // After week 6: +1 → ×1.005 (118.48993)
        118.49m,   // After week 7: DELOAD (no change)
        120.27m,   // After week 8: +3 → ×1.015 (120.26735)
        122.67m,   // After week 9: +4 → ×1.02 (122.6727)
        122.67m,   // After week 10: 0 → ×1.0 (no change)
        120.22m,   // After week 11: -1 → ×0.98 (120.2165)
        114.21m,   // After week 12: -3(≤-2) → ×0.95 (114.20568)
        114.21m,   // After week 13: no AMRAP
        114.21m,   // After week 14: DELOAD
        114.21m,   // After weeks 15-21: no AMRAP entered
        114.21m,
        114.21m,
        114.21m,
        114.21m,
        114.21m,
        114.21m,
    };
}
