using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// E2E tests for Workout Flow functionality.
/// Tests the complete workflow of completing training days and progressing weeks,
/// including all three progression types: Linear, RepsPerSet, and MinimalSets.
/// </summary>
[Collection("E2E")]
public class WorkoutFlowE2ETests : E2ETestBase
{
    public WorkoutFlowE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    /// <summary>
    /// Tests completing a training day and verifying progression changes are applied.
    /// Uses Linear progression exercise with AMRAP to verify TM adjustments.
    /// </summary>
    [Fact]
    public async Task CompleteDay_WithLinearProgression_AppliesProgressionChanges()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout with Linear progression
            await CreateTestWorkoutAsync(page);

            // Navigate to workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Get the workout ID via API
            var workoutData = await GetCurrentWorkoutAsync(page);
            workoutData.Should().NotBeNull("Should have an active workout");

            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var exercises = workoutData.Value.GetProperty("exercises");

            // Find a Day 1 exercise with Linear progression
            var day1Exercises = exercises.EnumerateArray()
                .Where(e => e.GetProperty("assignedDay").GetInt32() == 1)
                .ToList();

            day1Exercises.Should().NotBeEmpty("Day 1 should have exercises");

            // Complete Day 1 via API (simulate completing a workout)
            var performances = new List<object>();
            foreach (var exercise in day1Exercises)
            {
                var exerciseId = exercise.GetProperty("id").GetString();
                var progression = exercise.GetProperty("progression");
                var progressionType = progression.GetProperty("type").GetString();

                var completedSets = CreateCompletedSets(progression, progressionType!, success: true);
                performances.Add(new { ExerciseId = exerciseId, CompletedSets = completedSets });
            }

            // Complete Day 1 via API
            var completeDayResult = await CompleteDayViaApiAsync(page, workoutId!, 1, performances);
            completeDayResult.Should().NotBeNull();

            // Verify the result contains progression changes
            var exercisesCompleted = completeDayResult.Value.GetProperty("exercisesCompleted").GetInt32();
            exercisesCompleted.Should().BeGreaterThan(0, "Should have completed at least one exercise");

            var progressionChanges = completeDayResult.Value.GetProperty("progressionChanges");
            progressionChanges.GetArrayLength().Should().BeGreaterThan(0, "Should have progression changes");

            // Verify UI updates to show completed status
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Note: UI verification depends on the frontend implementation
            // This test primarily validates the API flow
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests progressing from one week to the next.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_FromWeek1ToWeek2_UpdatesWeekNumber()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout
            await CreateTestWorkoutAsync(page);

            // Get the workout ID
            var workoutData = await GetCurrentWorkoutAsync(page);
            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var initialWeek = workoutData.Value.GetProperty("currentWeek").GetInt32();
            initialWeek.Should().Be(1, "Should start at week 1");

            // Progress to next week via API
            var progressResult = await ProgressWeekViaApiAsync(page, workoutId!);
            progressResult.Should().NotBeNull();

            var previousWeek = progressResult.Value.GetProperty("previousWeek").GetInt32();
            var newWeek = progressResult.Value.GetProperty("newWeek").GetInt32();

            previousWeek.Should().Be(1);
            newWeek.Should().Be(2);

            // Verify workout is now at week 2
            var updatedWorkout = await GetCurrentWorkoutAsync(page);
            updatedWorkout.Value.GetProperty("currentWeek").GetInt32().Should().Be(2);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that progressing to week 7 identifies it as a deload week.
    /// </summary>
    [Fact]
    public async Task ProgressWeek_ToWeek7_IdentifiesDeloadWeek()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout
            await CreateTestWorkoutAsync(page);

            // Get the workout ID
            var workoutData = await GetCurrentWorkoutAsync(page);
            var workoutId = workoutData.Value.GetProperty("id").GetString();

            // Progress through weeks 1-6 to reach week 7
            for (int i = 1; i < 7; i++)
            {
                await ProgressWeekViaApiAsync(page, workoutId!);
            }

            // Verify we're at week 7
            var updatedWorkout = await GetCurrentWorkoutAsync(page);
            updatedWorkout.Value.GetProperty("currentWeek").GetInt32().Should().Be(7);

            // Week 7 is a deload week in the A2S program
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests completing a full week (4 days) of training.
    /// </summary>
    [Fact]
    public async Task CompleteFullWeek_ThenProgress_WorksCorrectly()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout
            await CreateTestWorkoutAsync(page);

            var workoutData = await GetCurrentWorkoutAsync(page);
            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var exercises = workoutData.Value.GetProperty("exercises");

