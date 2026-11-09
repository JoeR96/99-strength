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

namespace A2S.Api.Tests.Integration;

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

    #region Progression Tests

    [Fact]
    public async Task UnilateralExercise_SuccessAtMaxSets_IncreasesWeight()
    {
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 3, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state: 3 sets (max for unilateral), unilateral
        initialProgression!.CurrentSetCount.Should().Be(3);
        initialProgression.IsUnilateral.Should().BeTrue();
        var initialWeight = initialProgression.CurrentWeight;

        var performances = CreateMaxRepsPerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

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

    [Fact]
    public async Task UnilateralExercise_Failure_DecreasesSetsThenWeight()
    {
        var client = CreateClient();
        var workoutId = await CreateWorkoutWithUnilateralExerciseAsync(client, startingSets: 2, targetSets: 5);
        var workout = await GetWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;

        // Verify initial state: 2 sets, unilateral
        initialProgression!.CurrentSetCount.Should().Be(2);
        initialProgression.IsUnilateral.Should().BeTrue();
        var initialWeight = initialProgression.CurrentWeight;

        var performances = CreateFailurePerformance(exercise, initialProgression);
        var completeRequest = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", completeRequest);

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

    #region Week Progression Tests

    [Fact]
    public async Task UnilateralStatus_PreservedAcrossWeekProgression()
    {
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

        var undoResponse = await client.PostAsync($"/api/v1/workouts/{workoutId}/undo-last-completion", null);

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

    private async Task<Guid> CreateWorkoutWithRepsPerSetExerciseAsync(
        HttpClient client, int startingSets = 3, int targetSets = 5)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Lat Pulldown (Cable)",  // Must match ExerciseLibrary name exactly
                ExternalTemplateId = $"lat-pulldown-unilateral-test-{Guid.NewGuid()}",
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

    private async Task<Guid> CreateWorkoutWithUnilateralExerciseAsync(
        HttpClient client, int startingSets = 2, int targetSets = 5)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            // Day 1 - unilateral exercise (the one we're testing)
            new CreateExerciseRequest
            {
                TemplateName = "Bent Over Row (Dumbbell)",
                ExternalTemplateId = $"db-row-unilateral-test-{Guid.NewGuid()}",
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
                ExternalTemplateId = $"squat-filler-{Guid.NewGuid()}",
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
                ExternalTemplateId = $"bench-filler-{Guid.NewGuid()}",
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
                ExternalTemplateId = $"deadlift-filler-{Guid.NewGuid()}",
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
