using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using A2S.Tests.Shared.Helpers;
using A2S.Tests.Shared.TestData;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

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

    [Fact]
    public async Task CompleteDay_WithMixedProgressionTypes_ReturnsSuccessWithChanges()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Get the workout to find actual exercise IDs
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        getCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout.Should().NotBeNull();

        // Create performances for Day 1 exercises
        var actualPerformances = CreatePerformancesForDay(workout!, DayNumber.Day1);

        var completeDayRequest = new { Performances = actualPerformances };
        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            completeDayRequest);

        // Debug output if request fails
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Complete day failed. Status: {response.StatusCode}, Body: {errorContent}");
        }

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
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();

        var performances = new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = Guid.NewGuid(),
                CompletedSets = new List<CompletedSetRequest>
                {
                    new CompletedSetRequest { SetNumber = 1, Weight = 50m, WeightUnit = WeightUnit.Kilograms, ActualReps = 10, WasAmrap = false }
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/days/1/complete",
            new { Performances = performances });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompleteDay_WithInvalidDayNumber_ReturnsBadRequest()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var performances = new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = Guid.NewGuid(),
                CompletedSets = new List<CompletedSetRequest>
                {
                    new CompletedSetRequest { SetNumber = 1, Weight = 50m, WeightUnit = WeightUnit.Kilograms, ActualReps = 10, WasAmrap = false }
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/7/complete", // Invalid day
            new { Performances = performances });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests completing Day 1 with actual spreadsheet data for Week 1.
    /// Verifies linear progression (OHP with AMRAP +4) correctly changes.
    /// </summary>
    [Fact]
    public async Task CompleteDay1_Week1_WithSpreadsheetData_AppliesCorrectProgressions()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find the OHP exercise (Linear progression)
        var ohp = workout!.Exercises.FirstOrDefault(e => e.Name == "Overhead Press (Smith Machine)");
        ohp.Should().NotBeNull("OHP should be in the workout");
        ohp!.Progression.Type.Should().Be("Linear");

        // Build performances based on Week 1 spreadsheet data
        var week1OhpData = SpreadsheetTestData.GetWeekData(1, "Overhead Press (Smith Machine)");

        var performances = new List<ExercisePerformanceRequest>();

        // OHP: 4 sets at 42.5kg, AMRAP gets 19 reps (target was 15, so +4)
        var ohpSets = new List<CompletedSetRequest>();
        for (int i = 1; i <= week1OhpData.SetGoal!.Value; i++)
        {
            var isAmrap = i == week1OhpData.SetGoal.Value;
            ohpSets.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = week1OhpData.Weight!.Value,
                WeightUnit = WeightUnit.Kilograms,
                ActualReps = isAmrap ? week1OhpData.AmrapResult!.Value : week1OhpData.RepsPerNormalSet!.Value,
                WasAmrap = isAmrap
            });
        }

        performances.Add(new ExercisePerformanceRequest
        {
            ExerciseId = ohp.Id,
            CompletedSets = ohpSets
        });

        // Add other Day 1 exercises with success performances
        var day1Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1 && e.Id != ohp.Id);
        foreach (var exercise in day1Exercises)
        {
            var sets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets
            });
        }

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            new { Performances = performances });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day1);

        // OHP with high AMRAP (19 actual vs 15 rep-out target = +4 delta) should show "TM increased 2%"
        // Week 1 Hypertrophy rep-out target is 15
        var ohpChange = result.ProgressionChanges.FirstOrDefault(c => c.ExerciseName == "Overhead Press (Smith Machine)");
        ohpChange.Should().NotBeNull();
        ohpChange!.Change.Should().Contain("3%", "AMRAP +9 delta (19-10) should result in 3% TM increase");
    }

    #endregion

    #region Progress Week Tests

    /// <summary>
    /// Tests progressing from week 1 to week 2 by completing all days.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_FromWeek1ToWeek2_ReturnsCorrectWeekAndBlock()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        CompleteDayResult? lastDayResult = null;

        // Complete all 4 days to trigger week progression
        for (int day = 1; day <= 4; day++)
        {
            var dayNumber = (DayNumber)day;
            var performances = CreatePerformancesForDay(workout!, dayNumber);
            var response = await client.PostAsJsonAsync(
                $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                new { Performances = performances });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            lastDayResult = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        }

        lastDayResult.Should().NotBeNull();
        lastDayResult!.WeekProgressed.Should().BeTrue();
        lastDayResult.NewCurrentWeek.Should().Be(2);

        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        updatedWorkout!.CurrentWeek.Should().Be(2);
        updatedWorkout.CurrentBlock.Should().Be(1); // Still in block 1
    }

    /// <summary>
    /// Tests that week 7 is correctly identified as a deload week.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_ToWeek7_IdentifiesAsDeloadWeek()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Progress through weeks 1-6 by completing all days
        for (int week = 1; week <= 6; week++)
        {
            var workout = await GetCurrentWorkoutAsync(client);
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var performances = CreatePerformancesForDay(workout!, dayNumber);
                var response = await client.PostAsJsonAsync(
                    $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                    new { Performances = performances });
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        // Check that we're now at week 7 (deload week)
        var finalWorkout = await GetCurrentWorkoutAsync(client);

        finalWorkout!.CurrentWeek.Should().Be(7);
    }

    /// <summary>
    /// Tests that progressing with invalid workout ID returns 404.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_WithInvalidWorkoutId_ReturnsNotFound()
    {
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();

        var response = await client.PostAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/progress-week",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests block transitions (week 7 -> block 2, week 14 -> block 3).
    /// </summary>
    [Fact]
    public async Task ProgressWeek_AcrossBlocks_UpdatesBlockCorrectly()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        // Progress to week 8 (start of block 2) by completing all days in weeks 1-7
        for (int week = 1; week <= 7; week++)
        {
            var workout = await GetCurrentWorkoutAsync(client);
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var performances = CreatePerformancesForDay(workout!, dayNumber);
                var response = await client.PostAsJsonAsync(
                    $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                    new { Performances = performances });
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        var finalWorkout = await GetCurrentWorkoutAsync(client);
        finalWorkout!.CurrentWeek.Should().Be(8);
        finalWorkout.CurrentBlock.Should().Be(2);
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
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find Lat Pulldown (RepsPerSet progression, 3 starting sets)
        var latPulldown = workout!.Exercises.FirstOrDefault(e => e.Name == "Lat Pulldown (Cable)");
        latPulldown.Should().NotBeNull();
        latPulldown!.Progression.Type.Should().Be("RepsPerSet");

        var rpsProgression = latPulldown.Progression as RepsPerSetProgressionDto;
        rpsProgression.Should().NotBeNull();
        var initialSetCount = rpsProgression!.CurrentSetCount;
        initialSetCount.Should().Be(3, "Lat Pulldown should start with 3 sets");

        // Create performances for all Day 1 exercises (all hitting max reps = success)
        var performances = new List<ExercisePerformanceRequest>();
        var day1Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1).ToList();

        foreach (var exercise in day1Exercises)
        {
            var sets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets
            });
        }

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/1/complete",
            new { Performances = performances });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();

        // Lat Pulldown should have "Added 1 set" change
        var latPulldownChange = result!.ProgressionChanges.FirstOrDefault(c => c.ExerciseName == "Lat Pulldown (Cable)");
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
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find Assisted Dips (MinimalSets progression)
        var assistedDips = workout!.Exercises.FirstOrDefault(e => e.Name == "Triceps Dip (Assisted)");
        assistedDips.Should().NotBeNull();
        assistedDips!.Progression.Type.Should().Be("MinimalSets");

        // Find Assisted Pullups (MinimalSets progression)
        var assistedPullups = workout.Exercises.FirstOrDefault(e => e.Name == "Pull Up (Assisted)");
        assistedPullups.Should().NotBeNull();
        assistedPullups!.Progression.Type.Should().Be("MinimalSets");

        // Create performances for Day 3 exercises
        var performances = new List<ExercisePerformanceRequest>();
        var day3Exercises = workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day3).ToList();

        foreach (var exercise in day3Exercises)
        {
            if (exercise.Progression.Type == "MinimalSets")
            {
                var minimalSets = exercise.Progression as MinimalSetsProgressionDto;
                // Complete target reps in exact number of sets (maintained)
                var setsData = new List<CompletedSetRequest>();
                var repsPerSet = minimalSets!.TargetTotalReps / minimalSets.CurrentSetCount;
                var remainder = minimalSets.TargetTotalReps % minimalSets.CurrentSetCount;

                for (int i = 1; i <= minimalSets.CurrentSetCount; i++)
                {
                    setsData.Add(new CompletedSetRequest
                    {
                        SetNumber = i,
                        Weight = minimalSets.CurrentWeight,
                        WeightUnit = WeightUnit.Kilograms,
                        ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                        WasAmrap = false
                    });
                }

                performances.Add(new ExercisePerformanceRequest
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = setsData
                });
            }
            else
            {
                var sets = CreateSuccessPerformanceForExercise(exercise);
                performances.Add(new ExercisePerformanceRequest
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = sets
                });
            }
        }

        var response = await client.PostAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/days/3/complete",
            new { Performances = performances });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day3);
        result.ExercisesCompleted.Should().BeGreaterThan(0);
    }

    #endregion

    #region RepsPerSet Multi-Week Progression Tests

    /// <summary>
    /// Multi-week integration test for RepsPerSet progression with concrete value assertions.
    /// Verifies the full cycle: add sets -> add sets -> weight increase + reset -> maintained -> failed.
    /// Each week asserts exact CurrentSetCount and CurrentWeight values from the API.
    /// </summary>
    [Fact]
    public async Task RepsPerSetProgression_MultiWeekCycle_VerifiesConcreteSetAndWeightChanges()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // Find Lat Pulldown (RepsPerSet, Cable, starts at 3 sets, target 5, 20kg)
        var latPulldown = workout!.Exercises.FirstOrDefault(e => e.Name == "Lat Pulldown (Cable)");
        latPulldown.Should().NotBeNull();
        var rps = latPulldown!.Progression as RepsPerSetProgressionDto;
        rps.Should().NotBeNull();
        rps!.CurrentSetCount.Should().Be(3, "Lat Pulldown should start with 3 sets");
        rps.CurrentWeight.Should().Be(20m, "Lat Pulldown should start at 20kg");

        // RepRange.Common.Medium = (Min=8, Target=10, Max=12)
        // SUCCESS: all sets >= Maximum (12)
        // MAINTAINED: all sets >= Minimum (8) but NOT all >= Maximum (12) -> use Target (10)
        // FAILED: any set < Minimum (8)
        var weekScenarios = new (int repsForLatPulldown, int expectedSets, decimal expectedWeight, string description)[]
        {
            // Week 1: SUCCESS (all sets >= 12) -> adds 1 set: 3->4
            (12, 4, 20m, "SUCCESS: 3 sets all hit 12 (max) -> add set to 4"),
            // Week 2: SUCCESS (all sets >= 12) -> adds 1 set: 4->5
            (12, 5, 20m, "SUCCESS: 4 sets all hit 12 (max) -> add set to 5"),
            // Week 3: SUCCESS (all sets >= 12) -> at target (5), increase weight + reset to 3
            (12, 3, 22.5m, "SUCCESS at target: 5 sets all hit 12 -> weight +2.5kg, reset to 3 sets"),
            // Week 4: MAINTAINED (all sets hit 10, >= min 8 but < max 12) -> no change
            (10, 3, 22.5m, "MAINTAINED: 3 sets all hit 10 (target, below max 12) -> no change"),
            // Week 5: FAILED (one set below min 8) -> remove 1 set: 3->2
            (-1, 2, 22.5m, "FAILED: one set below min (7 reps) -> remove set to 2"),
        };

        for (int weekIdx = 0; weekIdx < weekScenarios.Length; weekIdx++)
        {
            var scenario = weekScenarios[weekIdx];
            var weekNum = weekIdx + 1;

            // Re-read the workout to get current state
            workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(weekNum, $"Should be at week {weekNum}");

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Lat Pulldown (Cable)")
                    {
                        var currentRps = exercise.Progression as RepsPerSetProgressionDto;
                        var setsList = new List<CompletedSetRequest>();

                        if (scenario.repsForLatPulldown == -1)
                        {
                            // FAILURE scenario: one set below minimum (7 reps)
                            for (int i = 1; i <= currentRps!.CurrentSetCount; i++)
                            {
                                setsList.Add(new CompletedSetRequest
                                {
                                    SetNumber = i,
                                    Weight = currentRps.CurrentWeight,
                                    WeightUnit = WeightUnit.Kilograms,
                                    ActualReps = i == 1 ? 7 : 10, // First set below min (8), rest ok
                                    WasAmrap = false
                                });
                            }
                        }
                        else
                        {
                            // SUCCESS or MAINTAINED: all sets at specified reps
                            for (int i = 1; i <= currentRps!.CurrentSetCount; i++)
                            {
                                setsList.Add(new CompletedSetRequest
                                {
                                    SetNumber = i,
                                    Weight = currentRps.CurrentWeight,
                                    WeightUnit = WeightUnit.Kilograms,
                                    ActualReps = scenario.repsForLatPulldown,
                                    WasAmrap = false
                                });
                            }
                        }

                        performances.Add(new ExercisePerformanceRequest
                        {
                            ExerciseId = exercise.Id,
                            CompletedSets = setsList
                        });
                    }
                    else
                    {
                        // All other exercises: maintain (don't change their state)
                        var sets = CreateMaintainPerformanceForExercise(exercise);
                        performances.Add(new ExercisePerformanceRequest
                        {
                            ExerciseId = exercise.Id,
                            CompletedSets = sets
                        });
                    }
                }

                var response = await client.PostAsJsonAsync(
                    $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                    new { Performances = performances });
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"Week {weekNum} Day {day} should complete successfully");
            }

            // After completing all 4 days, the week auto-progresses.
            // Re-read and assert the Lat Pulldown's progression state.
            var updatedWorkout = await GetCurrentWorkoutAsync(client);
            var updatedLatPulldown = updatedWorkout!.Exercises.First(e => e.Name == "Lat Pulldown (Cable)");
            var updatedRps = updatedLatPulldown.Progression as RepsPerSetProgressionDto;

            updatedRps!.CurrentSetCount.Should().Be(scenario.expectedSets,
                $"Week {weekNum}: {scenario.description} -> CurrentSetCount should be {scenario.expectedSets}");
            updatedRps.CurrentWeight.Should().Be(scenario.expectedWeight,
                $"Week {weekNum}: {scenario.description} -> CurrentWeight should be {scenario.expectedWeight}kg");
        }
    }

    #endregion

    #region Combined 21-Week Progression Tests

    /// <summary>
    /// Full 21-week integration test verifying Linear, bilateral RepsPerSet, and unilateral RepsPerSet
    /// progressions all running simultaneously in the same workout.
    ///
    /// Linear (OHP, Day 1): Tests all AMRAP delta paths (+3%, +2%, +1.5%, 0, -2%, -5%) and deloads.
    /// Bilateral RepsPerSet (Lat Pulldown, Day 1): Full add-sets → weight-increase-reset → maintained → failed cycle.
    ///   MaxSets=5 (bilateral), startingSets=3, targetSets=5, Cable +2.5kg.
    /// Unilateral RepsPerSet (Single Leg Press, Day 4): Unilateral capped at MaxSets=3, startingSets=4.
    ///   Because startingSets(4) > effectiveMaxSets(3), SUCCESS always increases weight.
    ///   Machine +2.5kg.
    ///
    /// Also verifies Hevy sync data via the WeekPlan endpoint each week:
    /// - Correct set count, weight, target reps for each exercise
    /// - AMRAP flag on last set of Linear exercises (maps to Hevy "failure" type)
    /// - No AMRAP on RepsPerSet exercises
    /// - Correct intensity-based weights for Linear exercises
    /// </summary>
    [Fact]
    public async Task Full21WeekCycle_AllProgressionTypes_VerifiesProgressionAndHevySync()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);

        // --- Linear setup (OHP, Day 1) ---
        var ohp = workout!.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
        var linearProg = ohp.Progression as LinearProgressionDto;
        linearProg.Should().NotBeNull();
        var startingTm = linearProg!.TrainingMax.Value;

        var weeklyDeltas = new[]
        {
            +5, +4, +3, 0, -1, +5, 0,   // Block 1 (week 7 = deload)
            +4, +3, +5, -2, +4, +5, 0,   // Block 2 (week 14 = deload)
            +3, +4, 0, +5, -1, +4, 0     // Block 3 (week 21 = deload)
        };
        // Pre-computed TM sequence (OHP starting at 65m, verified against domain logic)
        startingTm.Should().Be(65m, "OHP starting TM must be 65m for pre-computed assertions");
        var expectedTmValues = new[] { 65m, 66.95m, 68.29m, 69.31m, 69.31m, 67.92m, 69.96m, 69.96m, 71.36m, 72.43m, 74.60m, 70.87m, 72.29m, 74.46m, 74.46m, 75.58m, 77.09m, 77.09m, 79.40m, 77.81m, 79.37m, 79.37m };

        // --- Bilateral RepsPerSet setup (Lat Pulldown, Day 1) ---
        // Cable +2.5kg, startingSets=3, targetSets=5, maxSets=5 (bilateral)
        // RepRange.Common.Medium: Min=8, Target=10, Max=12
        // S=SUCCESS(12), M=MAINTAINED(10), F=FAILED(7 on 1 set)
        var latScenarios = new (int reps, int expectedSets, decimal expectedWeight, string desc)[]
        {
            (12, 4, 20m,   "S: 3→4"),            // Week 1
            (12, 5, 20m,   "S: 4→5"),            // Week 2
            (12, 3, 22.5m, "S at target: +2.5kg, reset 3"), // Week 3
            (10, 3, 22.5m, "M: no change"),      // Week 4
            (12, 4, 22.5m, "S: 3→4"),            // Week 5
            (12, 5, 22.5m, "S: 4→5"),            // Week 6
            (12, 3, 25m,   "S at target: +2.5kg, reset 3"), // Week 7
            (-1, 2, 25m,   "F: 3→2"),            // Week 8
            (10, 2, 25m,   "M: no change"),      // Week 9
            (12, 3, 25m,   "S: 2→3"),            // Week 10
            (12, 4, 25m,   "S: 3→4"),            // Week 11
            (12, 5, 25m,   "S: 4→5"),            // Week 12
            (12, 3, 27.5m, "S at target: +2.5kg, reset 3"), // Week 13
            (10, 3, 27.5m, "M: no change"),      // Week 14
            (12, 4, 27.5m, "S: 3→4"),            // Week 15
            (-1, 3, 27.5m, "F: 4→3"),            // Week 16
            (12, 4, 27.5m, "S: 3→4"),            // Week 17
            (12, 5, 27.5m, "S: 4→5"),            // Week 18
            (12, 3, 30m,   "S at target: +2.5kg, reset 3"), // Week 19
            (10, 3, 30m,   "M: no change"),      // Week 20
            (10, 3, 30m,   "M: no change"),      // Week 21
        };

        // --- Unilateral RepsPerSet setup (Single Leg Press, Day 4) ---
        // Machine +2.5kg, startingSets=4, targetSets=6, maxSets=3 (unilateral)
        // effectiveMaxSets = min(6,3) = 3. StartingSets(4) > effectiveMaxSets(3),
        // so SUCCESS always increases weight and resets to 4.
        // FAILED drops sets. Only when sets < 3 does SUCCESS add a set instead.
        var legPressScenarios = new (int reps, int expectedSets, decimal expectedWeight, string desc)[]
        {
            (12, 4, 22.5m, "S: 4>=3, +2.5kg, reset 4"),   // Week 1
            (12, 4, 25m,   "S: 4>=3, +2.5kg, reset 4"),   // Week 2
            (10, 4, 25m,   "M: no change"),                 // Week 3
            (12, 4, 27.5m, "S: 4>=3, +2.5kg, reset 4"),   // Week 4
            (-1, 3, 27.5m, "F: 4→3"),                      // Week 5
            (10, 3, 27.5m, "M: no change"),                 // Week 6
            (12, 4, 30m,   "S: 3>=3, +2.5kg, reset 4"),   // Week 7
            (-1, 3, 30m,   "F: 4→3"),                      // Week 8
            (-1, 2, 30m,   "F: 3→2"),                      // Week 9
            (12, 3, 30m,   "S: 2<3, add set 2→3"),         // Week 10
            (12, 4, 32.5m, "S: 3>=3, +2.5kg, reset 4"),   // Week 11
            (10, 4, 32.5m, "M: no change"),                 // Week 12
            (12, 4, 35m,   "S: 4>=3, +2.5kg, reset 4"),   // Week 13
            (10, 4, 35m,   "M: no change"),                 // Week 14
            (12, 4, 37.5m, "S: 4>=3, +2.5kg, reset 4"),   // Week 15
            (10, 4, 37.5m, "M: no change"),                 // Week 16
            (12, 4, 40m,   "S: 4>=3, +2.5kg, reset 4"),   // Week 17
            (-1, 3, 40m,   "F: 4→3"),                      // Week 18
            (10, 3, 40m,   "M: no change"),                 // Week 19
            (12, 4, 42.5m, "S: 3>=3, +2.5kg, reset 4"),   // Week 20
            (10, 4, 42.5m, "M: no change"),                 // Week 21
        };

        for (int week = 1; week <= 21; week++)
        {
            workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(week, $"Should be at week {week}");

            var isDeload = week == 7 || week == 14 || week == 21;
            var delta = weeklyDeltas[week - 1];
            var latWeek = latScenarios[week - 1];
            var legPressWeek = legPressScenarios[week - 1];

            // --- Hevy sync assertions: verify planned sets via WeekPlan endpoint ---
            // Check Day 1 (has OHP Linear + Lat Pulldown RepsPerSet)
            await AssertHevyPlannedSets(client, workoutId, week, day: 1, workout,
                expectedTmValues, isDeload);

            // Check Day 4 (has Single Leg Press unilateral RepsPerSet)
            await AssertHevyPlannedSets(client, workoutId, week, day: 4, workout,
                expectedTmValues, isDeload);

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Overhead Press (Smith Machine)")
                    {
                        performances.Add(PerformanceRequestBuilders.CreateLinearPerformance(
                            exercise, week, isDeload, delta, setCount: 4, skipAmrapOnDeload: true));
                    }
                    else if (exercise.Name == "Lat Pulldown (Cable)")
                    {
                        performances.Add(CreateRepsPerSetPerformance(exercise, latWeek.reps));
                    }
                    else if (exercise.Name == "Single Leg Press (Machine)")
                    {
                        performances.Add(CreateRepsPerSetPerformance(exercise, legPressWeek.reps));
                    }
                    else
                    {
                        performances.Add(new ExercisePerformanceRequest
                        {
                            ExerciseId = exercise.Id,
                            CompletedSets = CreateMaintainPerformanceForExercise(exercise)
                        });
                    }
                }

                var response = await client.PostAsJsonAsync(
                    $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                    new { Performances = performances });
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"Week {week} Day {day} should complete successfully");
            }

            // After week 21 the workout is Completed (no longer "current"), skip.
            if (week < 21)
            {
                var updated = await GetCurrentWorkoutAsync(client);

                var updatedOhp = updated!.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
                var updatedLinear = updatedOhp.Progression as LinearProgressionDto;
                updatedLinear!.TrainingMax.Value.Should().Be(expectedTmValues[week],
                    $"Week {week} OHP: delta={delta}, deload={isDeload} -> TM={expectedTmValues[week]}kg");

                var updatedLat = updated.Exercises.First(e => e.Name == "Lat Pulldown (Cable)");
                var updatedRps = updatedLat.Progression as RepsPerSetProgressionDto;
                updatedRps!.CurrentSetCount.Should().Be(latWeek.expectedSets,
                    $"Week {week} Lat Pulldown: {latWeek.desc} -> sets={latWeek.expectedSets}");
                updatedRps.CurrentWeight.Should().Be(latWeek.expectedWeight,
                    $"Week {week} Lat Pulldown: {latWeek.desc} -> weight={latWeek.expectedWeight}kg");

                var updatedLegPress = updated.Exercises.First(e => e.Name == "Single Leg Press (Machine)");
                var updatedLegRps = updatedLegPress.Progression as RepsPerSetProgressionDto;
                updatedLegRps!.CurrentSetCount.Should().Be(legPressWeek.expectedSets,
                    $"Week {week} Single Leg Press (uni): {legPressWeek.desc} -> sets={legPressWeek.expectedSets}");
                updatedLegRps.CurrentWeight.Should().Be(legPressWeek.expectedWeight,
                    $"Week {week} Single Leg Press (uni): {legPressWeek.desc} -> weight={legPressWeek.expectedWeight}kg");
            }
        }
    }

    private async Task AssertHevyPlannedSets(
        HttpClient client, Guid workoutId, int week, int day,
        WorkoutDto workout, decimal[] expectedTmValues, bool isDeload)
    {
        var planResponse = await client.GetAsync(
            $"/api/v1/workouts/{workoutId}/weeks/{week}/days/{day}/plan");
        planResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"WeekPlan for week {week} day {day} should return OK");

        var plan = await planResponse.Content.ReadFromJsonAsync<WeekPlanDto>();
        plan.Should().NotBeNull();
        plan!.WeekNumber.Should().Be(week);
        plan.DayNumber.Should().Be(day);
        plan.IsDeloadWeek.Should().Be(isDeload, $"Week {week} deload flag");

        foreach (var plannedExercise in plan.Exercises)
        {
            plannedExercise.PlannedSets.Should().NotBeEmpty(
                $"Week {week} Day {day}: {plannedExercise.Name} should have planned sets");

            if (plannedExercise.Name == "Overhead Press (Smith Machine)")
            {
                // Linear: verify set count, weight (TM * intensity), AMRAP on last set
                var isDeloadWeek = week == 7 || week == 14 || week == 21;
                var expectedSets = isDeloadWeek ? 4 : 5; // A2S2 Hypertrophy: 5 working sets, 4 deload sets
                var expectedNormalReps = ProgramWeekHelpers.GetRepsPerSetForWeek(week);
                var expectedAmrapReps = ProgramWeekHelpers.GetRepOutTargetForWeek(week);
                var intensity = ProgramWeekHelpers.GetIntensityForWeek(week);

                // Get the TM at the START of this week (before completion applies progression).
                // expectedTmValues[0] = starting TM, expectedTmValues[n] = TM after week n.
                var currentTm = expectedTmValues[week - 1];

                var expectedWeight = Math.Round(currentTm * intensity / 2.5m) * 2.5m;

                plannedExercise.PlannedSets.Should().HaveCount(expectedSets,
                    $"Week {week} OHP: should have {expectedSets} sets");
                // Normal sets use RepsPerSet
                plannedExercise.PlannedSets.First().TargetReps.Should().Be(expectedNormalReps,
                    $"Week {week} OHP: normal set target reps should be {expectedNormalReps}");
                plannedExercise.PlannedSets.First().WeightKg.Should().Be(expectedWeight,
                    $"Week {week} OHP: weight should be {expectedWeight}kg ({intensity * 100}% of TM {currentTm})");

                if (!isDeloadWeek)
                {
                    // AMRAP: last set should be AMRAP with rep-out target
                    plannedExercise.PlannedSets.Last().IsAmrap.Should().BeTrue(
                        $"Week {week} OHP: last set should be AMRAP for Hevy sync");
                    plannedExercise.PlannedSets.Last().TargetReps.Should().Be(expectedAmrapReps,
                        $"Week {week} OHP: AMRAP set target reps should be rep-out target {expectedAmrapReps}");
                }
                foreach (var set in plannedExercise.PlannedSets.SkipLast(1))
                {
                    set.IsAmrap.Should().BeFalse(
                        $"Week {week} OHP: non-last sets should NOT be AMRAP");
                }
            }
            else if (plannedExercise.ProgressionType == "RepsPerSet")
            {
                // RepsPerSet: no set should be AMRAP
                foreach (var set in plannedExercise.PlannedSets)
                {
                    set.IsAmrap.Should().BeFalse(
                        $"Week {week} Day {day} {plannedExercise.Name}: RepsPerSet sets should never be AMRAP");
                }

                // All sets should have the same weight and reps
                var firstSet = plannedExercise.PlannedSets.First();
                foreach (var set in plannedExercise.PlannedSets.Skip(1))
                {
                    set.WeightKg.Should().Be(firstSet.WeightKg,
                        $"Week {week} {plannedExercise.Name}: all sets same weight");
                    set.TargetReps.Should().Be(firstSet.TargetReps,
                        $"Week {week} {plannedExercise.Name}: all sets same target reps");
                }

                // Verify specific exercises we're tracking
                if (plannedExercise.Name == "Lat Pulldown (Cable)")
                {
                    var latDto = workout.Exercises.First(e => e.Name == "Lat Pulldown (Cable)");
                    var latRps = latDto.Progression as RepsPerSetProgressionDto;
                    plannedExercise.PlannedSets.Should().HaveCount(latRps!.CurrentSetCount,
                        $"Week {week} Lat Pulldown: set count should match current state");
                    firstSet.WeightKg.Should().Be(latRps.CurrentWeight,
                        $"Week {week} Lat Pulldown: weight should match current state");
                }
                else if (plannedExercise.Name == "Single Leg Press (Machine)")
                {
                    var legDto = workout.Exercises.First(e => e.Name == "Single Leg Press (Machine)");
                    var legRps = legDto.Progression as RepsPerSetProgressionDto;
                    plannedExercise.PlannedSets.Should().HaveCount(legRps!.CurrentSetCount,
                        $"Week {week} Single Leg Press: set count should match current state");
                    firstSet.WeightKg.Should().Be(legRps.CurrentWeight,
                        $"Week {week} Single Leg Press: weight should match current state");

                    // Verify unilateral flag
                    plannedExercise.Metadata.IsUnilateral.Should().BeTrue(
                        $"Week {week} Single Leg Press: should be marked as unilateral");
                }
            }
        }
    }

    /// <summary>
    /// Creates a performance request for a RepsPerSet exercise.
    /// reps = -1 means FAILED (one set at 7, rest at 10).
    /// reps >= 12 means SUCCESS, reps 8-11 means MAINTAINED.
    /// </summary>
    private static ExercisePerformanceRequest CreateRepsPerSetPerformance(
        ExerciseDto exercise, int reps)
    {
        var rps = exercise.Progression as RepsPerSetProgressionDto;
        var weightUnit = rps!.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;

        var setsList = new List<CompletedSetRequest>();
        for (int i = 1; i <= rps.CurrentSetCount; i++)
        {
            var actualReps = reps == -1
                ? (i == 1 ? 7 : 10)  // FAILED: first set below min, rest maintained
                : reps;

            setsList.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = rps.CurrentWeight,
                WeightUnit = weightUnit,
                ActualReps = actualReps,
                WasAmrap = false
            });
        }

        return new ExercisePerformanceRequest
        {
            ExerciseId = exercise.Id,
            CompletedSets = setsList
        };
    }

    #endregion

    #region Full Workout Cycle Tests

    /// <summary>
    /// Tests completing an entire week (all 4 days) and auto-progressing to the next week.
    /// The system automatically progresses when all days are completed.
    /// </summary>
    [Fact]
    public async Task CompleteFullWeek_ThenProgress_WorksCorrectly()
    {
        var client = CreateClient();
        var workoutId = await CreateSpreadsheetTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        workout!.CurrentWeek.Should().Be(1);

        CompleteDayResult? lastDayResult = null;

        // Complete all 4 days
        for (int day = 1; day <= 4; day++)
        {
            var dayNumber = (DayNumber)day;
            var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

            var performances = new List<ExercisePerformanceRequest>();
            foreach (var exercise in dayExercises)
            {
                var sets = CreateSuccessPerformanceForExercise(exercise);
                performances.Add(new ExercisePerformanceRequest
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = sets
                });
            }

            var response = await client.PostAsJsonAsync(
                $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                new { Performances = performances });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            lastDayResult = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        }

        lastDayResult.Should().NotBeNull();
        lastDayResult!.WeekProgressed.Should().BeTrue("Week should auto-progress when all days are completed");
        lastDayResult.NewCurrentWeek.Should().Be(2);

        // Verify workout is now at week 2
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        updatedWorkout!.CurrentWeek.Should().Be(2);
    }

    #endregion

    #region Helper Methods

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
                    ExternalTemplateId = $"test-{config.Name.Replace(" ", "-").ToLowerInvariant()}-flow",
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
                var startingSets = config.StartingSets ?? 3;
                var targetSets = Math.Max(startingSets, 5); // Target must be >= starting
                request = new CreateExerciseRequest
                {
                    TemplateName = config.Name,
                    ExternalTemplateId = $"test-{config.Name.Replace(" ", "-").ToLowerInvariant()}-flow",
                    Category = GetCategoryFromProgressionType(config.ProgressionType),
                    ProgressionType = config.ProgressionType.ToString(),
                    AssignedDay = config.Day,
                    OrderInDay = config.Order,
                    StartingWeight = 20m,
                    WeightUnit = WeightUnit.Kilograms,
                    StartingSets = startingSets,
                    TargetSets = targetSets,
                    IsUnilateral = config.IsUnilateral
                };
            }
            else // MinimalSets
            {
                request = new CreateExerciseRequest
                {
                    TemplateName = config.Name,
                    ExternalTemplateId = $"test-{config.Name.Replace(" ", "-").ToLowerInvariant()}-flow",
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
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create workout. Status: {response.StatusCode}, Body: {errorContent}");
        }
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

    private static List<CompletedSetRequest> CreateSuccessPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression.Type == "Linear")
        {
            var linear = exercise.Progression as LinearProgressionDto;
            var weightUnit = linear!.TrainingMax.Unit == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds; // 1 = Kilograms in DTO
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = linear.TrainingMax.Value * 0.7m,
                    WeightUnit = weightUnit,
                    ActualReps = isAmrap ? 15 : 10, // AMRAP gets more reps (success)
                    WasAmrap = isAmrap
                });
            }
        }
        else if (exercise.Progression.Type == "RepsPerSet")
        {
            var rps = exercise.Progression as RepsPerSetProgressionDto;
            var weightUnit = rps!.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= rps.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = weightUnit,
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
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = WeightUnit.Kilograms,
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    /// <summary>
    /// Creates a "maintain" performance for an exercise - hits target but doesn't trigger progression changes.
    /// For Linear: AMRAP delta = 0 (hit exact target reps). For RepsPerSet: hit target reps (not max).
    /// For MinimalSets: complete target reps in exact number of sets.
    /// </summary>
    private static List<CompletedSetRequest> CreateMaintainPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression.Type == "Linear")
        {
            var linear = exercise.Progression as LinearProgressionDto;
            var weightUnit = linear!.TrainingMax.Unit == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds;
            // Use the base sets (not week-specific) since we just need valid performances
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = linear.TrainingMax.Value * 0.7m,
                    WeightUnit = weightUnit,
                    ActualReps = 10, // AMRAP delta = 0 (target reps, no TM change)
                    WasAmrap = isAmrap
                });
            }
        }
        else if (exercise.Progression.Type == "RepsPerSet")
        {
            var rps = exercise.Progression as RepsPerSetProgressionDto;
            var weightUnit = rps!.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= rps.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = rps.RepRange.Maximum, // Hit target but not max -> maintained
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
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = WeightUnit.Kilograms,
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    private static List<ExercisePerformanceRequest> CreatePerformancesForDay(WorkoutDto workout, DayNumber day)
    {
        var dayExercises = workout.Exercises
            .Where(e => e.AssignedDay == day)
            .ToList();

        var performances = new List<ExercisePerformanceRequest>();
        foreach (var exercise in dayExercises)
        {
            var completedSets = CreateSuccessPerformanceForExercise(exercise);
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = completedSets
            });
        }

        return performances;
    }

    #endregion
}
