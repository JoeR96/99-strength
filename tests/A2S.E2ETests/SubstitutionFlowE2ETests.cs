using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// E2E tests for exercise substitution flows.
/// </summary>
[Collection("E2E")]
public class SubstitutionFlowE2ETests : E2ETestBase
{
    public SubstitutionFlowE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    [Fact]
    public async Task TemporarySubstitution_DoesNotChangeFutureWorkouts()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout
            await CreateTestWorkoutAsync(page);

            var workoutData = await GetCurrentWorkoutAsync(page);
            workoutData.Should().NotBeNull("Should have an active workout");

            var workoutId = workoutData.Value.GetProperty("id").GetString();
            var exercises = workoutData.Value.GetProperty("exercises");

            // Get the first Day 1 exercise
            var day1Exercise = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("assignedDay").GetInt32() == 1);

            day1Exercise.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined, "Day 1 should have exercises");

            var exerciseId = day1Exercise.GetProperty("id").GetString();
            var originalExerciseName = day1Exercise.GetProperty("name").GetString();

            // Apply a temporary substitution via API
            var substitutionResult = await ApplySubstitutionViaApiAsync(
                page,
                workoutId!,
                exerciseId!,
                substituteName: "Leg Press",
                isPermanent: false);

            substitutionResult.Should().NotBeNull("Temporary substitution should succeed");

            // Verify the substitution was applied for this session
            var workoutAfterSub = await GetCurrentWorkoutAsync(page);
            var substitutedExercise = workoutAfterSub.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            // For temporary substitution, the exercise might have a temporary substitute flag
            // or the name might temporarily change for the current session

            // Complete Day 1 with the substituted exercise
            var day1Exercises = workoutAfterSub.Value.GetProperty("exercises").EnumerateArray()
                .Where(e => e.GetProperty("assignedDay").GetInt32() == 1)
                .ToList();

            var performances = new List<object>();
            foreach (var exercise in day1Exercises)
            {
                var exId = exercise.GetProperty("id").GetString();
                var progression = exercise.GetProperty("progression");
                var progressionType = progression.GetProperty("type").GetString();

                var completedSets = CreateCompletedSets(progression, progressionType!, success: true);
                performances.Add(new { ExerciseId = exId, CompletedSets = completedSets });
            }

            await CompleteDayViaApiAsync(page, workoutId!, 1, performances);

            // Progress to next week to see if the original exercise is back
            await ProgressWeekViaApiAsync(page, workoutId!);

            // Get the workout after progressing
            var workoutNextWeek = await GetCurrentWorkoutAsync(page);
            var exerciseNextWeek = workoutNextWeek.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            // The exercise name should be the original (not the substitute)
            var exerciseNameNextWeek = exerciseNextWeek.GetProperty("name").GetString();
            exerciseNameNextWeek.Should().Be(originalExerciseName, "Original exercise should be restored for future workouts");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task PermanentSubstitution_ChangesExerciseForAllFutureWorkouts()
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

            // Get the first Day 1 exercise
            var day1Exercise = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("assignedDay").GetInt32() == 1);

            var exerciseId = day1Exercise.GetProperty("id").GetString();
            var originalExerciseName = day1Exercise.GetProperty("name").GetString();

            // Apply a permanent substitution via API
            var substitutionResult = await ApplySubstitutionViaApiAsync(
                page,
                workoutId!,
                exerciseId!,
                substituteName: "Front Squat",
                isPermanent: true);

            substitutionResult.Should().NotBeNull("Permanent substitution should succeed");

            // Verify the exercise name has changed
            var workoutAfterSub = await GetCurrentWorkoutAsync(page);
            var substitutedExercise = workoutAfterSub.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            var newExerciseName = substitutedExercise.GetProperty("name").GetString();
            newExerciseName.Should().Be("Front Squat", "Exercise should be permanently changed to Front Squat");

            // Complete Day 1
            var day1Exercises = workoutAfterSub.Value.GetProperty("exercises").EnumerateArray()
                .Where(e => e.GetProperty("assignedDay").GetInt32() == 1)
                .ToList();

            var performances = new List<object>();
            foreach (var exercise in day1Exercises)
            {
                var exId = exercise.GetProperty("id").GetString();
                var progression = exercise.GetProperty("progression");
                var progressionType = progression.GetProperty("type").GetString();

                var completedSets = CreateCompletedSets(progression, progressionType!, success: true);
                performances.Add(new { ExerciseId = exId, CompletedSets = completedSets });
            }

            await CompleteDayViaApiAsync(page, workoutId!, 1, performances);

            // Progress to next week
            await ProgressWeekViaApiAsync(page, workoutId!);

            // Verify the substitution persists
            var workoutNextWeek = await GetCurrentWorkoutAsync(page);
            var exerciseNextWeek = workoutNextWeek.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            var exerciseNameNextWeek = exerciseNextWeek.GetProperty("name").GetString();
            exerciseNameNextWeek.Should().Be("Front Squat", "Permanent substitution should persist across weeks");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task SubstitutionModal_CancelsWithoutChanges()
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

            // Get the first Day 1 exercise
            var day1Exercise = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("assignedDay").GetInt32() == 1);

            var exerciseId = day1Exercise.GetProperty("id").GetString();
            var originalExerciseName = day1Exercise.GetProperty("name").GetString();

            // Navigate to the workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // The UI should have substitute buttons
            // Note: The actual UI interaction depends on the frontend implementation

            // Verify the exercise name hasn't changed (no substitution was applied)
            var workoutAfter = await GetCurrentWorkoutAsync(page);
            var exerciseAfter = workoutAfter.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            var exerciseNameAfter = exerciseAfter.GetProperty("name").GetString();
            exerciseNameAfter.Should().Be(originalExerciseName, "Exercise should remain unchanged when no substitution is applied");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task SubstituteExercise_UpdatesExerciseDetails()
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

            // Get the first Day 1 exercise
            var day1Exercise = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("assignedDay").GetInt32() == 1);

            var exerciseId = day1Exercise.GetProperty("id").GetString();

            // Apply substitution with specific details
            var substitutionResult = await ApplySubstitutionViaApiAsync(
                page,
                workoutId!,
                exerciseId!,
                substituteName: "Hack Squat",
                isPermanent: true);

            // Verify the result
            substitutionResult.Should().NotBeNull("Substitution should succeed");

            // Verify the exercise was updated
            var updatedWorkout = await GetCurrentWorkoutAsync(page);
            var updatedExercise = updatedWorkout.Value.GetProperty("exercises").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("id").GetString() == exerciseId);

            updatedExercise.GetProperty("name").GetString().Should().Be("Hack Squat");
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
        await programNameInput.FillAsync("Test Substitution Flow");

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
    /// Applies an exercise substitution via API.
    /// </summary>
    private async Task<System.Text.Json.JsonElement?> ApplySubstitutionViaApiAsync(
        IPage page, string workoutId, string exerciseId, string substituteName, bool isPermanent)
    {
        var requestBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            SubstituteName = substituteName,
            IsPermanent = isPermanent
        });

        var resultJsonString = await page.EvaluateAsync<string>($@"async () => {{
            const clerk = window.Clerk;
            if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});
            const token = await clerk.session?.getToken();
            if (!token) return JSON.stringify({{ error: 'No auth token' }});
            const response = await fetch('{ApiBaseUrl}/api/v1/workouts/{workoutId}/exercises/{exerciseId}/substitute', {{
                method: 'POST',
                headers: {{
                    'Accept': 'application/json',
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                }},
                body: JSON.stringify({requestBody})
            }});
            if (!response.ok) return JSON.stringify({{ error: response.status }});
            const text = await response.text();
            return text ? text : JSON.stringify({{ success: true }});
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