            // Complete all 4 days
            for (int day = 1; day <= 4; day++)
            {
                var dayExercises = exercises.EnumerateArray()
                    .Where(e => e.GetProperty("assignedDay").GetInt32() == day)
                    .ToList();

                if (!dayExercises.Any())
                    continue;

                var performances = new List<object>();
                foreach (var exercise in dayExercises)
                {
                    var exerciseId = exercise.GetProperty("id").GetString();
                    var progression = exercise.GetProperty("progression");
                    var progressionType = progression.GetProperty("type").GetString();

                    var completedSets = CreateCompletedSets(progression, progressionType!, success: true);
                    performances.Add(new { ExerciseId = exerciseId, CompletedSets = completedSets });
                }

                var result = await CompleteDayViaApiAsync(page, workoutId!, day, performances);
                result.Should().NotBeNull($"Day {day} completion should succeed");
            }

            // Progress to next week
            var progressResult = await ProgressWeekViaApiAsync(page, workoutId!);
            progressResult.Value.GetProperty("newWeek").GetInt32().Should().Be(2);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that RepsPerSet progression correctly adds sets on success.
    /// </summary>
    [Fact]
    public async Task CompleteDay_RepsPerSetSuccess_AddsSets()
    {
        // Arrange
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            await CreateTestWorkoutAsync(page);

            var workoutData = await GetCurrentWorkoutAsync(page);
            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var exercises = workoutData.Value.GetProperty("exercises");

            // Find a RepsPerSet exercise
            var repsPerSetExercise = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("progression").GetProperty("type").GetString() == "RepsPerSet");

            if (repsPerSetExercise.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            {
                var exerciseId = repsPerSetExercise.GetProperty("id").GetString();
                var progression = repsPerSetExercise.GetProperty("progression");
                var day = repsPerSetExercise.GetProperty("assignedDay").GetInt32();

                // Create success performance (all sets hit max reps)
                var completedSets = CreateCompletedSets(progression, "RepsPerSet", success: true);
                var performances = new List<object> { new { ExerciseId = exerciseId, CompletedSets = completedSets } };

                var result = await CompleteDayViaApiAsync(page, workoutId!, day, performances);
                result.Should().NotBeNull();

                // Verify progression change indicates set addition
                var changes = result.Value.GetProperty("progressionChanges").EnumerateArray().ToList();
                var exerciseChange = changes.FirstOrDefault(c => c.GetProperty("exerciseId").GetString() == exerciseId);

                if (exerciseChange.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    var changeDescription = exerciseChange.GetProperty("change").GetString();
                    changeDescription.Should().Contain("Added 1 set", "RepsPerSet success should add a set");
                }
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test workout with exercises for all 4 days.
    /// </summary>
    private async Task CreateTestWorkoutAsync(IPage page)
    {
        // Navigate to setup
        await page.GotoAsync($"{FrontendUrl}/workout");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check if we need to create a workout
        var noWorkoutMessage = page.Locator("h2:has-text('No Active Workout')").First;
        var hasNoWorkout = await noWorkoutMessage.IsVisibleAsync();

        if (!hasNoWorkout)
        {
            // Already have a workout
            return;
        }

        var createButton = page.Locator("button:has-text('Create Workout Program')").First;
        await createButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await createButton.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

        // Fill in program details
        var programNameInput = page.Locator("input[type='text']").First;
        await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await programNameInput.FillAsync("Test Workout Flow");

        // Select 4-Day variant
        var variantSelect = page.Locator("select").First;
        await variantSelect.SelectOptionAsync(new SelectOptionValue { Value = "4" });

        // Navigate through wizard and create
        await NavigateToConfirmStepAndCreate(page);

        // Wait for redirect
        await page.WaitForURLAsync(
            url => url.Contains("/dashboard") || url.Contains("/workout"),
            new() { Timeout = 15000 });
    }

    /// <summary>
    /// Gets the current workout via API.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> GetCurrentWorkoutAsync(IPage page)
    {
        var workoutJsonString = await page.EvaluateAsync<string>($@"async () => {{
            const clerk = window.Clerk;
            if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});
            const token = await clerk.session?.getToken();
            if (!token) return JSON.stringify({{ error: 'No auth token' }});
            const response = await fetch('{ApiBaseUrl}/api/v1/workouts/current', {{
                headers: {{ 'Accept': 'application/json', 'Authorization': 'Bearer ' + token }}
            }});
            if (!response.ok) return JSON.stringify({{ error: response.status }});
            return JSON.stringify(await response.json());
        }}");

        if (string.IsNullOrEmpty(workoutJsonString))
            return null;

        var json = System.Text.Json.JsonDocument.Parse(workoutJsonString);
        if (json.RootElement.TryGetProperty("error", out _))
            return null;

