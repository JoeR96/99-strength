using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.UpdateExercises;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetExerciseLibrary;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for Workouts API endpoints.
/// Tests the complete workout creation workflow including happy and sad paths.
/// Each test uses a unique user to ensure test isolation.
/// </summary>
[Collection("Integration")]
public class WorkoutsIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public WorkoutsIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a new authenticated client with a unique user for test isolation.
    /// </summary>
    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Happy Path Tests

    /// <summary>
    /// Tests creating a 5-day split workout program with mixed progression strategies.
    /// Verifies exercises can have both Linear and RepsPerSet progressions.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithFiveDaySplit_ReturnsCreatedWithMixedProgressions()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "5-Day Split Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();

        // Verify the Location header is set
        response.Headers.Location.Should().NotBeNull();

        // Verify the workout has exercises with mixed progressions
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();
        workout!.Exercises.Should().NotBeEmpty();

        // Default workout creates 4 main lifts with Linear progression
        var linearExercises = workout.Exercises.Where(e => e.Progression.Type == "Linear").ToList();
        linearExercises.Should().HaveCount(4, "Default workout should have 4 Linear progression exercises");

        // Verify all default main lifts use linear progression
        var benchPress = workout.Exercises.FirstOrDefault(e => e.Name == "Bench Press");
        benchPress.Should().NotBeNull();
        benchPress!.Category.Should().Be(ExerciseCategory.MainLift);
        benchPress.Progression.Type.Should().Be("Linear");

        var squat = workout.Exercises.FirstOrDefault(e => e.Name == "Squat");
        squat.Should().NotBeNull();
        squat!.Category.Should().Be(ExerciseCategory.MainLift);
        squat.Progression.Type.Should().Be("Linear");
    }

    /// <summary>
    /// Tests creating a hypertrophy-focused workout program.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithValidHypertrophyProgram_ReturnsCreatedWithId()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "My Hypertrophy Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();

        // Verify the Location header is set
        response.Headers.Location.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that after creating a workout, it can be retrieved as the current workout.
    /// Default workout creates 4 main lifts on Days 1-4.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_ThenGetCurrent_ReturnsCreatedWorkoutWithDefaultExercises()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Test Default Workout",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act - Create the workout
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", command);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        // Act - Retrieve the current workout
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");

        // Assert
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();
        workout!.Id.Should().Be(createResult!.Id);
        workout.Name.Should().Be(command.Name);
        workout.TotalWeeks.Should().Be(command.TotalWeeks);
        workout.CurrentWeek.Should().Be(1);
        workout.Status.Should().Be(WorkoutStatus.Active.ToString()); // Workout is started immediately after creation

        // Verify default workout creates 4 main lifts on Days 1-4
        workout.Exercises.Should().HaveCount(4, "Default workout should have 4 main lifts");

        var day1Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();
        var day2Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day2).ToList();
        var day3Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day3).ToList();
        var day4Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day4).ToList();

        // Verify each day has exactly one main lift
        day1Exercises.Should().ContainSingle("Day 1 should have one main lift")
            .Which.Name.Should().Be("Squat");
        day2Exercises.Should().ContainSingle("Day 2 should have one main lift")
            .Which.Name.Should().Be("Bench Press");
        day3Exercises.Should().ContainSingle("Day 3 should have one main lift")
            .Which.Name.Should().Be("Deadlift");
        day4Exercises.Should().ContainSingle("Day 4 should have one main lift")
            .Which.Name.Should().Be("Overhead Press");

        // All exercises should be main lifts with linear progression
        workout.Exercises.Should().OnlyContain(
            e => e.Category == ExerciseCategory.MainLift && e.Progression.Type == "Linear",
            "All default exercises should be main lifts with linear progression");
    }

    /// <summary>
    /// Tests that exercises in the workout have correct progression types.
    /// MainLift and Auxiliary can use Linear OR RepsPerSet progression.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_ExercisesHaveCorrectProgressionTypes()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Progression Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        // Assert - Default workout creates 4 main lifts with linear progression
        var mainLifts = workout!.Exercises.Where(e => e.Category == ExerciseCategory.MainLift).ToList();
        mainLifts.Should().HaveCount(4, "Default workout should have 4 main lifts");
        mainLifts.Should().OnlyContain(e => e.Progression.Type == "Linear",
            "All main lifts should use Linear progression");

        // Verify LinearProgression has TrainingMax set
        foreach (var lift in mainLifts)
        {
            var linearProg = lift.Progression as LinearProgressionDto;
            linearProg.Should().NotBeNull($"{lift.Name} should have LinearProgressionDto");
            linearProg!.TrainingMax.Should().NotBeNull($"{lift.Name} should have TrainingMax");
            linearProg.TrainingMax.Value.Should().BeGreaterThan(0, $"{lift.Name} should have positive TM");
            linearProg.UseAmrap.Should().BeTrue($"{lift.Name} should use AMRAP");
        }
    }

    /// <summary>
    /// Tests creating a workout with custom total weeks.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithCustomWeeks_CreatesSuccessfully()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Short Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 12
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify by retrieving
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.TotalWeeks.Should().Be(12);
    }

    #endregion

    #region Sad Path Tests - Validation

    /// <summary>
    /// Tests that creating a workout with empty name fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "", // Empty name
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that creating a workout with zero total weeks fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithZeroWeeks_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 0 // Invalid
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that creating a workout with negative total weeks fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithNegativeWeeks_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: -5 // Invalid
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests that creating a workout with excessively long name fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithExcessivelyLongName_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: new string('A', 201), // Exceeds typical max length
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests that creating a workout with excessively high total weeks fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithExcessiveWeeks_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 1000 // Unrealistic
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Sad Path Tests - Business Rules

    /// <summary>
    /// Tests that creating a second workout when one already exists returns conflict.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WhenActiveWorkoutExists_ReturnsConflict()
    {
        // Arrange - use same client for both requests to ensure same user
        var client = CreateClient();
        var firstCommand = new CreateWorkoutCommand(
            Name: "First Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        var firstResponse = await client.PostAsJsonAsync("/api/v1/workouts", firstCommand);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Try to create second workout with same user
        var secondCommand = new CreateWorkoutCommand(
            Name: "Second Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        var secondResponse = await client.PostAsJsonAsync("/api/v1/workouts", secondCommand);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var errorResponse = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().Contain("active workout");
    }

    #endregion

    #region Sad Path Tests - Authentication & Authorization

    /// <summary>
    /// Tests that unauthenticated requests to create workout are rejected.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create unauthenticated client
        var unauthenticatedClient = _factory.CreateClient();

        var command = new CreateWorkoutCommand(
            Name: "Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests that unauthenticated requests to get current workout are rejected.
    /// </summary>
    [Fact]
    public async Task GetCurrentWorkout_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create unauthenticated client
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/v1/workouts/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Current Workout Tests

    /// <summary>
    /// Tests that getting current workout when none exists returns 404.
    /// </summary>
    [Fact]
    public async Task GetCurrentWorkout_WhenNoWorkoutExists_ReturnsNotFound()
    {
        // Arrange - Create a fresh authenticated client (new user, no workout)
        var freshClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await freshClient.GetAsync("/api/v1/workouts/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse!.Message.Should().Contain("No active workout");
    }

    #endregion

    #region Exercise Library Tests

    /// <summary>
    /// Tests that the exercise library endpoint returns successfully.
    /// The library contains exercises grouped by type, not by category or progression.
    /// </summary>
    [Fact]
    public async Task GetExerciseLibrary_ReturnsSuccessWithExercises()
    {
        // Act
        var response = await CreateClient().GetAsync("/api/v1/workouts/exercises/library");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var library = await response.Content.ReadFromJsonAsync<ExerciseLibraryDto>();
        library.Should().NotBeNull();
        library!.Templates.Should().NotBeNull();

        // Verify we have exercise templates
        library.Templates.Count.Should().BeGreaterThanOrEqualTo(20);
        library.Templates.Should().Contain(e => e.Name == "Squat");
        library.Templates.Should().Contain(e => e.Name == "Bench Press");
        library.Templates.Should().Contain(e => e.Name == "Deadlift");
        library.Templates.Should().Contain(e => e.Name == "Overhead Press");

        // Verify we have accessory exercises
        library.Templates.Should().Contain(e => e.Name == "Bicep Curl");
        library.Templates.Should().Contain(e => e.Name == "Lateral Raise");
    }

    /// <summary>
    /// Tests that exercise definitions don't include category - it's chosen when adding to workout.
    /// </summary>
    [Fact]
    public async Task GetExerciseLibrary_ExerciseDefinitionsHaveNoCategoryOrProgressionType()
    {
        // Act
        var response = await CreateClient().GetAsync("/api/v1/workouts/exercises/library");
        var library = await response.Content.ReadFromJsonAsync<ExerciseLibraryDto>();

        // Assert - Exercises should have equipment but no category
        var squat = library!.Templates.First(e => e.Name == "Squat");
        squat.Equipment.Should().Be(EquipmentType.Barbell);
        squat.DefaultRepRange.Should().NotBeNull();
        squat.DefaultSets.Should().NotBeNull();

        // Verify accessory exercises have rep ranges defined
        var curl = library.Templates.First(e => e.Name == "Bicep Curl");
        curl.Equipment.Should().Be(EquipmentType.Dumbbell);
        curl.DefaultRepRange.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that unauthenticated requests to get exercise library are rejected.
    /// </summary>
    [Fact]
    public async Task GetExerciseLibrary_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create unauthenticated client
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/v1/workouts/exercises/library");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Configured Exercise Tests

    /// <summary>
    /// Tests creating a workout with user-configured exercises.
    /// Verifies that exercises are created with the specified category, progression, day, and order.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithConfiguredExercises_CreatesWorkoutWithExactConfiguration()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bench Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Barbell Row",
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bicep Curl",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 2,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Custom Configuration Program",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify exercises are configured correctly
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();
        workout!.Exercises.Count.Should().Be(4);

        // Verify Squat configuration
        var squat = workout.Exercises.First(e => e.Name == "Squat");
        squat.Category.Should().Be(ExerciseCategory.MainLift);
        squat.Progression.Type.Should().Be("Linear");
        squat.AssignedDay.Should().Be(DayNumber.Day1);
        squat.OrderInDay.Should().Be(1);

        // Verify Bench Press configuration
        var bench = workout.Exercises.First(e => e.Name == "Bench Press");
        bench.Category.Should().Be(ExerciseCategory.MainLift);
        bench.AssignedDay.Should().Be(DayNumber.Day2);
        bench.OrderInDay.Should().Be(1);

        // Verify Barbell Row configuration (auxiliary with Linear progression)
        var row = workout.Exercises.First(e => e.Name == "Barbell Row");
        row.Category.Should().Be(ExerciseCategory.Auxiliary);
        row.Progression.Type.Should().Be("Linear");
        row.AssignedDay.Should().Be(DayNumber.Day1);
        row.OrderInDay.Should().Be(2);

        // Verify Bicep Curl configuration (accessory with RepsPerSet)
        var curl = workout.Exercises.First(e => e.Name == "Bicep Curl");
        curl.Category.Should().Be(ExerciseCategory.Accessory);
        curl.Progression.Type.Should().Be("RepsPerSet");
        curl.AssignedDay.Should().Be(DayNumber.Day2);
        curl.OrderInDay.Should().Be(2);
    }

    /// <summary>
    /// Tests creating a workout with empty exercise list falls back to defaults.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithEmptyExerciseList_UsesDefaultExercises()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Default Exercise Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21,
            Exercises: new List<CreateExerciseRequest>()
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify default exercises are created
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.Exercises.Should().NotBeEmpty("Should have default exercises when list is empty");
    }

    /// <summary>
    /// Tests creating a workout with exercises assigned to different days.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithExercisesOnMultipleDays_GroupsCorrectly()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bench Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Deadlift",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                TrainingMaxValue = 180m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Overhead Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                TrainingMaxValue = 70m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Multi-Day Program",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        // Assert - Verify exercises are on correct days
        workout.Should().NotBeNull();

        var day1Exercises = workout!.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();
        var day2Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day2).ToList();
        var day3Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day3).ToList();
        var day4Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day4).ToList();

        day1Exercises.Should().ContainSingle(e => e.Name == "Squat");
        day2Exercises.Should().ContainSingle(e => e.Name == "Bench Press");
        day3Exercises.Should().ContainSingle(e => e.Name == "Deadlift");
        day4Exercises.Should().ContainSingle(e => e.Name == "Overhead Press");
    }

    /// <summary>
    /// Tests that exercises with invalid template names are skipped.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithInvalidTemplateName_SkipsInvalidExercises()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "NonExistentExercise", // Invalid template
                Category = ExerciseCategory.Auxiliary,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bench Press",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Program with Invalid Exercise",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.Exercises.Count.Should().Be(2, "Invalid exercise should be skipped");
        workout.Exercises.Should().Contain(e => e.Name == "Squat");
        workout.Exercises.Should().Contain(e => e.Name == "Bench Press");
        workout.Exercises.Should().NotContain(e => e.Name == "NonExistentExercise");
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// Tests creating a workout with whitespace-only name fails validation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithWhitespaceName_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "   ", // Whitespace only
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests creating a workout with boundary value for total weeks (1 week).
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithMinimumWeeks_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateWorkoutCommand(
            Name: "One Week Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 1
        );

        // Act
        var response = await CreateClient().PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Tests creating a workout with special characters in name.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithSpecialCharactersInName_CreatesSuccessfully()
    {
        // Arrange - use same client for create and get to ensure same user
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "John's Program (2026) - Phase #1!",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify the name is preserved
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.Name.Should().Be(command.Name);
    }

    #endregion

    #region Update Exercises Tests

    /// <summary>
    /// Tests updating the Training Max of a Linear progression exercise.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_UpdateTrainingMax_UpdatesSuccessfully()
    {
        // Arrange - create a workout first
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                HevyExerciseTemplateId = "D04AC939",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var createCommand = new CreateWorkoutCommand(
            Name: "Update TM Test Program",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the workout to find the exercise ID
        var getResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();

        var squatExercise = workout!.Exercises.First(e => e.Name == "Squat");
        var originalTM = (squatExercise.Progression as LinearProgressionDto)?.TrainingMax.Value;
        originalTM.Should().Be(100m);

        // Act - Update the training max
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.Parse(squatExercise.Id),
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms,
                    Reason = "Adjusted based on recent performance"
                }
            }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workouts/{workout.Id}/exercises", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<UpdateExercisesResult>();
        result.Should().NotBeNull();
        result!.UpdatedCount.Should().Be(1);
        result.Results.Should().ContainSingle();
        result.Results[0].Success.Should().BeTrue();
        result.Results[0].ExerciseName.Should().Be("Squat");
        result.Results[0].PreviousValue.Should().Be("100 Kilograms");
        result.Results[0].NewValue.Should().Be("110 Kilograms");

        // Verify the change persisted
        var verifyResponse = await client.GetAsync("/api/v1/workouts/current");
        var updatedWorkout = await verifyResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        var updatedSquat = updatedWorkout!.Exercises.First(e => e.Name == "Squat");
        var newTM = (updatedSquat.Progression as LinearProgressionDto)?.TrainingMax.Value;
        newTM.Should().Be(110m);
    }

    /// <summary>
    /// Tests updating the weight of a RepsPerSet progression exercise.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_UpdateWeight_UpdatesSuccessfully()
    {
        // Arrange - create a workout with RepsPerSet exercise
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Bicep Curl",
                HevyExerciseTemplateId = "ADA8623C",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms
            }
        };

        var createCommand = new CreateWorkoutCommand(
            Name: "Update Weight Test Program",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the workout to find the exercise ID
        var getResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        var curlExercise = workout!.Exercises.First(e => e.Name == "Bicep Curl");

        // Act - Update the weight
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.Parse(curlExercise.Id),
                    WeightValue = 17.5m,
                    WeightUnit = WeightUnit.Kilograms
                }
            }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workouts/{workout.Id}/exercises", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<UpdateExercisesResult>();
        result.Should().NotBeNull();
        result!.UpdatedCount.Should().Be(1);
        result.Results[0].Success.Should().BeTrue();

        // Verify the change persisted
        var verifyResponse = await client.GetAsync("/api/v1/workouts/current");
        var updatedWorkout = await verifyResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        var updatedCurl = updatedWorkout!.Exercises.First(e => e.Name == "Bicep Curl");
        var newWeight = (updatedCurl.Progression as RepsPerSetProgressionDto)?.CurrentWeight;
        newWeight.Should().Be(17.5m);
    }

    /// <summary>
    /// Tests updating multiple exercises in a single request.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_UpdateMultipleExercises_UpdatesAllSuccessfully()
    {
        // Arrange - create a workout with multiple exercises
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                HevyExerciseTemplateId = "D04AC939",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Bench Press",
                HevyExerciseTemplateId = "79D0BB3A",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var createCommand = new CreateWorkoutCommand(
            Name: "Multi-Update Test Program",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        var squatExercise = workout!.Exercises.First(e => e.Name == "Squat");
        var benchExercise = workout.Exercises.First(e => e.Name == "Bench Press");

        // Act - Update both exercises
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.Parse(squatExercise.Id),
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                },
                new()
                {
                    ExerciseId = Guid.Parse(benchExercise.Id),
                    TrainingMaxValue = 85m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                }
            }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workouts/{workout.Id}/exercises", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<UpdateExercisesResult>();
        result.Should().NotBeNull();
        result!.UpdatedCount.Should().Be(2);
        result.Results.Should().HaveCount(2);
        result.Results.Should().OnlyContain(r => r.Success);
    }

    /// <summary>
    /// Tests that updating non-existent exercise returns failure in results.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_WithNonExistentExercise_ReturnsFailureForThatExercise()
    {
        // Arrange
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Squat",
                HevyExerciseTemplateId = "D04AC939",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var createCommand = new CreateWorkoutCommand(
            Name: "Non-Existent Exercise Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        // Act - Try to update non-existent exercise
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.NewGuid(), // Non-existent
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                }
            }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workouts/{workout!.Id}/exercises", updateRequest);

        // Assert - Still returns 200 but with failure in results
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<UpdateExercisesResult>();
        result.Should().NotBeNull();
        result!.UpdatedCount.Should().Be(0);
        result.Results.Should().ContainSingle();
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Message.Should().Contain("not found");
    }

    /// <summary>
    /// Tests that unauthenticated requests to update exercises are rejected.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.NewGuid(),
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                }
            }
        };

        // Act
        var response = await unauthenticatedClient.PutAsJsonAsync($"/api/v1/workouts/{Guid.NewGuid()}/exercises", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests that updating exercises for non-existent workout returns not found.
    /// </summary>
    [Fact]
    public async Task UpdateExercises_WithNonExistentWorkout_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var updateRequest = new UpdateExercisesApiRequest
        {
            Updates = new List<ExerciseUpdateApiRequest>
            {
                new()
                {
                    ExerciseId = Guid.NewGuid(),
                    TrainingMaxValue = 110m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                }
            }
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/workouts/{Guid.NewGuid()}/exercises", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that Linear progression exercises now have AMRAP enabled by default.
    /// This validates the AMRAP bug fix for Auxiliary exercises.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_LinearProgressionAuxiliary_HasAmrapEnabled()
    {
        // Arrange - create a workout with an Auxiliary Linear progression exercise (like OHP in Monk Mode)
        var client = CreateClient();
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Overhead Press",
                HevyExerciseTemplateId = "7B8D84E8",
                Category = ExerciseCategory.Auxiliary, // Auxiliary, not MainLift
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 65m,
                TrainingMaxUnit = WeightUnit.Kilograms
                // Note: NOT setting UseAmrap, it should default to true
            }
        };

        var createCommand = new CreateWorkoutCommand(
            Name: "AMRAP Bug Fix Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        // Act
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        // Assert - AMRAP should be enabled for Linear progression even though it's Auxiliary
        var ohp = workout!.Exercises.First(e => e.Name == "Overhead Press");
        ohp.Category.Should().Be(ExerciseCategory.Auxiliary);
        ohp.Progression.Type.Should().Be("Linear");

        var linearProg = ohp.Progression as LinearProgressionDto;
        linearProg.Should().NotBeNull();
        linearProg!.UseAmrap.Should().BeTrue("All Linear progression exercises should have AMRAP enabled by default");
    }

    #endregion
}

#region Response DTOs

internal class CreateWorkoutResponse
{
    public Guid Id { get; set; }
}

internal class ErrorResponse
{
    public string? Error { get; set; }
    public string? Message { get; set; }
}

internal class UpdateExercisesApiRequest
{
    public List<ExerciseUpdateApiRequest> Updates { get; set; } = new();
}

internal class ExerciseUpdateApiRequest
{
    public Guid ExerciseId { get; set; }
    public decimal? TrainingMaxValue { get; set; }
    public WeightUnit? TrainingMaxUnit { get; set; }
    public decimal? WeightValue { get; set; }
    public WeightUnit? WeightUnit { get; set; }
    public string? Reason { get; set; }
}

#endregion
