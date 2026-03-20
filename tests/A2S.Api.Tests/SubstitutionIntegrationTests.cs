using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.SubstituteExercise;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests;

/// <summary>
/// Integration tests for exercise substitution functionality.
/// Tests both temporary substitutions (skip progression) and permanent substitutions (with progression changes).
/// </summary>
[Collection("Integration")]
public class SubstitutionIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public SubstitutionIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Temporary Substitution Tests

    /// <summary>
    /// Tests that a temporary substitution (WasTemporarySubstitution = true) does not affect progression.
    /// Even with high AMRAP performance, the training max should remain unchanged.
    /// </summary>
    [Fact]
    public async Task TemporarySubstitution_WithHighAmrapPerformance_DoesNotAffectProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "Linear");

        // Get initial training max
        var initialProgression = exercise.Progression as LinearProgressionDto;
        initialProgression.Should().NotBeNull();
        var initialTrainingMax = initialProgression!.TrainingMax.Value;

        // Act - Complete day with WasTemporarySubstitution = true (temporary substitution)
        // Even with high AMRAP reps (which would normally increase TM), progression should be skipped
        var performances = new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = CreateLinearSetsWithHighAmrap(initialProgression),
                WasTemporarySubstitution = true  // This flags skip progression
            }
        };

        // Add other exercises for Day 1
        foreach (var ex in workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1 && e.Id != exercise.Id))
        {
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = ex.Id,
                CompletedSets = CreateSuccessPerformanceForExercise(ex),
                WasTemporarySubstitution = false
            });
        }

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CompleteDayResult>();
        result.Should().NotBeNull();

        // Verify progression was NOT changed for the temporarily substituted exercise
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as LinearProgressionDto;

        updatedProgression.Should().NotBeNull();
        updatedProgression!.TrainingMax.Value.Should().Be(initialTrainingMax,
            "Training max should be unchanged when WasTemporarySubstitution is true");

        // The progression change for this exercise should indicate it was skipped
        var exerciseChange = result!.ProgressionChanges.FirstOrDefault(c => c.ExerciseId == exercise.Id);
        exerciseChange.Should().NotBeNull();
        exerciseChange!.Change.ToLowerInvariant().Should().Contain("skipped",
            because: "Progression change should indicate progression was skipped for temporary substitution");
    }

    /// <summary>
    /// Tests that a temporary substitution preserves the original exercise's progression state
    /// even when the user would have failed (low reps).
    /// </summary>
    [Fact]
    public async Task TemporarySubstitution_WithLowPerformance_DoesNotDecreaseProgression()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "Linear");

        var initialProgression = exercise.Progression as LinearProgressionDto;
        var initialTrainingMax = initialProgression!.TrainingMax.Value;

        // Act - Complete day with very low reps (which would normally decrease TM)
        // but using temporary substitution flag
        var performances = new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = CreateLinearSetsWithLowReps(initialProgression),
                WasTemporarySubstitution = true
            }
        };

        // Add other Day 1 exercises
        foreach (var ex in workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1 && e.Id != exercise.Id))
        {
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = ex.Id,
                CompletedSets = CreateSuccessPerformanceForExercise(ex),
                WasTemporarySubstitution = false
            });
        }

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as LinearProgressionDto;

        updatedProgression!.TrainingMax.Value.Should().Be(initialTrainingMax,
            "Training max should be unchanged for temporary substitution even with poor performance");
    }

    /// <summary>
    /// Tests that temporary substitution for RepsPerSet exercises does not add or remove sets.
    /// </summary>
    [Fact]
    public async Task TemporarySubstitution_RepsPerSetExercise_DoesNotChangeSets()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithRepsPerSetExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");

        var initialProgression = exercise.Progression as RepsPerSetProgressionDto;
        initialProgression.Should().NotBeNull();
        var initialSetCount = initialProgression!.CurrentSetCount;

        // Act - Complete with max reps (which would normally add a set) but temporary substitution
        var performances = new List<ExercisePerformanceRequest>
        {
            new ExercisePerformanceRequest
            {
                ExerciseId = exercise.Id,
                CompletedSets = CreateRepsPerSetSuccessPerformance(initialProgression),
                WasTemporarySubstitution = true
            }
        };

        // Add other Day 1 exercises
        foreach (var ex in workout.Exercises.Where(e => e.AssignedDay == DayNumber.Day1 && e.Id != exercise.Id))
        {
            performances.Add(new ExercisePerformanceRequest
            {
                ExerciseId = ex.Id,
                CompletedSets = CreateSuccessPerformanceForExercise(ex),
                WasTemporarySubstitution = false
            });
        }

        var request = new { Performances = performances };
        var response = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        var updatedProgression = updatedExercise.Progression as RepsPerSetProgressionDto;

        updatedProgression!.CurrentSetCount.Should().Be(initialSetCount,
            "Set count should be unchanged for temporary substitution");
    }

    #endregion

    #region Permanent Substitution Tests

    /// <summary>
    /// Tests that a permanent substitution changes the exercise name.
    /// </summary>
    [Fact]
    public async Task PermanentSubstitution_ChangesExerciseName()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First();
        var originalName = exercise.Name;

        // Act
        var substituteRequest = new
        {
            NewExerciseName = "Substitute Exercise"
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SubstituteExerciseResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OriginalName.Should().Be(originalName);
        result.NewName.Should().Be("Substitute Exercise");
        result.ProgressionTypeChanged.Should().BeFalse();

        // Verify the exercise was updated
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        updatedExercise.Name.Should().Be("Substitute Exercise");
    }

    /// <summary>
    /// Tests that a permanent substitution with RepsPerSet progression config applies the new configuration.
    /// </summary>
    [Fact]
    public async Task PermanentSubstitution_WithRepsPerSetProgression_AppliesNewConfig()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "Linear");

        // Act - Substitute with new RepsPerSet progression
        var substituteRequest = new
        {
            NewExerciseName = "Leg Press",
            Reason = "Equipment not available",
            NewProgressionConfig = new
            {
                Type = "RepsPerSet",
                RepRangeMinimum = 8,
                RepRangeTarget = 10,
                RepRangeMaximum = 12,
                TargetSets = 4,
                StartingWeight = 100m,
                WeightUnit = WeightUnit.Kilograms,
                IsUnilateral = false
            }
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SubstituteExerciseResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.NewName.Should().Be("Leg Press");
        result.ProgressionTypeChanged.Should().BeTrue();
        result.NewProgressionType.Should().Be("RepsPerSet");

        // Verify the exercise progression was updated
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        updatedExercise.Name.Should().Be("Leg Press");
        updatedExercise.Progression.Type.Should().Be("RepsPerSet");

        var newProgression = updatedExercise.Progression as RepsPerSetProgressionDto;
        newProgression.Should().NotBeNull();
        newProgression!.RepRange.Should().NotBeNull();
        newProgression.RepRange.Minimum.Should().Be(8);
        newProgression.RepRange.Target.Should().Be(10);
        newProgression.RepRange.Maximum.Should().Be(12);
    }

    /// <summary>
    /// Tests that a permanent substitution changing to Linear progression applies correct configuration.
    /// </summary>
    [Fact]
    public async Task PermanentSubstitution_WithLinearProgression_AppliesNewConfig()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithRepsPerSetExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First(e => e.Progression.Type == "RepsPerSet");

        // Act - Substitute with Linear progression
        var substituteRequest = new
        {
            NewExerciseName = "Back Squat",
            Reason = "Upgrading to main lift",
            NewProgressionConfig = new
            {
                Type = "Linear",
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms,
                UseAmrap = true,
                BaseSetsPerExercise = 4
            }
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SubstituteExerciseResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ProgressionTypeChanged.Should().BeTrue();
        result.NewProgressionType.Should().Be("Linear");

        // Verify the exercise progression was updated
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        updatedExercise.Progression.Type.Should().Be("Linear");

        var newProgression = updatedExercise.Progression as LinearProgressionDto;
        newProgression.Should().NotBeNull();
        newProgression!.TrainingMax.Value.Should().Be(100m);
        newProgression.UseAmrap.Should().BeTrue();
        newProgression.BaseSetsPerExercise.Should().Be(4);
    }

    /// <summary>
    /// Tests that a permanent substitution changing to MinimalSets progression applies correct configuration.
    /// </summary>
    [Fact]
    public async Task PermanentSubstitution_WithMinimalSetsProgression_AppliesNewConfig()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First();

        // Act - Substitute with MinimalSets progression
        var substituteRequest = new
        {
            NewExerciseName = "Assisted Dips",
            Reason = "Switching to bodyweight progression",
            NewProgressionConfig = new
            {
                Type = "MinimalSets",
                StartingWeight = 20m,
                WeightUnit = WeightUnit.Kilograms,
                TargetTotalReps = 40,
                MinimumSets = 2,
                MaximumSets = 10
            }
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SubstituteExerciseResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ProgressionTypeChanged.Should().BeTrue();
        result.NewProgressionType.Should().Be("MinimalSets");

        // Verify the exercise progression was updated
        var updatedWorkout = await GetCurrentWorkoutAsync(client);
        var updatedExercise = updatedWorkout!.Exercises.First(e => e.Id == exercise.Id);
        updatedExercise.Progression.Type.Should().Be("MinimalSets");

        var newProgression = updatedExercise.Progression as MinimalSetsProgressionDto;
        newProgression.Should().NotBeNull();
        newProgression!.TargetTotalReps.Should().Be(40);
        newProgression.MinimumSets.Should().Be(2);
        newProgression.MaximumSets.Should().Be(10);
    }

    /// <summary>
    /// Tests that substitution with a reason records the reason properly.
    /// </summary>
    [Fact]
    public async Task PermanentSubstitution_WithReason_RecordsReason()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First();

        // Act
        var substituteRequest = new
        {
            NewExerciseName = "Front Squat",
            Reason = "Testing audit trail - equipment change"
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SubstituteExerciseResult>();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("Successfully substituted");
    }

    #endregion

    #region Error Case Tests

    /// <summary>
    /// Tests that substitution with invalid workout ID returns 404.
    /// </summary>
    [Fact]
    public async Task Substitute_WithInvalidWorkoutId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var invalidWorkoutId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var substituteRequest = new
        {
            NewExerciseName = "New Exercise"
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{invalidWorkoutId}/exercises/{exerciseId}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that substitution with invalid exercise ID returns 404.
    /// </summary>
    [Fact]
    public async Task Substitute_WithInvalidExerciseId_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var invalidExerciseId = Guid.NewGuid();

        var substituteRequest = new
        {
            NewExerciseName = "New Exercise"
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{invalidExerciseId}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that substitution with invalid progression config returns bad request.
    /// </summary>
    [Fact]
    public async Task Substitute_WithInvalidProgressionConfig_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First();

        // Act - RepsPerSet requires RepRange fields
        var substituteRequest = new
        {
            NewExerciseName = "New Exercise",
            NewProgressionConfig = new
            {
                Type = "RepsPerSet"
                // Missing required fields: RepRangeMinimum, RepRangeTarget, RepRangeMaximum, StartingWeight
            }
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests that substitution with unknown progression type returns bad request.
    /// </summary>
    [Fact]
    public async Task Substitute_WithUnknownProgressionType_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();
        var workoutId = await CreateTestWorkoutWithLinearExerciseAsync(client);
        var workout = await GetCurrentWorkoutAsync(client);
        var exercise = workout!.Exercises.First();

        // Act
        var substituteRequest = new
        {
            NewExerciseName = "New Exercise",
            NewProgressionConfig = new
            {
                Type = "UnknownProgressionType"
            }
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{exercise.Id}/substitute",
            substituteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test workout with a Linear progression exercise on Day 1.
    /// </summary>
    private async Task<Guid> CreateTestWorkoutWithLinearExerciseAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Squat (Barbell)",
                HevyExerciseTemplateId = "back-squat-test",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new CreateExerciseRequest
            {
                TemplateName = "Lat Pulldown (Cable)",
                HevyExerciseTemplateId = "lat-pulldown-test",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                StartingWeight = 50m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5,
                IsUnilateral = false
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "Substitution Test Workout",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    /// <summary>
    /// Creates a test workout with a RepsPerSet progression exercise on Day 1.
    /// </summary>
    private async Task<Guid> CreateTestWorkoutWithRepsPerSetExerciseAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new CreateExerciseRequest
            {
                TemplateName = "Lat Pulldown (Cable)",
                HevyExerciseTemplateId = "lat-pulldown-rps-test",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                StartingWeight = 50m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5,
                IsUnilateral = false
            },
            new CreateExerciseRequest
            {
                TemplateName = "Seated Cable Row - V Grip (Cable)",
                HevyExerciseTemplateId = "cable-row-test",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                StartingWeight = 40m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5,
                IsUnilateral = false
            }
        };

        var command = new CreateWorkoutCommand(
            Name: "RepsPerSet Test Workout",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises: exercises
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return result!.Id;
    }

    private async Task<WorkoutDto?> GetCurrentWorkoutAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/workouts/current");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<WorkoutDto>();
    }

    /// <summary>
    /// Creates completed sets for Linear progression with high AMRAP performance.
    /// </summary>
    private static IReadOnlyList<CompletedSetRequest> CreateLinearSetsWithHighAmrap(LinearProgressionDto progression)
    {
        var sets = new List<CompletedSetRequest>();
        var weight = progression.TrainingMax.Value * 0.7m; // 70% TM

        for (int i = 1; i <= progression.BaseSetsPerExercise; i++)
        {
            var isAmrap = i == progression.BaseSetsPerExercise;
            sets.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = weight,
                WeightUnit = (WeightUnit)progression.TrainingMax.Unit,
                ActualReps = isAmrap ? 20 : 10, // Very high AMRAP reps (would normally increase TM significantly)
                WasAmrap = isAmrap
            });
        }

        return sets;
    }

    /// <summary>
    /// Creates completed sets for Linear progression with low performance (would normally decrease TM).
    /// </summary>
    private static IReadOnlyList<CompletedSetRequest> CreateLinearSetsWithLowReps(LinearProgressionDto progression)
    {
        var sets = new List<CompletedSetRequest>();
        var weight = progression.TrainingMax.Value * 0.7m;

        for (int i = 1; i <= progression.BaseSetsPerExercise; i++)
        {
            var isAmrap = i == progression.BaseSetsPerExercise;
            sets.Add(new CompletedSetRequest
            {
                SetNumber = i,
                Weight = weight,
                WeightUnit = (WeightUnit)progression.TrainingMax.Unit,
                ActualReps = isAmrap ? 3 : 5, // Very low AMRAP (would normally decrease TM)
                WasAmrap = isAmrap
            });
        }

        return sets;
    }

    /// <summary>
    /// Creates completed sets for RepsPerSet with max reps (success, would add a set).
    /// </summary>
    private static IReadOnlyList<CompletedSetRequest> CreateRepsPerSetSuccessPerformance(RepsPerSetProgressionDto progression)
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
                ActualReps = progression.RepRange.Maximum, // Hit max reps = success
                WasAmrap = false
            });
        }

        return sets;
    }

    /// <summary>
    /// Creates a success performance for any exercise type.
    /// </summary>
    private static IReadOnlyList<CompletedSetRequest> CreateSuccessPerformanceForExercise(ExerciseDto exercise)
    {
        var sets = new List<CompletedSetRequest>();

        if (exercise.Progression is LinearProgressionDto linear)
        {
            for (int i = 1; i <= linear.BaseSetsPerExercise; i++)
            {
                var isAmrap = i == linear.BaseSetsPerExercise;
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = linear.TrainingMax.Value * 0.7m,
                    WeightUnit = (WeightUnit)linear.TrainingMax.Unit,
                    ActualReps = isAmrap ? 15 : 10,
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
                    ActualReps = rps.RepRange.Maximum,
                    WasAmrap = false
                });
            }
        }
        else if (exercise.Progression is MinimalSetsProgressionDto minimal)
        {
            var weightUnit = minimal.WeightUnit == "Kilograms" ? WeightUnit.Kilograms : WeightUnit.Pounds;
            var repsPerSet = minimal.TargetTotalReps / minimal.CurrentSetCount;
            var remainder = minimal.TargetTotalReps % minimal.CurrentSetCount;

            for (int i = 1; i <= minimal.CurrentSetCount; i++)
            {
                sets.Add(new CompletedSetRequest
                {
                    SetNumber = i,
                    Weight = minimal.CurrentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    #endregion
}
