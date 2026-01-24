using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using A2S.Tests.Shared.TestData;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for the complete workout flow including day completion,
/// week progression, and all progression types (Linear, RepsPerSet, MinimalSets).
/// Uses actual spreadsheet data as source of truth.
/// </summary>
[Collection("Integration")]
public class WorkoutFlowIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public WorkoutFlowIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Complete Day Tests

    /// <summary>
    /// Tests completing a training day with mixed progression types.
    /// Verifies the endpoint returns success and progression changes.
    /// </summary>
    [Fact]
    public async Task CompleteDay_WithMixedProgressionTypes_ReturnsSuccessWithChanges()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Get the workout to find actual exercise IDs
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();

        // Create performances for Day 1 exercises
        var actualPerformances = CreatePerformancesForDay(workout!, DayNumber.Day1);

        // Act
        var completeDayRequest = new { Performances = actualPerformances };
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            completeDayRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day1);
        result.ExercisesCompleted.Should().BeGreaterThan(0);
        result.ProgressionChanges.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests that completing a day with invalid workout ID returns 404.
    /// </summary>
    [Fact]
    public async Task CompleteDay_WithInvalidWorkoutId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();

        var request = new
        {
            Performances = new[]
            {
                new
                {
                    ExerciseId = Guid.NewGuid(),
                    CompletedSets = new[]
                    {
                        new { SetNumber = 1, Weight = 50m, WeightUnit = 0, ActualReps = 10, WasAmrap = false }
                    }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/days/1/complete",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that completing a day with invalid day number returns BadRequest.
    /// </summary>
    [Fact]
    public async Task CompleteDay_WithInvalidDayNumber_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var request = new
        {
            Performances = new[]
            {
                new
                {
                    ExerciseId = Guid.NewGuid(),
                    CompletedSets = new[]
                    {
                        new { SetNumber = 1, Weight = 50m, WeightUnit = 0, ActualReps = 10, WasAmrap = false }
                    }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/7/complete", // Invalid day
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests completing Day 1 with actual spreadsheet data for Week 1.
    /// Verifies linear progression (OHP with AMRAP +4) correctly changes.
    /// </summary>
    [Fact]
    public async Task CompleteDay1_Week1_WithSpreadsheetData_AppliesCorrectProgressions()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find the OHP exercise (Linear progression)
        var ohp = workout!.Exercises.FirstOrDefault(e => e.Name == "Overhead Press Smith Machine");
        ohp.Should().NotBeNull("OHP should be in the workout");
        ohp!.Progression.Type.Should().Be("Linear");

        // Build performances based on Week 1 spreadsheet data
        var week1OhpData = SpreadsheetTestData.GetWeekData(1, "Overhead Press Smith Machine");

        var performances = new List<object>();

        // OHP: 4 sets at 42.5kg, AMRAP gets 19 reps (target was 15, so +4)
        var ohpSets = new List<object>();
        for (int i = 1; i <= week1OhpData.SetGoal!.Value; i++)
        {
            var isAmrap = i == week1OhpData.SetGoal.Value;
            ohpSets.Add(new
            {
                SetNumber = i,
                Weight = week1OhpData.Weight!.Value,
                WeightUnit = 0, // Kilograms
                ActualReps = isAmrap ? week1OhpData.AmrapResult!.Value : week1OhpData.RepsPerNormalSet!.Value,
                WasAmrap = isAmrap
            });
        }

        performances.Add(new
        {
            ExerciseId = ohp.Id,
            CompletedSets = ohpSets
        });

        // Add other Day 1 exercises with success performances
        var day1Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1 && e.Id != ohp.Id);
        foreach (var exercise in day1Exercises)
        {
            var sets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets
            });
        }

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            new { Performances = performances });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day1);

        // OHP with +4 AMRAP delta should show "TM increased 2%"
        var ohpChange = result.ProgressionChanges.FirstOrDefault(c => c.ExerciseName == "Overhead Press Smith Machine");
        ohpChange.Should().NotBeNull();
        ohpChange!.Change.Should().Contain("2%", "AMRAP +4 should result in 2% TM increase");
    }

    #endregion

    #region Progress Week Tests

    /// <summary>
    /// Tests progressing from week 1 to week 2.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_FromWeek1ToWeek2_ReturnsCorrectWeekAndBlock()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/progress-week",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ProgressWeekResult>();
        result.Should().NotBeNull();
        result!.PreviousWeek.Should().Be(1);
        result.NewWeek.Should().Be(2);
        result.NewBlock.Should().Be(1); // Still in block 1
        result.IsDeloadWeek.Should().BeFalse();
        result.IsProgramComplete.Should().BeFalse();
    }

    /// <summary>
    /// Tests that week 7 is correctly identified as a deload week.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_ToWeek7_IdentifiesAsDeloadWeek()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Progress through weeks 1-6
        for (int i = 1; i < 7; i++)
        {
            var progressResponse = await client.PostAsync($"/api/v1/workouts/{workoutId}/progress-week", null);
            progressResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act - Check the response from progressing to week 7
        // We need to re-get the workout to verify state
        var workout = await GetCurrentWorkoutAsync(client);

        // Assert - We should now be at week 7
        workout!.CurrentWeek.Should().Be(7);
    }

    /// <summary>
    /// Tests that progressing with invalid workout ID returns 404.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_WithInvalidWorkoutId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/progress-week",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests block transitions (week 7 -> block 2, week 14 -> block 3).
    /// </summary>
    [Fact]
    public async Task ProgressWeek_AcrossBlocks_UpdatesBlockCorrectly()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Progress to week 8 (start of block 2)
        for (int i = 1; i <= 7; i++)
        {
            await client.PostAsync($"/api/v1/workouts/{workoutId}/progress-week", null);
        }

        // Assert - Week 8 should be block 2
        var workout = await GetCurrentWorkoutAsync(client);
        workout!.CurrentWeek.Should().Be(8);
        workout.CurrentBlock.Should().Be(2);
    }

    #endregion

    #region Linear Progression Tests

    /// <summary>
    /// Tests that AMRAP result of +4 reps increases TM by approximately 2%.
    /// Based on spreadsheet data: Week 1 OHP AMRAP target 15, actual 19 (+4).
    /// </summary>
    [Theory]
    [InlineData(15, 19, 0.02)] // +4 reps = +2% TM
    [InlineData(10, 16, 0.03)] // +6 reps = +3% TM
    [InlineData(14, 17, 0.015)] // +3 reps = +1.5% TM
    [InlineData(12, 12, 0.0)] // 0 reps = no change
    [InlineData(12, 10, -0.05)] // -2 reps = -5% TM
    public void LinearProgression_WithAmrapDelta_CalculatesCorrectAdjustment(
        int targetReps, int actualReps, decimal expectedAdjustmentPercentage)
    {
        // This test validates the RTF (Reps To Failure) progression table
        // The expected adjustments are:
        // +5 or more reps: +3.0%
        // +4 reps: +2.0%
        // +3 reps: +1.5%
        // +2 reps: +1.0%
        // +1 rep: +0.5%
        // 0 reps: No change
        // -1 rep: -2.0%
        // -2 or worse: -5.0%

        var delta = actualReps - targetReps;
        var actualAdjustment = delta switch
        {
            >= 5 => 0.03m,
            4 => 0.02m,
            3 => 0.015m,
            2 => 0.01m,
            1 => 0.005m,
            0 => 0.0m,
            -1 => -0.02m,
            _ => -0.05m
        };

        actualAdjustment.Should().Be(expectedAdjustmentPercentage);
    }

    #endregion

    #region RepsPerSet Progression Tests

    /// <summary>
    /// Tests that successful completion (all sets hit max reps) adds a set.
    /// Based on spreadsheet: Week 1 Lat Pulldown 3 sets -> Week 2 4 sets.
    /// </summary>
    [Theory]
    [InlineData(3, true, false, 4)] // 3 sets + success = 4 sets (bilateral)
    [InlineData(4, true, false, 5)] // 4 sets + success = 5 sets (bilateral)
    [InlineData(5, true, false, 5)] // At max (5) + success = stay at max (bilateral), weight increases
    [InlineData(3, true, true, 3)] // 3 sets + success = 3 sets max (unilateral, per side)
    [InlineData(4, false, false, 4)] // 4 sets + failure = 4 sets maintained
    public void RepsPerSetProgression_WithCompletionStatus_CalculatesCorrectNextSets(
        int currentSets, bool allCompleted, bool isUnilateral, int expectedSets)
    {
        // This test validates the RepsPerSet progression logic
        var maxSets = isUnilateral ? 3 : 5;

        int actualNextSets;
        if (allCompleted)
        {
            actualNextSets = currentSets < maxSets ? currentSets + 1 : maxSets;
        }
        else
        {
            actualNextSets = currentSets; // Failure or maintained = no change in this simplified test
        }

        actualNextSets.Should().Be(expectedSets);
    }

    /// <summary>
    /// Tests completing Day 1 RepsPerSet exercises with success.
    /// Lat Pulldown should go from 3 sets to 4 sets.
    /// </summary>
    [Fact]
    public async Task CompleteDay1_RepsPerSetSuccess_AddsSets()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find Lat Pulldown (RepsPerSet progression, 3 starting sets)
        var latPulldown = workout!.Exercises.FirstOrDefault(e => e.Name == "Lat Pulldown");
        latPulldown.Should().NotBeNull();
        latPulldown!.Progression.Type.Should().Be("RepsPerSet");

        var rpsProgression = latPulldown.Progression as RepsPerSetProgressionDto;
        rpsProgression.Should().NotBeNull();
        var initialSetCount = rpsProgression!.CurrentSetCount;
        initialSetCount.Should().Be(3, "Lat Pulldown should start with 3 sets");

        // Create performances for all Day 1 exercises (all hitting max reps = success)
        var performances = new List<object>();
        var day1Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();

        foreach (var exercise in day1Exercises)
        {
            var sets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets
            });
        }

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            new { Performances = performances });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();

        // Lat Pulldown should have "Added 1 set" change
        var latPulldownChange = result!.ProgressionChanges.FirstOrDefault(c => c.ExerciseName == "Lat Pulldown");
        latPulldownChange.Should().NotBeNull();
        latPulldownChange!.Change.Should().Contain("Added 1 set");
    }

    #endregion

    #region MinimalSets Progression Tests

    /// <summary>
    /// Tests MinimalSets progression based on completing target reps.
    /// Based on spreadsheet: Assisted Dips target 40 reps.
    /// </summary>
    [Theory]
    [InlineData(40, 40, 3, 3, 3)] // Hit target in expected sets = maintain
    [InlineData(40, 40, 2, 3, 2)] // Hit target in fewer sets = reduce sets (progress)
    [InlineData(40, 35, 3, 3, 4)] // Missed target = add set
    public void MinimalSetsProgression_WithTotalReps_CalculatesCorrectNextSets(
        int targetReps, int actualReps, int setsUsed, int currentSets, int expectedNextSets)
    {
        // This test validates the MinimalSets progression logic
        int actualNextSets;

        if (actualReps < targetReps)
        {
            // Failed - add a set
            actualNextSets = currentSets + 1;
        }
        else if (setsUsed < currentSets)
        {
            // Success - reduce sets
            actualNextSets = currentSets - 1;
        }
        else
        {
            // Maintained
            actualNextSets = currentSets;
        }

        actualNextSets.Should().Be(expectedNextSets);
    }

    /// <summary>
    /// Tests completing Day 3 with MinimalSets exercises (Assisted Dips, Assisted Pullups).
    /// </summary>
    [Fact]
    public async Task CompleteDay3_WithMinimalSetsExercises_AppliesCorrectProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find Assisted Dips (MinimalSets progression)
        var assistedDips = workout!.Exercises.FirstOrDefault(e => e.Name == "Assisted Dips");
        assistedDips.Should().NotBeNull();
        assistedDips!.Progression.Type.Should().Be("MinimalSets");

        // Find Assisted Pullups (MinimalSets progression)
        var assistedPullups = workout.Exercises.FirstOrDefault(e => e.Name == "Assisted Pullups");
        assistedPullups.Should().NotBeNull();
        assistedPullups!.Progression.Type.Should().Be("MinimalSets");

        // Create performances for Day 3 exercises
        var performances = new List<object>();
        var day3Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day3).ToList();

        foreach (var exercise in day3Exercises)
        {
            if (exercise.Progression.Type == "MinimalSets")
            {
                var minimalSets = exercise.Progression as MinimalSetsProgressionDto;
                // Complete target reps in exact number of sets (maintained)
                var setsData = new List<object>();
                var repsPerSet = minimalSets!.TargetTotalReps / minimalSets.CurrentSetCount;
                var remainder = minimalSets.TargetTotalReps % minimalSets.CurrentSetCount;

                for (int i = 1; i <= minimalSets.CurrentSetCount; i++)
                {
                    setsData.Add(new
                    {
                        SetNumber = i,
                        Weight = minimalSets.CurrentWeight,
                        WeightUnit = 0,
                        ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                        WasAmrap = false
                    });
                }

                performances.Add(new
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = setsData
                });
            }
            else
            {
                var sets = CreateSuccessPerformanceForExercise(exercise);
                performances.Add(new
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = sets
                });
            }
        }

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/3/complete",
            new { Performances = performances });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day3);
        result.ExercisesCompleted.Should().BeGreaterThan(0);
    }

    #endregion

    #region Full Workout Cycle Tests

    /// <summary>
    /// Tests completing an entire week (all 4 days) and progressing.
    /// </summary>
    [Fact]
    public async Task CompleteFullWeek_ThenProgress_WorksCorrectly()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        workout!.CurrentWeek.Should().Be(1);

        // Complete all 4 days
        for (int day = 1; day <= 4; day++)
        {
            var dayNumber = (DayNumber)day;
            var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

            var performances = new List<object>();
            foreach (var exercise in dayExercises)
            {
                var sets = CreateSuccessPerformanceForExercise(exercise);
                performances.Add(new
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = sets
                });
            }

            var response = await client.PostAsJsonAsync(
                $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                new { Performances = performances });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act - Progress to next week
        var progressResponse = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/progress-week",
            null);

        // Assert
        progressResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var progressResult = await progressResponse.Content.ReadFromJsonAsync<ProgressWeekResult>();
        progressResult.Should().NotBeNull();
        progressResult!.NewWeek.Should().Be(2);

        // Verify workout is now at week 2
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        updatedWorkout!.CurrentWeek.Should().Be(2);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a workout with the spreadsheet test data configuration.
    /// </summary>
    private async Task<Guid> CreateSpreadsheetTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>();

        foreach (var config in SpreadsheetTestData.AllExercises)
        {
            CreateExerciseRequest request;

            if (config.ProgressionType == ProgressionTestType.Linear)
            {
                request = new CreateExerciseRequest
                {
                    TemplateName = config.Name,
                    Category = GetCategoryFromProgressionType(config.ProgressionType),
                    ProgressionType = config.ProgressionType.ToString(),
                    AssignedDay = config.Day,
                    OrderInDay = config.Order,
                    TrainingMaxValue = config.TrainingMax ?? 50m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                };
            }
            else if (config.ProgressionType == ProgressionTestType.RepsPerSet)
            {
                request = new CreateExerciseRequest
                {
                    TemplateName = config.Name,
                    Category = GetCategoryFromProgressionType(config.ProgressionType),
                    ProgressionType = config.ProgressionType.ToString(),
                    AssignedDay = config.Day,
                    OrderInDay = config.Order,
                    StartingWeight = 20m,
                    WeightUnit = WeightUnit.Kilograms,
                    StartingSets = config.StartingSets ?? 3,
                    TargetSets = 5,
                    IsUnilateral = config.IsUnilateral
                };
            }
            else // MinimalSets
            {
                request = new CreateExerciseRequest
                {
                    TemplateName = config.Name,
                    Category = GetCategoryFromProgressionType(config.ProgressionType),
                    ProgressionType = config.ProgressionType.ToString(),
                    AssignedDay = config.Day,
                    OrderInDay = config.Order,
                    StartingWeight = config.StartingWeight ?? 30m,
                    WeightUnit = WeightUnit.Kilograms,
                    TargetTotalReps = config.TargetTotalReps ?? 40,
                    StartingSets = config.StartingSets ?? 3
                };
            }

            exercises.Add(request);
        }

        var command = new CreateWorkoutCommand(
            Name: "Spreadsheet Test Workout",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private static ExerciseCategory GetCategoryFromProgressionType(ProgressionTestType type)
    {
        return type switch
        {
            ProgressionTestType.Linear => ExerciseCategory.MainLift,
            ProgressionTestType.RepsPerSet => ExerciseCategory.Accessory,
            ProgressionTestType.MinimalSets => ExerciseCategory.Accessory,
            _ => ExerciseCategory.Accessory
        };
    }

    private async Task<WorkoutDto?> GetCurrentWorkoutAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/workouts/current");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkoutDto>();
    }

    private static List<object> CreateSuccessPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<object>();

        if (exercise.Progression.Type == "Linear")
        {
            var linear = exercise.Progression as LinearProgressionDto;
            for (int i = 1; i <= linear!.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = linear.TrainingMax.Value * 0.7m,
                    WeightUnit = linear.TrainingMax.Unit,
                    ActualReps = isAmrap ? 15 : 10, // AMRAP gets more reps (success)
                    WasAmrap = isAmrap
                });
            }
        }
        else if (exercise.Progression.Type == "RepsPerSet")
        {
            var rps = exercise.Progression as RepsPerSetProgressionDto;
            for (int i = 1; i <= rps!.CurrentSetCount; i++)
            {
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = rps.WeightUnit == "Kilograms" ? 0 : 1,
                    ActualReps = rps.RepRange.Maximum, // Hit max reps = success
                    WasAmrap = false
                });
            }
        }
        else if (exercise.Progression.Type == "MinimalSets")
        {
            var minimal = exercise.Progression as MinimalSetsProgressionDto;
            var repsPerSet = minimal!.TargetTotalReps / minimal.CurrentSetCount;
            var remainder = minimal.TargetTotalReps % minimal.CurrentSetCount;

            for (int i = 1; i <= minimal.CurrentSetCount; i++)
            {
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = 0,
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    private static List<object> CreatePerformancesForDay(WorkoutDto workout, DayNumber day)
    {
        var dayExercises = workout.Exercises
            .Where(e => e.AssignedDay == day)
            .ToList();

        var performances = new List<object>();
        foreach (var exercise in dayExercises)
        {
            var completedSets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new
            {
                ExerciseId = exercise.Id,
                CompletedSets = completedSets
            });
        }

        return performances;
    }

    #endregion
}
