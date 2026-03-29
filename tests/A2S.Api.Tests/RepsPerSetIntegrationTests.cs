using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using A2S.Tests.Shared.Helpers;
using A2S.Tests.Shared.TestData;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for RepsPerSet progression through the API.
/// Validates bilateral and unilateral progression against hand-computed validation data
/// from RepsPerSetValidationData in SpreadsheetTestData.
/// </summary>
[Collection("Integration")]
public class RepsPerSetIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public RepsPerSetIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    /// <summary>
    /// Bilateral RepsPerSet (Barbell, +2.5kg) over 21 weeks.
    /// StartingSets=3, TargetSets=5, MaxSets=5, StartingWeight=60kg.
    /// Validates: SUCCESS→add set, MaxSets→weight increase+reset, MAINTAINED→no change, FAILED→remove set.
    /// </summary>
    [Fact]
    public async Task BilateralRepsPerSet_21Weeks_ValidatesSetAndWeightProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateRpsTestWorkoutAsync(client, isUnilateral: false);

        var scenarios = RepsPerSetValidationData.BilateralBarbellScenarios;

        for (int week = 1; week <= 21; week++)
        {
            var workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(week, $"Should be at week {week}");

            var scenario = scenarios[week - 1];

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Bent Over Row (Barbell)")
                    {
                        performances.Add(CreateRepsPerSetPerformance(exercise, scenario.Reps));
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

            // Assert progression state after completing all days
            if (week < 21)
            {
                var updated = await GetCurrentWorkoutAsync(client);
                var updatedRow = updated!.Exercises.First(e => e.Name == "Bent Over Row (Barbell)");
                var rps = updatedRow.Progression as RepsPerSetProgressionDto;

                rps!.CurrentSetCount.Should().Be(scenario.ExpectedSets,
                    $"Week {week}: {scenario.Description} → sets={scenario.ExpectedSets}");
                rps.CurrentWeight.Should().Be(scenario.ExpectedWeight,
                    $"Week {week}: {scenario.Description} → weight={scenario.ExpectedWeight}kg");
            }
        }
    }

    /// <summary>
    /// Unilateral RepsPerSet (Cable, +2.5kg) over 21 weeks.
    /// StartingSets=4, TargetSets=6, MaxSets=3 (unilateral cap).
    /// EffectiveMaxSets = min(6, 3) = 3. Since StartingSets(4) > EffectiveMaxSets(3),
    /// SUCCESS always increases weight and resets to StartingSets.
    /// Validates: weight increase pattern, unilateral capping, FAILED→drop set, sub-max SUCCESS→add set.
    /// </summary>
    [Fact]
    public async Task UnilateralRepsPerSet_21Weeks_ValidatesSetAndWeightProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateRpsTestWorkoutAsync(client, isUnilateral: true);

        var scenarios = RepsPerSetValidationData.UnilateralCableScenarios;

        for (int week = 1; week <= 21; week++)
        {
            var workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(week, $"Should be at week {week}");

            var scenario = scenarios[week - 1];

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayNumber = (DayNumber)day;
                var dayExercises = workout.Exercises.Where(e => e.AssignedDay == dayNumber).ToList();

                var performances = new List<ExercisePerformanceRequest>();
                foreach (var exercise in dayExercises)
                {
                    if (exercise.Name == "Single Arm Lat Pulldown")
                    {
                        performances.Add(CreateRepsPerSetPerformance(exercise, scenario.Reps));
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

            // Assert progression state after completing all days
            if (week < 21)
            {
                var updated = await GetCurrentWorkoutAsync(client);
                var updatedLat = updated!.Exercises.First(e => e.Name == "Single Arm Lat Pulldown");
                var rps = updatedLat.Progression as RepsPerSetProgressionDto;

                rps!.CurrentSetCount.Should().Be(scenario.ExpectedSets,
                    $"Week {week}: {scenario.Description} → sets={scenario.ExpectedSets}");
                rps.CurrentWeight.Should().Be(scenario.ExpectedWeight,
                    $"Week {week}: {scenario.Description} → weight={scenario.ExpectedWeight}kg");
                rps.IsUnilateral.Should().BeTrue(
                    $"Week {week}: Should remain marked as unilateral");
            }
        }
    }

    #region Helper Methods

    private async Task<Guid> CreateRpsTestWorkoutAsync(HttpClient client, bool isUnilateral)
    {
        var exercises = new List<CreateExerciseRequest>();

        if (!isUnilateral)
        {
            // Bilateral test: Bent Over Row on Day 1
            exercises.Add(new CreateExerciseRequest
            {
                TemplateName = "Bent Over Row (Barbell)",
                ExternalTemplateId = "test-bent-over-row-rps",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 60m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5,
                IsUnilateral = false
            });
        }
        else
        {
            // Unilateral test: Single Arm Lat Pulldown on Day 1
            exercises.Add(new CreateExerciseRequest
            {
                TemplateName = "Single Arm Lat Pulldown",
                ExternalTemplateId = "test-lat-pulldown-rps",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 79m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 4,
                TargetSets = 6,
                IsUnilateral = true
            });
        }

        // Filler exercises on other days
        exercises.Add(new CreateExerciseRequest
        {
            TemplateName = "Bicep Curl (Cable)",
            ExternalTemplateId = "test-bicep-curl-rps",
            Category = ExerciseCategory.Accessory,
            ProgressionType = "RepsPerSet",
            AssignedDay = DayNumber.Day2,
            OrderInDay = 1,
            StartingWeight = 15m,
            WeightUnit = WeightUnit.Kilograms,
            StartingSets = 3,
            TargetSets = 5
        });
        exercises.Add(new CreateExerciseRequest
        {
            TemplateName = "Triceps Pushdown",
            ExternalTemplateId = "test-triceps-pushdown-rps",
            Category = ExerciseCategory.Accessory,
            ProgressionType = "RepsPerSet",
            AssignedDay = DayNumber.Day3,
            OrderInDay = 1,
            StartingWeight = 15m,
            WeightUnit = WeightUnit.Kilograms,
            StartingSets = 3,
            TargetSets = 5
        });
        exercises.Add(new CreateExerciseRequest
        {
            TemplateName = "Leg Extension (Machine)",
            ExternalTemplateId = "test-leg-ext-rps",
            Category = ExerciseCategory.Accessory,
            ProgressionType = "RepsPerSet",
            AssignedDay = DayNumber.Day4,
            OrderInDay = 1,
            StartingWeight = 25m,
            WeightUnit = WeightUnit.Kilograms,
            StartingSets = 3,
            TargetSets = 5
        });

        var command = new CreateWorkoutCommand(
            Name: isUnilateral ? "Unilateral RPS Test" : "Bilateral RPS Test",
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

    private static ExercisePerformanceRequest CreateRepsPerSetPerformance(ExerciseDto exercise, int reps)
    {
        var rps = exercise.Progression as RepsPerSetProgressionDto;
        var weightUnit = rps!.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;

        var setsList = new List<CompletedSetRequest>();
        for (int i = 1; i <= rps.CurrentSetCount; i++)
        {
            var actualReps = reps == RepsPerSetWeekScenario.FailedWeekSentinel
                ? (i == 1 ? 7 : 10)  // FAILED: first set below min (8), rest maintained
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
}
