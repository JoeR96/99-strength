using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.UndoCompletion;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for the undo completion functionality.
/// Tests undo behavior in Week 1 (clears data only) and later weeks (rolls back to previous week).
/// </summary>
[Collection("Integration")]
public class UndoCompletionIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public UndoCompletionIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Basic Undo Tests

    [Fact]
    public async Task UndoLastCompletion_WithNoCompletedDays_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutAsync(client);

        // Act
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UndoLastCompletion_WithInvalidWorkoutId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoLastCompletion_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var workoutId = Guid.NewGuid();

        // Act
        var response = await unauthenticatedClient.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Week 1 Undo Tests - Should Just Clear Data

    /// <summary>
    /// In Week 1, undoing should just clear the completion data without rolling back the week.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_InWeek1_ClearsDataWithoutWeekRollback()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutAsync(client);

        var workout = await GetWorkoutAsync(client);
        workout!.CurrentWeek.Should().Be(1);
        workout.CurrentDay.Should().Be(1);

        // Complete Day 1
        await CompleteDayAsync(client, workoutId, 1, workout);

        var afterCompletion = await GetWorkoutAsync(client);
        afterCompletion!.CurrentDay.Should().Be(2);
        afterCompletion.CurrentWeek.Should().Be(1);
        afterCompletion.CompletedDaysInCurrentWeek.Should().Contain(1);

        // Act - Undo the completion
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day1);
        result.WeekNumber.Should().Be(1);
        result.WeekRolledBack.Should().BeFalse("Week should NOT roll back when undoing in Week 1");

        // Verify workout state is restored to Day 1, Week 1
        var restoredWorkout = await GetWorkoutAsync(client);
        restoredWorkout!.CurrentDay.Should().Be(1);
        restoredWorkout.CurrentWeek.Should().Be(1);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().NotContain(1);
    }

    /// <summary>
    /// In Week 1, undoing multiple days should clear them one at a time without week rollback.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_MultipleDaysInWeek1_ClearsEachDayIndividually()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateFourDayTestWorkoutAsync(client);

        var workout = await GetWorkoutAsync(client);

        // Complete Days 1, 2, 3 in Week 1
        await CompleteDayAsync(client, workoutId, 1, workout!);
        var afterDay1 = await GetWorkoutAsync(client);

        await CompleteDayAsync(client, workoutId, 2, afterDay1!);
        var afterDay2 = await GetWorkoutAsync(client);

        await CompleteDayAsync(client, workoutId, 3, afterDay2!);
        var afterDay3 = await GetWorkoutAsync(client);

        afterDay3!.CurrentWeek.Should().Be(1);
        afterDay3.CurrentDay.Should().Be(4);
        afterDay3.CompletedDaysInCurrentWeek.Should().HaveCount(3);

        // Act - Undo Day 3
        var undo3Response = await client.PostAsync($"/api/v1/workouts/{workoutId}/undo-last-completion", null);
        undo3Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var undo3Result = await undo3Response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        undo3Result!.Day.Should().Be(DayNumber.Day3);
        undo3Result.WeekRolledBack.Should().BeFalse();

        var afterUndo3 = await GetWorkoutAsync(client);
        afterUndo3!.CurrentDay.Should().Be(3);
        afterUndo3.CurrentWeek.Should().Be(1);
        afterUndo3.CompletedDaysInCurrentWeek.Should().HaveCount(2);

        // Undo Day 2
        var undo2Response = await client.PostAsync($"/api/v1/workouts/{workoutId}/undo-last-completion", null);
        var undo2Result = await undo2Response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        undo2Result!.Day.Should().Be(DayNumber.Day2);
        undo2Result.WeekRolledBack.Should().BeFalse();

        var afterUndo2 = await GetWorkoutAsync(client);
        afterUndo2!.CurrentDay.Should().Be(2);
        afterUndo2.CurrentWeek.Should().Be(1);
        afterUndo2.CompletedDaysInCurrentWeek.Should().HaveCount(1);

        // Undo Day 1
        var undo1Response = await client.PostAsync($"/api/v1/workouts/{workoutId}/undo-last-completion", null);
        var undo1Result = await undo1Response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        undo1Result!.Day.Should().Be(DayNumber.Day1);
        undo1Result.WeekRolledBack.Should().BeFalse();

        var afterUndo1 = await GetWorkoutAsync(client);
        afterUndo1!.CurrentDay.Should().Be(1);
        afterUndo1.CurrentWeek.Should().Be(1);
        afterUndo1.CompletedDaysInCurrentWeek.Should().BeEmpty();
    }

    #endregion

    #region Later Week Undo Tests - Should Roll Back Week

    /// <summary>
    /// When in Week 2+, undoing a completion from the previous week should roll back to that week.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_InWeek2_RollsBackToWeek1()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateFourDayTestWorkoutAsync(client);

        // Complete all 4 days of Week 1 to progress to Week 2
        await CompleteEntireWeekAsync(client, workoutId);

        var afterWeek1 = await GetWorkoutAsync(client);
        afterWeek1!.CurrentWeek.Should().Be(2, "Should have progressed to Week 2 after completing Week 1");
        afterWeek1.CurrentDay.Should().Be(1);
        afterWeek1.CompletedDaysInCurrentWeek.Should().BeEmpty();

        // Act - Undo the last completion (Day 4 of Week 1)
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        result.Should().NotBeNull();
        result!.Day.Should().Be(DayNumber.Day4);
        result.WeekNumber.Should().Be(1);
        result.WeekRolledBack.Should().BeTrue("Week should roll back when undoing from previous week");
        result.NewCurrentWeek.Should().Be(1);

        // Verify workout state is restored to Week 1, Day 4
        var restoredWorkout = await GetWorkoutAsync(client);
        restoredWorkout!.CurrentWeek.Should().Be(1);
        restoredWorkout.CurrentDay.Should().Be(4);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().Contain(1);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().Contain(2);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().Contain(3);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().NotContain(4);
    }

    /// <summary>
    /// When in Week 3, undoing should roll back to Week 2 (preserving Week 1 completions).
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_InWeek3_RollsBackToWeek2()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateFourDayTestWorkoutAsync(client);

        // Complete Weeks 1 and 2 to progress to Week 3
        await CompleteEntireWeekAsync(client, workoutId); // Week 1
        await CompleteEntireWeekAsync(client, workoutId); // Week 2

        var afterWeek2 = await GetWorkoutAsync(client);
        afterWeek2!.CurrentWeek.Should().Be(3);

        // Act - Undo the last completion (Day 4 of Week 2)
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        result!.Day.Should().Be(DayNumber.Day4);
        result.WeekNumber.Should().Be(2);
        result.WeekRolledBack.Should().BeTrue();
        result.NewCurrentWeek.Should().Be(2);

        // Verify workout state
        var restoredWorkout = await GetWorkoutAsync(client);
        restoredWorkout!.CurrentWeek.Should().Be(2);
        restoredWorkout.CurrentDay.Should().Be(4);
    }

    /// <summary>
    /// When mid-week in Week 2 (not rolled over yet), undoing should NOT roll back the week.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_MidWeekInWeek2_DoesNotRollBackWeek()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateFourDayTestWorkoutAsync(client);

        // Complete Week 1 to progress to Week 2
        await CompleteEntireWeekAsync(client, workoutId);

        var atWeek2Start = await GetWorkoutAsync(client);
        atWeek2Start!.CurrentWeek.Should().Be(2);

        // Complete Day 1 and Day 2 of Week 2 (but not all days)
        await CompleteDayAsync(client, workoutId, 1, atWeek2Start);
        var afterDay1Week2 = await GetWorkoutAsync(client);
        await CompleteDayAsync(client, workoutId, 2, afterDay1Week2!);

        var midWeek2 = await GetWorkoutAsync(client);
        midWeek2!.CurrentWeek.Should().Be(2);
        midWeek2.CurrentDay.Should().Be(3);

        // Act - Undo Day 2 of Week 2 (same week, no rollback)
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UndoCompletionResult>();
        result!.Day.Should().Be(DayNumber.Day2);
        result.WeekNumber.Should().Be(2);
        result.WeekRolledBack.Should().BeFalse("Should NOT roll back when undoing within same week");

        var restoredWorkout = await GetWorkoutAsync(client);
        restoredWorkout!.CurrentWeek.Should().Be(2);
        restoredWorkout.CurrentDay.Should().Be(2);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().Contain(1);
        restoredWorkout.CompletedDaysInCurrentWeek.Should().NotContain(2);
    }

    #endregion

    #region Progression State Restoration Tests

    /// <summary>
    /// Undoing should restore the exercise's training max to its pre-completion value.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_RestoresLinearProgressionTrainingMax()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutAsync(client);

        var workout = await GetWorkoutAsync(client);
        var linearExercise = workout!.Exercises.First(e => e.Progression.Type == "Linear");
        var initialProgression = linearExercise.Progression as LinearProgressionDto;
        var initialTrainingMax = initialProgression!.TrainingMax.Value;

        // Complete Day 1 with high AMRAP (should increase TM)
        await CompleteDayWithHighAmrapAsync(client, workoutId, 1, workout);

        var afterCompletion = await GetWorkoutAsync(client);
        var afterCompletionExercise = afterCompletion!.Exercises.First(e => e.Id == linearExercise.Id);
        var afterCompletionProgression = afterCompletionExercise.Progression as LinearProgressionDto;
        var afterCompletionTm = afterCompletionProgression!.TrainingMax.Value;

        // Verify TM increased after completion
        afterCompletionTm.Should().BeGreaterThan(initialTrainingMax,
            "Training max should have increased from high AMRAP performance");

        // Act - Undo the completion
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - TM should be restored to original value
        var restoredWorkout = await GetWorkoutAsync(client);
        var restoredExercise = restoredWorkout!.Exercises.First(e => e.Id == linearExercise.Id);
        var restoredProgression = restoredExercise.Progression as LinearProgressionDto;

        restoredProgression!.TrainingMax.Value.Should().Be(initialTrainingMax,
            "Training max should be restored to pre-completion value after undo");
    }

    /// <summary>
    /// Undoing should restore RepsPerSet progression state (current sets, weight, etc).
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_RestoresRepsPerSetProgressionState()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithRepsPerSetAsync(client);

        var workout = await GetWorkoutAsync(client);
        var rpsExercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");
        var initialProgression = rpsExercise.Progression as RepsPerSetProgressionDto;
        var initialSetCount = initialProgression!.CurrentSetCount;

        // Complete Day 1 with success (should add a set)
        await CompleteDayWithRpsSuccessAsync(client, workoutId, 1, workout);

        var afterCompletion = await GetWorkoutAsync(client);
        var afterCompletionExercise = afterCompletion!.Exercises.First(e => e.Id == rpsExercise.Id);
        var afterCompletionProgression = afterCompletionExercise.Progression as RepsPerSetProgressionDto;
        var afterCompletionSetCount = afterCompletionProgression!.CurrentSetCount;

        // Verify set count increased
        afterCompletionSetCount.Should().BeGreaterThan(initialSetCount,
            "Set count should increase after successful completion");

        // Act - Undo the completion
        var response = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Set count should be restored
        var restoredWorkout = await GetWorkoutAsync(client);
        var restoredExercise = restoredWorkout!.Exercises.First(e => e.Id == rpsExercise.Id);
        var restoredProgression = restoredExercise.Progression as RepsPerSetProgressionDto;

        restoredProgression!.CurrentSetCount.Should().Be(initialSetCount,
            "Set count should be restored to pre-completion value after undo");
    }

    #endregion

    #region Undo Then Re-Complete Tests

    /// <summary>
    /// After undoing, re-completing the day should work correctly.
    /// </summary>
    [Fact]
    public async Task UndoLastCompletion_ThenReCompleteDay_WorksCorrectly()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutAsync(client);

        var workout = await GetWorkoutAsync(client);

        // Complete Day 1
        await CompleteDayAsync(client, workoutId, 1, workout!);

        // Undo Day 1
        var undoResponse = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);
        undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify we're back at Day 1
        var workoutAfterUndo = await GetWorkoutAsync(client);
        workoutAfterUndo!.CurrentDay.Should().Be(1);

        // Act - Re-complete Day 1
        await CompleteDayAsync(client, workoutId, 1, workoutAfterUndo);

        // Assert - Should be at Day 2 again
        var finalWorkout = await GetWorkoutAsync(client);
        finalWorkout!.CurrentDay.Should().BeGreaterThan(1);
        finalWorkout.CompletedDaysInCurrentWeek.Should().Contain(1);
    }

    /// <summary>
    /// After undoing and rolling back week, re-completing all days should progress week again.
    /// </summary>
    [Fact]
    public async Task UndoWithWeekRollback_ThenReComplete_ProgressesWeekAgain()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateFourDayTestWorkoutAsync(client);

        // Complete Week 1 to get to Week 2
        await CompleteEntireWeekAsync(client, workoutId);

        var atWeek2 = await GetWorkoutAsync(client);
        atWeek2!.CurrentWeek.Should().Be(2);

        // Undo last completion (rolls back to Week 1)
        var undoResponse = await client.PostAsync(
            $"/api/v1/workouts/{workoutId}/undo-last-completion",
            null);
        undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterUndo = await GetWorkoutAsync(client);
        afterUndo!.CurrentWeek.Should().Be(1);
        afterUndo.CurrentDay.Should().Be(4);

        // Act - Complete Day 4 again
        await CompleteDayAsync(client, workoutId, 4, afterUndo);

        // Assert - Should be back at Week 2
        var afterReComplete = await GetWorkoutAsync(client);
        afterReComplete!.CurrentWeek.Should().Be(2);
        afterReComplete.CurrentDay.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Squat (Barbell)",
                HevyExerciseTemplateId = $"squat-undo-test-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Undo Test Workout",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create workout: {response.StatusCode}. {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private async Task<Guid> CreateFourDayTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Squat (Barbell)",
                HevyExerciseTemplateId = $"squat-fourday-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new CreateExerciseRequest
            {
                TemplateName = "Bench Press (Barbell)",
                HevyExerciseTemplateId = $"bench-fourday-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 80m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new CreateExerciseRequest
            {
                TemplateName = "Deadlift (Barbell)",
                HevyExerciseTemplateId = $"deadlift-fourday-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                TrainingMaxValue = 120m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new CreateExerciseRequest
            {
                TemplateName = "Overhead Press (Barbell)",
                HevyExerciseTemplateId = $"ohp-fourday-{Guid.NewGuid()}",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                TrainingMaxValue = 60m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Four Day Undo Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create workout: {response.StatusCode}. {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private async Task<Guid> CreateTestWorkoutWithRepsPerSetAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Lat Pulldown (Cable)",
                HevyExerciseTemplateId = $"lat-pulldown-undo-{Guid.NewGuid()}",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 50m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5,
                IsUnilateral = false
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "RepsPerSet Undo Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create workout: {response.StatusCode}. {error}");
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

    private async Task CompleteDayAsync(HttpClient client, Guid workoutId, int day, WorkoutDto workout)
    {
        var exercises = workout.Exercises.Where(e => (int)e.AssignedDay == day);
        var performances = exercises.Select(e => CreatePerformanceForExercise(e)).ToList();

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/{day}/complete", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to complete day {day}: {response.StatusCode}. {error}");
        }
    }

    private async Task CompleteDayWithHighAmrapAsync(HttpClient client, Guid workoutId, int day, WorkoutDto workout)
    {
        var exercises = workout.Exercises.Where(e => (int)e.AssignedDay == day);
        var performances = exercises.Select(e => CreateHighAmrapPerformanceForExercise(e)).ToList();

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/{day}/complete", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task CompleteDayWithRpsSuccessAsync(HttpClient client, Guid workoutId, int day, WorkoutDto workout)
    {
        var exercises = workout.Exercises.Where(e => (int)e.AssignedDay == day);
        var performances = exercises.Select(e => CreateRpsSuccessPerformanceForExercise(e)).ToList();

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/{day}/complete", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task CompleteEntireWeekAsync(HttpClient client, Guid workoutId)
    {
        for (int day = 1; day <= 4; day++)
        {
            var workout = await GetWorkoutAsync(client);
            await CompleteDayAsync(client, workoutId, day, workout!);
        }
    }

    private static ExercisePerformanceRequest CreatePerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression is LinearProgressionDto linear)
        {
            var weight = linear.TrainingMax.Value * 0.7m;
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = weight,
                    WeightUnit = (WeightUnit)linear.TrainingMax.Unit,
                    ActualReps = isAmrap ? 10 : 8, // Neutral AMRAP (0 RTF adjustment)
                    WasAmrap = isAmrap
                });
            }
        }
        else if (exercise.Progression is RepsPerSetProgressionDto rps)
        {
            var weightUnit = rps.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= rps.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = rps.RepRange.Target, // Target reps (neutral)
                    WasAmrap = false
                });
            }
        }
        else if (exercise.Progression is MinimalSetsProgressionDto minimal)
        {
            var weightUnit = minimal.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            var repsPerSet = minimal.TargetTotalReps / minimal.CurrentSetCount;
            for (int i = 1; i <= minimal.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = repsPerSet,
                    WasAmrap = false
                });
            }
        }

        return new ExercisePerformanceRequest
        {
            ExerciseId = exercise.Id,
            CompletedSets = sets,
            WasTemporarySubstitution = false
        };
    }

    private static ExercisePerformanceRequest CreateHighAmrapPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression is LinearProgressionDto linear)
        {
            var weight = linear.TrainingMax.Value * 0.7m;
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = weight,
                    WeightUnit = (WeightUnit)linear.TrainingMax.Unit,
                    ActualReps = isAmrap ? 20 : 10, // Very high AMRAP (+5 RTF = +3% TM)
                    WasAmrap = isAmrap
                });
            }
        }
        else
        {
            return CreatePerformanceForExercise(exercise);
        }

        return new ExercisePerformanceRequest
        {
            ExerciseId = exercise.Id,
            CompletedSets = sets,
            WasTemporarySubstitution = false
        };
    }

    private static ExercisePerformanceRequest CreateRpsSuccessPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression is RepsPerSetProgressionDto rps)
        {
            var weightUnit = rps.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            for (int i = 1; i <= rps.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = rps.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = rps.RepRange.Maximum, // Max reps = success = add set
                    WasAmrap = false
                });
            }
        }
        else
        {
            return CreatePerformanceForExercise(exercise);
        }

        return new ExercisePerformanceRequest
        {
            ExerciseId = exercise.Id,
            CompletedSets = sets,
            WasTemporarySubstitution = false
        };
    }

    #endregion
}