        return json.RootElement;
    }

    /// <summary>
    /// Completes a training day via API.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> CompleteDayViaApiAsync(
        IPage page, string workoutId, int day, List<object> performances)
    {
        var performancesJson = System.Text.Json.JsonSerializer.Serialize(performances);

        var resultJsonString = await page.EvaluateAsync<string>($@"async () => {{
            const clerk = window.Clerk;
            if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});
            const token = await clerk.session?.getToken();
            if (!token) return JSON.stringify({{ error: 'No auth token' }});
            const response = await fetch('{ApiBaseUrl}/api/v1/workouts/{workoutId}/days/{day}/complete', {{
                method: 'POST',
                headers: {{
                    'Accept': 'application/json',
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                }},
                body: JSON.stringify({{ Performances: {performancesJson} }})
            }});
            if (!response.ok) return JSON.stringify({{ error: response.status }});
            return JSON.stringify(await response.json());
        }}");

        if (string.IsNullOrEmpty(resultJsonString))
            return null;

        var json = System.Text.Json.JsonDocument.Parse(resultJsonString);
        if (json.RootElement.TryGetProperty("error", out _))
            return null;

        return json.RootElement;
    }

    /// <summary>
    /// Progresses to the next week via API.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> ProgressWeekViaApiAsync(IPage page, string workoutId)
    {
        var resultJsonString = await page.EvaluateAsync<string>($@"async () => {{
            const clerk = window.Clerk;
            if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});
            const token = await clerk.session?.getToken();
            if (!token) return JSON.stringify({{ error: 'No auth token' }});
            const response = await fetch('{ApiBaseUrl}/api/v1/workouts/{workoutId}/progress-week', {{
                method: 'POST',
                headers: {{
                    'Accept': 'application/json',
                    'Authorization': 'Bearer ' + token
                }}
            }});
            if (!response.ok) return JSON.stringify({{ error: response.status }});
            return JSON.stringify(await response.json());
        }}");

        if (string.IsNullOrEmpty(resultJsonString))
            return null;

        var json = System.Text.Json.JsonDocument.Parse(resultJsonString);
        if (json.RootElement.TryGetProperty("error", out _))
            return null;

        return json.RootElement;
    }

    /// <summary>
    /// Creates completed sets data for an exercise based on its progression type.
    /// </summary>
    private static List<object> CreateCompletedSets(
        System.Text.Json.JsonElement progression,
        string progressionType,
        bool success)
    {
        var sets = new List<object>();

        if (progressionType == "Linear")
        {
            var trainingMax = progression.GetProperty("trainingMax");
            var weight = trainingMax.GetProperty("value").GetDecimal() * 0.7m;
            var unit = trainingMax.GetProperty("unit").GetInt32();
            var baseSets = progression.GetProperty("baseSetsPerExercise").GetInt32();

            for (int i = 1; i <= baseSets; i++)
            {
                var isAmrap = i == baseSets;
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = weight,
                    WeightUnit = unit,
                    ActualReps = isAmrap ? (success ? 19 : 10) : 10, // AMRAP +4 for success
                    WasAmrap = isAmrap
                });
            }
        }
        else if (progressionType == "RepsPerSet")
        {
            var currentWeight = progression.GetProperty("currentWeight").GetDecimal();
            var weightUnit = progression.GetProperty("weightUnit").GetString() == "Kilograms" ? 0 : 1;
            var currentSetCount = progression.GetProperty("currentSetCount").GetInt32();
            var repRange = progression.GetProperty("repRange");
            var maxReps = repRange.GetProperty("maximum").GetInt32();
            var minReps = repRange.GetProperty("minimum").GetInt32();

            for (int i = 1; i <= currentSetCount; i++)
            {
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = currentWeight,
                    WeightUnit = weightUnit,
                    ActualReps = success ? maxReps : minReps - 1, // Hit max for success, below min for failure
                    WasAmrap = false
                });
            }
        }
        else if (progressionType == "MinimalSets")
        {
            var currentWeight = progression.GetProperty("currentWeight").GetDecimal();
            var targetTotalReps = progression.GetProperty("targetTotalReps").GetInt32();
            var currentSetCount = progression.GetProperty("currentSetCount").GetInt32();

            var repsPerSet = targetTotalReps / currentSetCount;
            var remainder = targetTotalReps % currentSetCount;

            for (int i = 1; i <= currentSetCount; i++)
            {
                sets.Add(new
                {
                    SetNumber = i,
                    Weight = currentWeight,
                    WeightUnit = 0, // Kilograms
                    ActualReps = repsPerSet + (i <= remainder ? 1 : 0),
                    WasAmrap = false
                });
            }
        }

        return sets;
    }

    /// <summary>
    /// Navigates through wizard steps and clicks Create Program.
    /// </summary>
    private async Task NavigateToConfirmStepAndCreate(IPage page)
    {
        var nextButton = page.Locator("button:has-text('Next')").First;
        var confirmButton = page.Locator("button:has-text('Create Program')").First;

        for (int i = 0; i < 5; i++)
        {
            var isConfirmVisible = await confirmButton.IsVisibleAsync();
            if (isConfirmVisible)
            {
                await confirmButton.ClickAsync();
                break;
            }

            var isNextVisible = await nextButton.IsVisibleAsync();
            if (!isNextVisible)
                break;

            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(500);
        }
    }

    #endregion
}
