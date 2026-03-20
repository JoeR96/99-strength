using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// E2E tests for the undo workout functionality.
/// </summary>
[Collection("E2E")]
public class UndoWorkoutE2ETests : E2ETestBase
{
    public UndoWorkoutE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    [Fact]
    public async Task UndoWorkout_ThenResubmit_CompletesSuccessfully()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout
            await CreateTestWorkoutAsync(page);

            // Get the workout ID and exercises for Day 1
            var workoutData = await GetCurrentWorkoutAsync(page);
            workoutData.Should().NotBeNull("Should have an active workout");

            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var exercises = workoutData.Value.GetProperty("exercises");

            var day1Exercises = exercises.EnumerateArray()
                .Where(e => e.GetProperty("assignedDay").GetInt32() == 1)
                .ToList();

            day1Exercises.Should().NotBeEmpty("Day 1 should have exercises");

            // Complete Day 1 via API
            var performances = new List<object>();
            foreach (var exercise in day1Exercises)
            {
                var exerciseId = exercise.GetProperty("id").GetString();
                var progression = exercise.GetProperty("progression");
                var progressionType = progression.GetProperty("type").GetString();

                var completedSets = CreateCompletedSets(progression, progressionType!, success: true);
                performances.Add(new { ExerciseId = exerciseId, CompletedSets = completedSets });
            }

            var completeDayResult = await CompleteDayViaApiAsync(page, workoutId!, 1, performances);
            completeDayResult.Should().NotBeNull("Day completion should succeed");

            // Verify day is completed
            var workoutAfterComplete = await GetCurrentWorkoutAsync(page);
            var completedDaysAfterComplete = workoutAfterComplete.Value.GetProperty("completedDays").EnumerateArray().ToList();
            completedDaysAfterComplete.Should().Contain(d => d.GetInt32() == 1, "Day 1 should be completed");

            // Undo the last workout via API
            var undoResult = await UndoLastWorkoutViaApiAsync(page, workoutId!);
            undoResult.Should().NotBeNull("Undo should succeed");

            // Verify day is no longer completed
            var workoutAfterUndo = await GetCurrentWorkoutAsync(page);
            var completedDaysAfterUndo = workoutAfterUndo.Value.GetProperty("completedDays").EnumerateArray().ToList();
            completedDaysAfterUndo.Should().NotContain(d => d.GetInt32() == 1, "Day 1 should no longer be completed after undo");

            // Re-complete Day 1
            var reCompleteResult = await CompleteDayViaApiAsync(page, workoutId!, 1, performances);
            reCompleteResult.Should().NotBeNull("Re-completing day should succeed");

            // Verify final state - day is completed again
            var finalWorkout = await GetCurrentWorkoutAsync(page);
            var finalCompletedDays = finalWorkout.Value.GetProperty("completedDays").EnumerateArray().ToList();
            finalCompletedDays.Should().Contain(d => d.GetInt32() == 1, "Day 1 should be completed again");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task UndoWorkout_RevertsProgressionChanges()
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

            // Find a Linear progression exercise on Day 1
            var linearExercise = exercises.EnumerateArray()
                .FirstOrDefault(e =>
                    e.GetProperty("assignedDay").GetInt32() == 1 &&
                    e.GetProperty("progression").GetProperty("type").GetString() == "Linear");

            if (linearExercise.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            {
                var exerciseId = linearExercise.GetProperty("id").GetString();
                var progression = linearExercise.GetProperty("progression");
                var originalTM = progression.GetProperty("trainingMax").GetProperty("value").GetDecimal();

                // Complete day with successful performance
                var day1Exercises = exercises.EnumerateArray()
                    .Where(e => e.GetProperty("assignedDay").GetInt32() == 1)
                    .ToList();

                var performances = new List<object>();
                foreach (var exercise in day1Exercises)
                {
                    var exId = exercise.GetProperty("id").GetString();
                    var prog = exercise.GetProperty("progression");
                    var progType = prog.GetProperty("type").GetString();

                    var completedSets = CreateCompletedSets(prog, progType!, success: true);
                    performances.Add(new { ExerciseId = exId, CompletedSets = completedSets });
                }

                await CompleteDayViaApiAsync(page, workoutId!, 1, performances);

                // Check that progression changed
                var workoutAfterComplete = await GetCurrentWorkoutAsync(page);
                var updatedExercise = workoutAfterComplete.Value.GetProperty("exercises").EnumerateArray()
                    .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);
                var updatedTM = updatedExercise.GetProperty("progression").GetProperty("trainingMax").GetProperty("value").GetDecimal();

                // For successful Linear progression, TM should have increased
                updatedTM.Should().BeGreaterThan(originalTM, "TM should increase after successful workout");

                // Undo the workout
                await UndoLastWorkoutViaApiAsync(page, workoutId!);

                // Verify TM is reverted
                var workoutAfterUndo = await GetCurrentWorkoutAsync(page);
                var revertedExercise = workoutAfterUndo.Value.GetProperty("exercises").EnumerateArray()
                    .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);
                var revertedTM = revertedExercise.GetProperty("progression").GetProperty("trainingMax").GetProperty("value").GetDecimal();

                revertedTM.Should().Be(originalTM, "TM should be reverted after undo");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task UndoWorkout_WhenNoPreviousWorkout_ReturnsError()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout (no days completed yet)
            await CreateTestWorkoutAsync(page);

            var workoutData = await GetCurrentWorkoutAsync(page);
            var workoutId = workoutData.Value.GetProperty("id").GetString();

            // Try to undo when there's nothing to undo
            var undoResult = await UndoLastWorkoutViaApiAsync(page, workoutId!);

            // Should fail or return an error
            undoResult.Should().BeNull("Undo should fail when no workout has been completed");
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
        // Navigate to workout page
        await page.GotoAsync($"{FrontendUrl}/workout");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check if we need to create a workout
        var noWorkoutMessage = page.Locator("h2:has-text('No Active Workout')").First;
        var hasNoWorkout = await noWorkoutMessage.IsVisibleAsync();

        if (!hasNoWorkout)
        {
            return;
        }

        var createButton = page.Locator("button:has-text('Create Workout Program')").First;
        await createButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await createButton.ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

        // Fill in program details
        var programNameInput = page.Locator("input[type='text']").First;
        await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await programNameInput.FillAsync("Test Undo Workout");

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
    /// Undoes the last completed workout via API.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> UndoLastWorkoutViaApiAsync(IPage page, string workoutId)
    {
        var resultJsonString = await page.EvaluateAsync<string>($@"async () => {{
            const clerk = window.Clerk;
            if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});
            const token = await clerk.session?.getToken();
            if (!token) return JSON.stringify({{ error: 'No auth token' }});
            const response = await fetch('{ApiBaseUrl}/api/v1/workouts/{workoutId}/undo', {{
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
                    ActualReps = isAmrap ? (success ? 19 : 10) : 10,
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
                    ActualReps = success ? maxReps : minReps - 1,
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
                    WeightUnit = 0,
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
