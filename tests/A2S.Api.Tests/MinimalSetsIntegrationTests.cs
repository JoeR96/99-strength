using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using A2S.Tests.Shared.Helpers;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for MinimalSets progression through the API.
/// Validates set count adjustments: SUCCESS (reduce), FAILED (increase), MAINTAINED (no change).
/// </summary>
[Collection("Integration")]
public class MinimalSetsIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public MinimalSetsIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    /// <summary>
    /// MinimalSets exercise over 6 weeks with SUCCESS, FAILED, and MAINTAINED outcomes.
    /// Exercise: TargetTotalReps=40, StartingSets=3, StartingWeight=30kg.
    ///
    /// Week 1: MAINTAINED — 3 sets, 40 total reps (14+13+13) → stays at 3 sets
    /// Week 2: SUCCESS — 2 sets, 40 total reps (20+20) → reduce to 2 sets
    /// Week 3: FAILED — 2 sets, 30 total reps (15+15) → increase to 3 sets
    /// Week 4: SUCCESS — 2 sets, 40 total reps (20+20) → reduce to 2 sets
    /// Week 5: FAILED — 2 sets, 35 total reps (18+17) → increase to 3 sets
    /// Week 6: MAINTAINED — 3 sets, 45 total reps (15+15+15) → stays at 3 sets
    /// </summary>
    [Fact]
    public async Task MinimalSets_MultiWeek_SuccessReducesSetsAndFailureAddsSets()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateMinimalSetsTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        var dips = workout!.Exercises.First(e => e.Name == "Triceps Dip (Assisted)");
        var minimalProg = dips.Progression as MinimalSetsProgressionDto;
        minimalProg.Should().NotBeNull();
        minimalProg!.CurrentSetCount.Should().Be(3, "Should start with 3 sets");
        minimalProg.TargetTotalReps.Should().Be(40, "Target should be 40 total reps");

        // Week scenarios: (repsPerSet[], expectedSetsAfter, description)
        // SUCCESS: total reps >= target AND sets submitted < currentSetCount → reduce set
        // MAINTAINED: total reps >= target AND sets submitted == currentSetCount → no change
        // FAILED: total reps < target → add set
        var weekScenarios = new (int[] repsPerSet, int expectedSets, string desc)[]
        {
            (new[] { 14, 13, 13 }, 3, "MAINTAINED: 40 reps in 3 sets (== current 3) → stays 3"),
            (new[] { 20, 20 },     2, "SUCCESS: 40 reps in 2 sets (< current 3) → reduce to 2"),
            (new[] { 15, 15 },     3, "FAILED: 30 reps in 2 sets < 40 target → increase to 3"),
            (new[] { 20, 20 },     2, "SUCCESS: 40 reps in 2 sets (< current 3) → reduce to 2"),
            (new[] { 18, 17 },     3, "FAILED: 35 reps in 2 sets < 40 target → increase to 3"),
            (new[] { 15, 15, 15 }, 3, "MAINTAINED: 45 reps in 3 sets (== current 3) → stays 3"),
        };

        // Act & Assert — complete 6 weeks
        for (int weekIdx = 0; weekIdx < weekScenarios.Length; weekIdx++)
        {
            var week = weekIdx + 1;
            var scenario = weekScenarios[weekIdx];

            workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(week, $"Should be at week {week}");

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Triceps Dip (Assisted)")
                    {
                        performances.Add(CreateMinimalSetsPerformance(exercise, scenario.repsPerSet));
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
            }

            // Assert progression state after completing the week
            var updated = await GetCurrentWorkoutAsync(client);
            var updatedDips = updated!.Exercises.First(e => e.Name == "Triceps Dip (Assisted)");
            var updatedMinimal = updatedDips.Progression as MinimalSetsProgressionDto;

            updatedMinimal!.CurrentSetCount.Should().Be(scenario.expectedSets,
                $"Week {week}: {scenario.desc} → sets={scenario.expectedSets}");
        }
    }

    #region Helper Methods

    private async Task<Guid> CreateMinimalSetsTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            // Day 1: MinimalSets exercise under test
            new()
            {
                TemplateName = "Triceps Dip (Assisted)",
                ExternalTemplateId = "test-dips-minimal",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "MinimalSets",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 30m,
                WeightUnit = WeightUnit.Kilograms,
                TargetTotalReps = 40,
                StartingSets = 3
            },
            // Filler exercises on other days
            new()
            {
                TemplateName = "Bicep Curl (Cable)",
                ExternalTemplateId = "test-bicep-curl-minimal",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            new()
            {
                TemplateName = "Triceps Pushdown",
                ExternalTemplateId = "test-triceps-pushdown-minimal",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            new()
            {
                TemplateName = "Leg Extension (Machine)",
                ExternalTemplateId = "test-leg-ext-minimal",
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
            Name: "MinimalSets Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
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

    private static ExercisePerformanceRequest CreateMinimalSetsPerformance(
        ExerciseDto exercise, int[] repsPerSet)
    {
        var minimal = exercise.Progression as MinimalSetsProgressionDto;

        var setsList = new List<CompletedSetRequest>();
        for (int i = 0; i < repsPerSet.Length; i++)
        {
            setsList.Add(new CompletedSetRequest
            {
                SetNumber = i + 1,
                Weight = minimal!.CurrentWeight,
                WeightUnit = WeightUnit.Kilograms,
                ActualReps = repsPerSet[i],
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
}
