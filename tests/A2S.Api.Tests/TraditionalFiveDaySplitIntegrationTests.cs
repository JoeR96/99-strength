using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Comprehensive integration tests for the Traditional Five-Day Split workout program.
///
/// Tests the complete workflow of creating a five-day split with the exact configuration
/// from the storybook (WorkoutDashboard.stories.tsx - FiveDayProgram story) and verifies
/// ALL exercise data is correctly persisted and retrieved.
///
/// Note: These tests run in a single test method because the current domain model
/// doesn't support multiple concurrent workouts (there's only one "active" workout at a time).
/// </summary>
[Collection("Integration")]
public class TraditionalFiveDaySplitIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public TraditionalFiveDaySplitIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        var uniqueUserId = Guid.NewGuid().ToString();
        var uniqueEmail = $"test-{uniqueUserId}@example.com";
        return _factory.CreateAuthenticatedClient(uniqueUserId, uniqueEmail);
    }

    /// <summary>
    /// Comprehensive test that creates the traditional five-day split and verifies ALL aspects:
    /// - Workout metadata
    /// - Exercise distribution across days
    /// - Exercise categories
    /// - Equipment types
    /// - Linear progression configuration
    /// - RepsPerSet progression configuration
    /// - Exercise order within days
    /// </summary>
    [Fact]
    public async Task CreateTraditionalFiveDaySplit_AllDataPersisted_AndRetrievedCorrectly()
    {
        // Arrange
        var client = CreateClient();
        var exercises = CreateTraditionalFiveDaySplitExercises();

        var command = new CreateWorkoutCommand(
            Name: "Traditional 5-Day Split Comprehensive Test",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        // Act - Create the workout
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert - Creation succeeded
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Workout creation failed: {await createResponse.Content.ReadAsStringAsync()}");

        // Act - Retrieve the workout
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();

        // ========================================
        // VERIFY: Workout Metadata
        // ========================================
        workout!.Name.Should().Be("Traditional 5-Day Split Comprehensive Test");
        workout.Variant.Should().Be("FiveDay");
        workout.TotalWeeks.Should().Be(21);
        workout.CurrentWeek.Should().Be(1);
        workout.Status.Should().Be("Active");
        workout.Exercises.Should().HaveCount(18, "Traditional 5-day split has 18 exercises");

        // ========================================
        // VERIFY: Exercise Distribution Across Days
        // ========================================
        var day1 = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();
        var day2 = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day2).ToList();
        var day3 = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day3).ToList();
        var day4 = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day4).ToList();
        var day5 = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day5).ToList();

        day1.Should().HaveCount(4, "Day 1 (Chest/Triceps) should have 4 exercises");
        day2.Should().HaveCount(4, "Day 2 (Back/Biceps) should have 4 exercises");
        day3.Should().HaveCount(4, "Day 3 (Legs) should have 4 exercises");
        day4.Should().HaveCount(3, "Day 4 (Shoulders/Arms) should have 3 exercises");
        day5.Should().HaveCount(3, "Day 5 (Full Body) should have 3 exercises");

        // ========================================
        // VERIFY: Exercise Categories
        // ========================================
        var mainLifts = workout.Exercises.Where(e => e.Category == ExerciseCategory.MainLift).ToList();
        var auxiliaries = workout.Exercises.Where(e => e.Category == ExerciseCategory.Auxiliary).ToList();
        var accessories = workout.Exercises.Where(e => e.Category == ExerciseCategory.Accessory).ToList();

        mainLifts.Should().HaveCount(5, "Should have 5 main lifts");
        mainLifts.Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Bench Press", "Deadlift", "Squat", "Overhead Press", "Front Squat" });

        auxiliaries.Should().HaveCount(5, "Should have 5 auxiliary exercises");
        accessories.Should().HaveCount(8, "Should have 8 accessory exercises");

        // ========================================
        // VERIFY: Equipment Types
        // ========================================
        var barbellExercises = workout.Exercises.Where(e => e.Equipment == EquipmentType.Barbell).ToList();
        var dumbbellExercises = workout.Exercises.Where(e => e.Equipment == EquipmentType.Dumbbell).ToList();
        var cableExercises = workout.Exercises.Where(e => e.Equipment == EquipmentType.Cable).ToList();
        var machineExercises = workout.Exercises.Where(e => e.Equipment == EquipmentType.Machine).ToList();

        barbellExercises.Should().HaveCount(8, "Should have 8 barbell exercises");
        dumbbellExercises.Should().HaveCount(3, "Should have 3 dumbbell exercises");
        cableExercises.Should().HaveCount(5, "Should have 5 cable exercises");
        machineExercises.Should().HaveCount(2, "Should have 2 machine exercises");

        // ========================================
        // VERIFY: Linear Progression Configuration
        // ========================================
        var linearExercises = workout.Exercises.Where(e => e.Progression.Type == "Linear").ToList();
        linearExercises.Should().HaveCount(7, "Should have 7 Linear progression exercises");

        // Main lifts with AMRAP
        VerifyLinearExercise(workout, "Bench Press", 90m, "Kilograms", useAmrap: true, sets: 4);
        VerifyLinearExercise(workout, "Deadlift", 140m, "Kilograms", useAmrap: true, sets: 4);
        VerifyLinearExercise(workout, "Squat", 110m, "Kilograms", useAmrap: true, sets: 4);
        VerifyLinearExercise(workout, "Overhead Press", 65m, "Kilograms", useAmrap: true, sets: 4);
        VerifyLinearExercise(workout, "Front Squat", 80m, "Kilograms", useAmrap: true, sets: 4);

        // Auxiliary lifts without AMRAP
        VerifyLinearExercise(workout, "Incline Bench Press", 70m, "Kilograms", useAmrap: false, sets: 4);
        VerifyLinearExercise(workout, "Barbell Row", 80m, "Kilograms", useAmrap: false, sets: 4);

        // ========================================
        // VERIFY: RepsPerSet Progression Configuration
        // ========================================
        var repsPerSetExercises = workout.Exercises.Where(e => e.Progression.Type == "RepsPerSet").ToList();
        repsPerSetExercises.Should().HaveCount(11, "Should have 11 RepsPerSet progression exercises");

        // Day 1 accessories
        VerifyRepsPerSetExercise(workout, "Tricep Extension", 20m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Lateral Raise", 10m, "Kilograms", sets: 3);

        // Day 2 accessories
        VerifyRepsPerSetExercise(workout, "Lat Pulldown", 50m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Bicep Curl", 15m, "Kilograms", sets: 3);

        // Day 3 exercises
        VerifyRepsPerSetExercise(workout, "Romanian Deadlift", 90m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Leg Press", 100m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Leg Curl", 40m, "Kilograms", sets: 3);

        // Day 4 accessories
        VerifyRepsPerSetExercise(workout, "Face Pull", 25m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Tricep Pushdown", 25m, "Kilograms", sets: 3);

        // Day 5 exercises
        VerifyRepsPerSetExercise(workout, "Dumbbell Row", 30m, "Kilograms", sets: 3);
        VerifyRepsPerSetExercise(workout, "Cable Crunch", 30m, "Kilograms", sets: 3);

        // ========================================
        // VERIFY: Exercise Order Within Days
        // ========================================
        // Day 1 order
        day1.OrderBy(e => e.OrderInDay).Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Bench Press", "Incline Bench Press", "Tricep Extension", "Lateral Raise" },
            options => options.WithStrictOrdering());

        // Day 2 order
        day2.OrderBy(e => e.OrderInDay).Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Deadlift", "Barbell Row", "Lat Pulldown", "Bicep Curl" },
            options => options.WithStrictOrdering());

        // Day 3 order
        day3.OrderBy(e => e.OrderInDay).Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Squat", "Romanian Deadlift", "Leg Press", "Leg Curl" },
            options => options.WithStrictOrdering());

        // Day 4 order
        day4.OrderBy(e => e.OrderInDay).Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Overhead Press", "Face Pull", "Tricep Pushdown" },
            options => options.WithStrictOrdering());

        // Day 5 order
        day5.OrderBy(e => e.OrderInDay).Select(e => e.Name).Should().BeEquivalentTo(
            new[] { "Front Squat", "Dumbbell Row", "Cable Crunch" },
            options => options.WithStrictOrdering());
    }

    #region Helper methods

    private void VerifyLinearExercise(WorkoutDto workout, string name, decimal expectedTm, string expectedUnit, bool useAmrap, int sets)
    {
        var exercise = workout.Exercises.First(e => e.Name == name);
        exercise.Progression.Type.Should().Be("Linear", $"{name} should have Linear progression");

        var linearProgression = exercise.Progression as LinearProgressionDto;
        linearProgression.Should().NotBeNull($"{name} should have LinearProgressionDto");
        linearProgression!.TrainingMax.Should().NotBeNull($"{name} should have TrainingMax");
        linearProgression.TrainingMax.Value.Should().Be(expectedTm, $"{name} TM should be {expectedTm}");
        var expectedUnitValue = expectedUnit == "Kilograms" ? 1 : 2; // Kilograms = 1, Pounds = 2
        linearProgression.TrainingMax.Unit.Should().Be(expectedUnitValue, $"{name} unit should be {expectedUnit}");
        linearProgression.UseAmrap.Should().Be(useAmrap, $"{name} AMRAP should be {useAmrap}");
        linearProgression.BaseSetsPerExercise.Should().Be(sets, $"{name} sets should be {sets}");
    }

    private void VerifyRepsPerSetExercise(WorkoutDto workout, string name, decimal expectedWeight, string expectedUnit, int sets)
    {
        var exercise = workout.Exercises.First(e => e.Name == name);
        exercise.Progression.Type.Should().Be("RepsPerSet", $"{name} should have RepsPerSet progression");

        var repsPerSetProgression = exercise.Progression as RepsPerSetProgressionDto;
        repsPerSetProgression.Should().NotBeNull($"{name} should have RepsPerSetProgressionDto");
        repsPerSetProgression!.CurrentWeight.Should().Be(expectedWeight, $"{name} weight should be {expectedWeight}");
        repsPerSetProgression.WeightUnit.Should().Be(expectedUnit, $"{name} unit should be {expectedUnit}");
        repsPerSetProgression.CurrentSetCount.Should().Be(sets, $"{name} sets should be {sets}");
    }

    /// <summary>
    /// Creates the complete list of 18 exercises for the traditional 5-day split.
    /// Matches the storybook configuration exactly.
    /// </summary>
    private static List<CreateExerciseRequest> CreateTraditionalFiveDaySplitExercises()
    {
        return new List<CreateExerciseRequest>
        {
            // Day 1: Chest/Triceps (4 exercises)
            new()
            {
                TemplateName = "Bench Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 90m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Incline Bench Press",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                TrainingMaxValue = 70m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Tricep Extension",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 3,
                StartingWeight = 20m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Lateral Raise",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 4,
                StartingWeight = 10m,
                WeightUnit = WeightUnit.Kilograms
            },

            // Day 2: Back/Biceps (4 exercises)
            new()
            {
                TemplateName = "Deadlift",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Barbell Row",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 2,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Lat Pulldown",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 3,
                StartingWeight = 50m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bicep Curl",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 4,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms
            },

            // Day 3: Legs (4 exercises)
            new()
            {
                TemplateName = "Squat",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                TrainingMaxValue = 110m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Romanian Deadlift",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 2,
                StartingWeight = 90m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Leg Press",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 3,
                StartingWeight = 100m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Leg Curl",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 4,
                StartingWeight = 40m,
                WeightUnit = WeightUnit.Kilograms
            },

            // Day 4: Shoulders/Arms (3 exercises)
            new()
            {
                TemplateName = "Overhead Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                TrainingMaxValue = 65m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Face Pull",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 2,
                StartingWeight = 25m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Tricep Pushdown",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 3,
                StartingWeight = 25m,
                WeightUnit = WeightUnit.Kilograms
            },

            // Day 5: Full Body (3 exercises)
            new()
            {
                TemplateName = "Front Squat",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day5,
                OrderInDay = 1,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Dumbbell Row",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day5,
                OrderInDay = 2,
                StartingWeight = 30m,
                WeightUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Cable Crunch",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day5,
                OrderInDay = 3,
                StartingWeight = 30m,
                WeightUnit = WeightUnit.Kilograms
            }
        };
    }

    #endregion
}
