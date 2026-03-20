using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.UndoCompletion;
using A2S.Application.Commands.UpdateExercises;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Comprehensive integration tests for unilateral exercise progression.
/// Tests verify:
/// - Set count capping when toggling to unilateral (max 3 sets)
/// - Weight progression at unilateral max sets
/// - Snapshot preservation of unilateral status
/// - Status persistence across week progressions
/// </summary>
[Collection("Integration")]
public class UnilateralProgressionTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public UnilateralProgressionTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Toggle Unilateral Tests

    /// <summary>
    /// Tests that toggling to unilateral when at 4 sets caps the set count at 3 (MaxSets for unilateral).
    /// </summary>
    [Fact]
    public async Task ToggleUnilateral_At4Sets_CapsSetCountAt3()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithRepsPerSetExerciseAsync(client, startingSets: 4, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state: 4 sets, bilateral
        initialProgression.Should().NotBeNull();
        initialProgression!.CurrentSetCount.Should().Be(4);
        initialProgression.IsUnilateral.Should().BeFalse();

        // Act - Toggle to unilateral
        var updateRequest = new
        {
            Updates = new[]
            {
                new
                {
                    ExerciseId = exercise.Id,
                    IsUnilateral = true,
                    Reason = "Testing unilateral toggle"
                }
            }
        };

        var response = await client.PutAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UpdateExercisesResult>();
        result.Should().NotBeNull();
        result!.UpdatedCount.Should().Be(1);

        // Verify the exercise is now unilateral and capped at 3 sets
        var updatedWorkout = await GetWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as RepsPerSetProgressionDto;

        updatedProgression.Should().NotBeNull();
        updatedProgression!.IsUnilateral.Should().BeTrue("Exercise should be marked as unilateral");
        updatedProgression.CurrentSetCount.Should().Be(3,
            "Set count should be capped at 3 (MaxSets for unilateral) when was at 4");
    }

    /// <summary>
    /// Tests that toggling to unilateral when at 2 sets maintains the set count (below max).
    /// </summary>
    [Fact]
    public async Task ToggleUnilateral_At2Sets_MaintainsSetCount()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithRepsPerSetExerciseAsync(client, startingSets: 2, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state
        initialProgression!.CurrentSetCount.Should().Be(2);
        initialProgression.IsUnilateral.Should().BeFalse();

        // Act - Toggle to unilateral
        var updateRequest = new
        {
            Updates = new[]
            {
                new
                {
                    ExerciseId = exercise.Id,
                    IsUnilateral = true,
                    Reason = "Testing unilateral toggle at low sets"
                }
            }
        };

        var response = await client.PutAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWorkout = await GetWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as RepsPerSetProgressionDto;

        updatedProgression!.IsUnilateral.Should().BeTrue();
        updatedProgression.CurrentSetCount.Should().Be(2,
            "Set count should remain at 2 (below unilateral max of 3)");
    }

    #endregion

    #region Progression Tests

    /// <summary>
    /// Tests that a unilateral exercise at max sets (3) increases weight when all sets hit max reps.
    /// Verifies sets don't exceed 3 after successful progression.
    /// </summary>
    [Fact]
    public async Task UnilateralExercise_SuccessAtMaxSets_IncreasesWeight()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 3, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state: 3 sets (max for unilateral), unilateral
        initialProgression!.CurrentSetCount.Should().Be(3);
        initialProgression.IsUnilateral.Should().BeTrue();
        var initialWeight = initialProgression.CurrentWeight;

        // Act - Complete day with all sets hitting max reps (15)
        var performances = CreateMaxRepsPerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWorkout = await GetWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as RepsPerSetProgressionDto;

        updatedProgression!.CurrentWeight.Should().BeGreaterThan(initialWeight,
            "Weight should increase when at max unilateral sets and hitting max reps");
        updatedProgression.CurrentSetCount.Should().BeLessThanOrEqualTo(3,
            "Sets should never exceed 3 for unilateral exercises");
        updatedProgression.IsUnilateral.Should().BeTrue("Unilateral status should be preserved");
    }

    /// <summary>
    /// Tests that a unilateral exercise with failure (below min reps) decreases sets first, then weight.
    /// </summary>
    [Fact]
    public async Task UnilateralExercise_Failure_DecreasesSetsThenWeight()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 2, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state: 2 sets, unilateral
        initialProgression!.CurrentSetCount.Should().Be(2);
        initialProgression.IsUnilateral.Should().BeTrue();
        var initialWeight = initialProgression.CurrentWeight;

        // Act - Complete day with failure (below min reps of 8)
        var performances = CreateFailurePerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWorkout = await GetWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as RepsPerSetProgressionDto;

        // At 2 sets with failure: should go to 1 set
        updatedProgression!.CurrentSetCount.Should().Be(1,
            "Sets should decrease by 1 when failing above minimum sets");
        updatedProgression.CurrentWeight.Should().Be(initialWeight,
            "Weight should not change when sets can still be reduced");
        updatedProgression.IsUnilateral.Should().BeTrue("Unilateral status should be preserved");
    }

    #endregion

    #region Toggle Back and Forth Tests

    /// <summary>
    /// Tests that toggling bilateral -> unilateral -> bilateral maintains proper set progression.
    /// When toggling back to bilateral, sets should NOT restore to the pre-unilateral value.
    /// </summary>
    [Fact]
    public async Task ToggleBilateralToUnilateral_ThenBack_MaintainsSetProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithRepsPerSetExerciseAsync(client, startingSets: 3, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");

        // First, progress to 4 sets by completing a successful day
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;
        initialProgression!.CurrentSetCount.Should().Be(3);

        // Complete day with max reps to progress to 4 sets
        var performances = CreateMaxRepsPerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

        // Verify progression to 4 sets
        var workoutAfterProgress = await GetWorkoutAsync(client);
        var exerciseAfterProgress = workoutAfterProgress!.Exercises.First(e => e.Id == exercise.Id);
        var progressionAfterSuccess = exerciseAfterProgress.Progression as RepsPerSetProgressionDto;
        progressionAfterSuccess!.CurrentSetCount.Should().Be(4, "Should have progressed to 4 sets");

        // Act 1 - Toggle to unilateral (should cap at 3)
        var toggleToUnilateral = new
        {
            Updates = new[]
            {
                new { ExerciseId = exercise.Id, IsUnilateral = true, Reason = "Toggle to unilateral" }
            }
        };
        await client.PutAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises", toggleToUnilateral);

        var workoutAfterUnilateral = await GetWorkoutAsync(client);
        var exerciseAfterUnilateral = workoutAfterUnilateral!.Exercises.First(e => e.Id == exercise.Id);
        var progressionAfterUnilateral = exerciseAfterUnilateral.Progression as RepsPerSetProgressionDto;
        progressionAfterUnilateral!.CurrentSetCount.Should().Be(3, "Should be capped at 3 for unilateral");
        progressionAfterUnilateral.IsUnilateral.Should().BeTrue();

        // Act 2 - Toggle back to bilateral
        var toggleToBilateral = new
        {
            Updates = new[]
            {
                new { ExerciseId = exercise.Id, IsUnilateral = false, Reason = "Toggle back to bilateral" }
            }
        };
        await client.PutAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises", toggleToBilateral);

        // Assert - Sets should still be 3, NOT restored to 4
        var finalWorkout = await GetWorkoutAsync(client);
        var finalExercise = finalWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var finalProgression = finalExercise.Progression as RepsPerSetProgressionDto;

        finalProgression!.IsUnilateral.Should().BeFalse("Should be bilateral again");
        finalProgression.CurrentSetCount.Should().Be(3,
            "Sets should remain at 3, NOT restore to pre-unilateral value of 4");
    }

    #endregion

    #region Week Progression Tests

    /// <summary>
    /// Tests that unilateral status is preserved across multiple week progressions.
    /// </summary>
    [Fact]
    public async Task UnilateralStatus_PreservedAcrossWeekProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 2, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");

        // Verify initial state
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;
        initialProgression!.IsUnilateral.Should().BeTrue();

        // Complete all days in the week
        for (int day = 1; day <= GetDaysPerWeek(workout); day++)
        {
            var currentWorkout = await GetWorkoutAsync(client);
            var currentExercise = currentWorkout!.Exercises.First(e => e.Id == exercise.Id);
            var currentProgression = currentExercise.Progression as RepsPerSetProgressionDto;

            if (currentWorkout.Exercises.Any(e => (int)e.AssignedDay == day))
            {
                var performances = CreateMaintainPerformanceForDay(currentWorkout, day);
                if (performances.Any())
                {
                    var completeRequest = new { Performances = performances };
                    await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/{day}/complete", completeRequest);
                }
            }
        }

        // After completing all days, the week should auto-progress to Week 2
        // Assert - Unilateral status should be preserved
        var weekTwoWorkout = await GetWorkoutAsync(client);
        weekTwoWorkout!.CurrentWeek.Should().Be(2, "Week should auto-progress after completing all days");

        var weekTwoExercise = weekTwoWorkout.Exercises.First(e => e.Id == exercise.Id);
        var weekTwoProgression = weekTwoExercise.Progression as RepsPerSetProgressionDto;

        weekTwoProgression!.IsUnilateral.Should().BeTrue(
            "Unilateral status should be preserved after week progression");
    }

    #endregion

    #region Undo/Snapshot Tests

    /// <summary>
    /// Tests that unilateral status is preserved in the snapshot and restored on undo.
    /// </summary>
    [Fact]
    public async Task Snapshot_PreservesUnilateralStatus_OnUndo()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 2, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");

        // Verify initial state
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;
        initialProgression!.IsUnilateral.Should().BeTrue();
        initialProgression.CurrentSetCount.Should().Be(2);

        // Complete a day with success (would normally add a set)
        var performances = CreateMaxRepsPerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

        // Verify progression occurred
        var afterComplete = await GetWorkoutAsync(client);
        var afterCompleteExercise = afterComplete!.Exercises.First(e => e.Id == exercise.Id);
        var afterCompleteProgression = afterCompleteExercise.Progression as RepsPerSetProgressionDto;
        afterCompleteProgression!.CurrentSetCount.Should().Be(3, "Should have added a set on success");
        afterCompleteProgression.IsUnilateral.Should().BeTrue();

        // Act - Undo the completion
        var undoResponse = await client.PostAsync($"/api/v1/workouts/{workoutId}/undo-last-completion", null);

        // Assert
        undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var undoResult = await undoResponse.Content.ReadFromJsonAsync<UndoCompletionResult>();
        undoResult.Should().NotBeNull();
        undoResult!.Day.Should().Be(DayNumber.Day1);

        // Verify state is restored including unilateral status
        var restoredWorkout = await GetWorkoutAsync(client);
        var restoredExercise = restoredWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var restoredProgression = restoredExercise.Progression as RepsPerSetProgressionDto;

        restoredProgression!.CurrentSetCount.Should().Be(2,
            "Set count should be restored to pre-completion value");
        restoredProgression.IsUnilateral.Should().BeTrue(
            "Unilateral status should be preserved after undo");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test workout with a RepsPerSet exercise (bilateral by default) on Day 1.
    /// </summary>
    private async Task<Guid> CreateWorkoutWithRepsPerSetExerciseAsync(
        HttpClient client, int startingSets = 3, int targetSets = 5)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Lat Pulldown (Cable)",  // Must match ExerciseLibrary name exactly
                HevyExerciseTemplateId = $"lat-pulldown-unilateral-test-{Guid.NewGuid()}",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 40m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = startingSets,
                TargetSets = targetSets,
                IsUnilateral = false
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Unilateral Test Workout - Bilateral Start",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create workout: {response.StatusCode}. Details: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    /// <summary>
    /// Creates a test workout with a RepsPerSet exercise that is already unilateral.
    /// </summary>
    private async Task<Guid> CreateWorkoutWithUnilateralExerciseAsync(
        HttpClient client, int startingSets = 2, int targetSets = 5)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            // Day 1 - unilateral exercise (the one we're testing)
            new CreateExerciseRequest
            {
                TemplateName = "Bent Over Row (Dumbbell)",
                HevyExerciseTemplateId = $"db-row-unilateral-test-{Guid.NewGuid()}",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 20m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = startingSets,
                TargetSets = targetSets,
                IsUnilateral = true
            },
            // Day 2 - filler exercise (needed for 4-day variant)
            new CreateExerciseRequest
            {
                TemplateName = "Squat (Barbell)",
                HevyExerciseTemplateId = $"squat-filler-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            // Day 3 - filler exercise (needed for 4-day variant)
            new CreateExerciseRequest
            {
                TemplateName = "Bench Press (Barbell)",
                HevyExerciseTemplateId = $"bench-filler-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            // Day 4 - filler exercise (needed for 4-day variant)
            new CreateExerciseRequest
            {
                TemplateName = "Deadlift (Barbell)",
                HevyExerciseTemplateId = $"deadlift-filler-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Unilateral Test Workout - Unilateral Start",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create unilateral workout: {response.StatusCode}. Details: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private async Task<WorkoutDto?> GetWorkoutAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/workouts/current");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkoutDto>();
    }

    /// <summary>
    /// Creates performance data with all sets hitting max reps (15).
    /// This should trigger set progression or weight increase.
    /// </summary>
    private static List<ExercisePerformanceRequest> CreateMaxRepsPerformance(ExerciseDto exercise, RepsPerSetProgressionDto progression)
    {
        var sets = new List<CompletedSetRequest>();
        var weightUnit = progression.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;

        for (int i = 1; i <= progression.CurrentSetCount; i++)
        {
            sets.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = progression.CurrentWeight,
                WeightUnit = weightUnit,
                ActualReps = 15, // Max reps for 8-12-15 rep range
                WasAmrap = false
            });
        }

        return new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets,
                WasTemporarySubstitution = false
            }
        };
    }

    /// <summary>
    /// Creates performance data with at least one set below min reps (8).
    /// This should trigger failure handling (set decrease or weight decrease).
    /// </summary>
    private static List<ExercisePerformanceRequest> CreateFailurePerformance(ExerciseDto exercise, RepsPerSetProgressionDto progression)
    {
        var sets = new List<CompletedSetRequest>();
        var weightUnit = progression.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;

        for (int i = 1; i <= progression.CurrentSetCount; i++)
        {
            sets.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = progression.CurrentWeight,
                WeightUnit = weightUnit,
                ActualReps = i == 1 ? 6 : 10, // First set below min (8), rest above
                WasAmrap = false
            });
        }

        return new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = sets,
                WasTemporarySubstitution = false
            }
        };
    }

    /// <summary>
    /// Creates maintain performance (target reps) for all exercises on a specific day.
    /// </summary>
    private static List<ExercisePerformanceRequest> CreateMaintainPerformanceForDay(WorkoutDto workout, int day)
    {
        var performances = new List<ExercisePerformanceRequest>();

        foreach (var exercise in workout.Exercises.Where(e => (int)e.AssignedDay == day))
        {
            var sets = new List<CompletedSetRequest>();

            if (exercise.Progression is RepsPerSetProgressionDto rpsProgression)
            {
                var weightUnit = rpsProgression.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
                for (int i = 1; i <= rpsProgression.CurrentSetCount; i++)
                {
                    sets.Add(new CompletedSetRequest
                    {
                        SetNumber = i,
                        Weight = rpsProgression.CurrentWeight,
                        WeightUnit = weightUnit,
                        ActualReps = 12, // Target reps (maintain)
                        WasAmrap = false
                    });
                }
            }
            else if (exercise.Progression is LinearProgressionDto linearProgression)
            {
                var weight = linearProgression.TrainingMax.Value * 0.7m;
                var weightUnit = linearProgression.TrainingMax.Unit == 1 ? WeightUnit.Kilograms : WeightUnit.Pounds;
                for (int i = 1; i <= linearProgression.BaseSetsPerExercise; i++)
                {
                    var isAmrap = i == linearProgression.BaseSetsPerExercise;
                    sets.Add(new CompletedSetRequest
                    {
                        SetNumber = i,
                        Weight = weight,
                        WeightUnit = weightUnit,
                        ActualReps = isAmrap ? 10 : 8, // Normal performance
                        WasAmrap = isAmrap
                    });
                }
            }
            else if (exercise.Progression is MinimalSetsProgressionDto minimalProgression)
            {
                var weightUnit = minimalProgression.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
                var repsPerSet = minimalProgression.TargetTotalReps / minimalProgression.CurrentSetCount;
                for (int i = 1; i <= minimalProgression.CurrentSetCount; i++)
                {
                    sets.Add(new CompletedSetRequest
                    {
                        SetNumber = i,
                        Weight = minimalProgression.CurrentWeight,
                        WeightUnit = weightUnit,
                        ActualReps = repsPerSet,
                        WasAmrap = false
                    });
                }
            }

            if (sets.Any())
            {
                performances.Add(new ExercisePerformanceRequest
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = sets,
                    WasTemporarySubstitution = false
                });
            }
        }

        return performances;
    }

    private static int GetDaysPerWeek(WorkoutDto workout)
    {
        return workout.Variant switch
        {
            "FourDay" => 4,
            "FiveDay" => 5,
            "SixDay" => 6,
            _ => 4
        };
    }

    #endregion

    #region Helper Classes

    private class CreateWorkoutResponse
    {
        public Guid Id { get; set; }
    }

    #endregion
}
