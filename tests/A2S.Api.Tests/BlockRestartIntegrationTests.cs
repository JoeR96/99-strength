using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.UpdateBlockSequence;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using A2S.Tests.Shared.Helpers;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for block restart after workout completion.
/// Validates that UpdateBlockSequence on a completed workout restarts the cycle
/// with TMs carried forward and week reset to 1.
/// </summary>
[Collection("Integration")]
public class BlockRestartIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public BlockRestartIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    /// <summary>
    /// Completes a short workout (1 block = 7 weeks), then restarts with a new block sequence.
    /// Verifies: workout returns to Active, week resets to 1, TMs carry forward from final values,
    /// and the restarted workout can be completed.
    /// </summary>
    [Fact]
    public async Task CompletedWorkout_UpdateBlockSequence_RestartsCycleWithCarriedForwardTms()
    {
        // Arrange — create a short workout (1 block = 7 weeks) for faster completion
        var client = CreateClient();
        var workoutId = await CreateShortTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        workout.Should().NotBeNull();
        workout!.TotalWeeks.Should().Be(7, "Single block workout should have 7 weeks");

        // Record the initial OHP TM
        var ohp = workout.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
        var initialTm = (ohp.Progression as LinearProgressionDto)!.TrainingMax.Value;
        initialTm.Should().Be(65m);

        // Complete all 7 weeks with consistent AMRAP results (+3 delta = +1.5% per week)
        CompleteDayResult? lastDayResult = null;
        for (int week = 1; week <= 7; week++)
        {
            workout = await GetCurrentWorkoutAsync(client);
            var isDeload = week == 7;

            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout!.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Overhead Press (Smith Machine)")
                    {
                        var linear = exercise.Progression as LinearProgressionDto;
                        // +3 delta for non-deload weeks → +1.5% TM increase
                        performances.Add(PerformanceRequestBuilders.CreateLinearPerformance(
                            exercise, week, isDeload, isDeload ? 0 : 3, linear!.BaseSetsPerExercise));
                    }
                    else
                    {
                        performances.Add(new ExercisePerformanceRequest
                        {
                            ExerciseId = exercise.Id,
                            CompletedSets = PerformanceRequestBuilders.CreateMaintainPerformance(exercise)
                        });
                    }
                }

                var response = await client.PostAsJsonAsync(
                    $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                    new { Performances = performances });
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"Week {week} Day {day} should complete successfully");
                lastDayResult = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
            }
        }

        // Verify workout is now complete
        lastDayResult.Should().NotBeNull();
        lastDayResult!.ProgramComplete.Should().BeTrue("Workout should be complete after all weeks");

        // Record the final TM before restart
        // After weeks 1-6 with +3 delta (+1.5% each): TM grows from 65.00
        // W1: 65 * 1.015 = 65.975 → 65.98
        // W2: 65.98 * 1.015 = 66.97 → 66.97
        // W3: 66.97 * 1.015 = 67.97 → 67.97
        // W4: 67.97 * 1.015 = 68.99 → 68.99
        // W5: 68.99 * 1.015 = 70.02 → 70.02
        // W6: 70.02 * 1.015 = 71.07 → 71.07
        // W7: DELOAD → 71.07

        // Get workout by listing all workouts (completed workout not returned by /current)
        var allWorkoutsResponse = await client.GetAsync("/api/v1/workouts");
        allWorkoutsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allWorkouts = await allWorkoutsResponse.Content.ReadFromJsonAsync<List<WorkoutSummaryDto>>();
        var completedWorkout = allWorkouts!.First(w => w.Id == workoutId);
        completedWorkout.Status.Should().Be("Completed");

        // Act — restart with a new block sequence [1, 2, 3]
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/block-sequence",
            new { blockSequence = new[] { 1, 2, 3 } });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"UpdateBlockSequence should succeed. Body: {await updateResponse.Content.ReadAsStringAsync()}");

        var updateResult = await updateResponse.Content.ReadFromJsonAsync<UpdateBlockSequenceResult>();
        updateResult.Should().NotBeNull();

        // Assert — workout restarted correctly
        updateResult!.TotalWeeks.Should().Be(21, "New block sequence [1,2,3] = 21 weeks");
        updateResult.CurrentWeek.Should().Be(1, "Week should reset to 1");

        // Verify the workout is now current again (Active status)
        var restartedWorkout = await GetCurrentWorkoutAsync(client);
        restartedWorkout.Should().NotBeNull("Restarted workout should be returned as current");
        restartedWorkout!.CurrentWeek.Should().Be(1, "Week should be 1 after restart");
        restartedWorkout.Status.Should().Be("Active", "Workout should be Active after restart");
        restartedWorkout.TotalWeeks.Should().Be(21);
        restartedWorkout.BlockSequence.Should().BeEquivalentTo(new[] { 1, 2, 3 });

        // Verify TMs carried forward (not reset to initial values)
        var restartedOhp = restartedWorkout.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
        var restartedTm = (restartedOhp.Progression as LinearProgressionDto)!.TrainingMax.Value;
        restartedTm.Should().BeGreaterThan(initialTm,
            "TM should carry forward from the completed cycle, not reset to initial");

        // Verify the restarted workout can be completed (first week)
        for (int day = 1; day <= 4; day++)
        {
            var dayNumber = (DayNumber)day;
            var dayExercises = restartedWorkout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

            var performances = new List<ExercisePerformanceRequest>();
            foreach (var exercise in dayExercises)
            {
                performances.Add(new ExercisePerformanceRequest
                {
                    ExerciseId = exercise.Id,
                    CompletedSets = PerformanceRequestBuilders.CreateMaintainPerformance(exercise)
                });
            }

            var response = await client.PostAsJsonAsync(
                $"/api/v1/workouts/{workoutId}/days/{day}/complete",
                new { Performances = performances });
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Restarted workout Day {day} should complete successfully");
        }

        // After completing week 1 of restarted workout, should be at week 2
        var afterWeek1 = await GetCurrentWorkoutAsync(client);
        afterWeek1!.CurrentWeek.Should().Be(2, "Should progress to week 2 after completing restarted week 1");
    }

    #region Helper Methods

    private async Task<Guid> CreateShortTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            // Day 1: OHP (Linear, TM=65)
            new()
            {
                TemplateName = "Overhead Press (Smith Machine)",
                HevyExerciseTemplateId = "test-ohp-block-restart",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 65m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            // Day 1: Filler
            new()
            {
                TemplateName = "Lat Pulldown (Cable)",
                HevyExerciseTemplateId = "test-lat-pulldown-block-restart",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                StartingWeight = 20m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 2: Filler
            new()
            {
                TemplateName = "Bicep Curl (Cable)",
                HevyExerciseTemplateId = "test-bicep-curl-block-restart",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 3: Filler
            new()
            {
                TemplateName = "Triceps Pushdown",
                HevyExerciseTemplateId = "test-triceps-pushdown-block-restart",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 4: Filler
            new()
            {
                TemplateName = "Leg Extension (Machine)",
                HevyExerciseTemplateId = "test-leg-ext-block-restart",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                StartingWeight = 25m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Block Restart Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 7,
            Exercises: exercises,
            BlockSequence: new List<int> { 1 }
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create workout. Status: {response.StatusCode}, Body: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private async Task<WorkoutDto?> GetCurrentWorkoutAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/workouts/current");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkoutDto>();
    }

    #endregion
}
