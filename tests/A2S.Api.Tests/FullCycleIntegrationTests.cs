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
/// Full 21-week cycle integration tests for LinearProgression.
/// Validates TM progression for OHP (TM=65) and Smith Squat (TM=110) against A2S2_Validation_Data.md.
/// </summary>
[Collection("Integration")]
public class FullCycleIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public FullCycleIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    /// <summary>
    /// Full 21-week LinearProgression cycle with TWO exercises:
    /// - OHP (TM=65): Weeks 1-6 AMRAP=19, weeks 7+ no AMRAP (TM frozen at 76.86kg)
    /// - Smith Squat (TM=110): Hand-crafted deltas testing ALL 8 TM adjustment multipliers
    ///   (×0.95, ×0.98, ×1.0, ×1.005, ×1.01, ×1.015, ×1.02, ×1.03)
    ///
    /// Verifies: TM progression per week, deload weeks have no AMRAP, workout reaches Completed status.
    /// </summary>
    [Fact]
    public async Task Full21WeekLinearProgression_WithOhpAndSmithSquat_ValidatesTmProgressionAndCompletion()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateLinearTestWorkoutAsync(client);

        var workout = await GetCurrentWorkoutAsync(client);
        workout.Should().NotBeNull();

        // OHP: all weeks 1-6 have AMRAP = 19
        // Deltas = AMRAP - repOutTarget: +4, +6, +7, +6, +7, +8, deload, 0, 0, ...
        var ohpDeltas = new[]
        {
            +4, +6, +7, +6, +7, +8, 0,   // Block 1 (week 7 = deload)
             0,  0,  0,  0,  0,  0, 0,    // Block 2 (no AMRAP entered, week 14 = deload)
             0,  0,  0,  0,  0,  0, 0     // Block 3 (no AMRAP entered, week 21 = deload)
        };

        // Smith Squat: hand-crafted to exercise all 8 multipliers
        var smithSquatDeltas = new[]
        {
            +5, +2, +2, +2, +2, +1, 0,   // Block 1
            +3, +4,  0, -1, -3,  0, 0,   // Block 2
             0,  0,  0,  0,  0,  0, 0    // Block 3
        };

        var ohp = workout!.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
        var smithSquat = workout.Exercises.First(e => e.Name == "Squat (Smith Machine)");

        var ohpLinear = ohp.Progression as LinearProgressionDto;
        var smithLinear = smithSquat.Progression as LinearProgressionDto;
        ohpLinear.Should().NotBeNull();
        smithLinear.Should().NotBeNull();
        ohpLinear!.TrainingMax.Value.Should().Be(65m);
        smithLinear!.TrainingMax.Value.Should().Be(110m);

        // Pre-computed TM sequences (verified against CalculateExpectedTmSequence logic)
        var expectedOhpTms = new[] { 65m, 66.30m, 68.29m, 70.34m, 72.45m, 74.62m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m, 76.86m };
        var expectedSmithTms = new[] { 110m, 113.30m, 114.43m, 115.57m, 116.73m, 117.90m, 118.49m, 118.49m, 120.27m, 122.68m, 122.68m, 120.23m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m, 114.22m };

        CompleteDayResult? lastDayResult = null;

        // Act & Assert — complete all 21 weeks
        for (int week = 1; week <= 21; week++)
        {
            workout = await GetCurrentWorkoutAsync(client);
            workout!.CurrentWeek.Should().Be(week, $"Should be at week {week}");

            var isDeload = week == 7 || week == 14 || week == 21;

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
                        var linear = exercise.Progression as LinearProgressionDto;
                        performances.Add(PerformanceRequestBuilders.CreateLinearPerformance(
                            exercise, week, isDeload, ohpDeltas[week - 1], linear!.BaseSetsPerExercise));
                    }
                    else if (exercise.Name == "Squat (Smith Machine)")
                    {
                        var linear = exercise.Progression as LinearProgressionDto;
                        performances.Add(PerformanceRequestBuilders.CreateLinearPerformance(
                            exercise, week, isDeload, smithSquatDeltas[week - 1], linear!.BaseSetsPerExercise));
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
                    $"Week {week} Day {day} should complete successfully. Body: {await response.Content.ReadAsStringAsync()}");

                lastDayResult = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
            }

            // Assert TM values after completing the week (except week 21 where workout is Completed)
            if (week < 21)
            {
                var updated = await GetCurrentWorkoutAsync(client);

                var updatedOhp = updated!.Exercises.First(e => e.Name == "Overhead Press (Smith Machine)");
                var updatedOhpLinear = updatedOhp.Progression as LinearProgressionDto;
                updatedOhpLinear!.TrainingMax.Value.Should().Be(expectedOhpTms[week],
                    $"Week {week} OHP TM: delta={ohpDeltas[week - 1]}, deload={isDeload}");

                var updatedSmith = updated.Exercises.First(e => e.Name == "Squat (Smith Machine)");
                var updatedSmithLinear = updatedSmith.Progression as LinearProgressionDto;
                updatedSmithLinear!.TrainingMax.Value.Should().Be(expectedSmithTms[week],
                    $"Week {week} Smith Squat TM: delta={smithSquatDeltas[week - 1]}, deload={isDeload}");

                // Verify deload weeks had no AMRAP impact (TM unchanged)
                if (isDeload)
                {
                    updatedOhpLinear.TrainingMax.Value.Should().Be(expectedOhpTms[week - 1],
                        $"Week {week} OHP TM should be unchanged during deload");
                    updatedSmithLinear.TrainingMax.Value.Should().Be(expectedSmithTms[week - 1],
                        $"Week {week} Smith Squat TM should be unchanged during deload");
                }
            }
        }

        // Assert — workout should be Completed after week 21
        lastDayResult.Should().NotBeNull();
        lastDayResult!.ProgramComplete.Should().BeTrue("Program should be complete after week 21");

        // Verify the workout is no longer "current" (status = Completed)
        var finalResponse = await client.GetAsync("/api/v1/workouts/current");
        finalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Completed workout should not be returned as 'current'");
    }

    #region Helper Methods

    private async Task<Guid> CreateLinearTestWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            // Day 1: OHP (Linear, TM=65)
            new()
            {
                TemplateName = "Overhead Press (Smith Machine)",
                ExternalTemplateId = "test-ohp-full-cycle",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 65m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            // Day 1: Filler RPS exercise
            new()
            {
                TemplateName = "Lat Pulldown (Cable)",
                ExternalTemplateId = "test-lat-pulldown-full-cycle",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                StartingWeight = 20m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 2: Smith Squat (Linear, TM=110)
            new()
            {
                TemplateName = "Squat (Smith Machine)",
                ExternalTemplateId = "test-smith-squat-full-cycle",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 110m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            // Day 2: Filler RPS exercise
            new()
            {
                TemplateName = "Lying Leg Curl (Machine)",
                ExternalTemplateId = "test-leg-curl-full-cycle",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 2,
                StartingWeight = 30m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 3: Filler RPS exercise
            new()
            {
                TemplateName = "Bicep Curl (Cable)",
                ExternalTemplateId = "test-bicep-curl-full-cycle",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                StartingWeight = 15m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            // Day 4: Filler RPS exercise
            new()
            {
                TemplateName = "Leg Extension (Machine)",
                ExternalTemplateId = "test-leg-ext-full-cycle",
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
            Name: "Full Cycle Linear Test",
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



    #endregion
}
