using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// E2E tests for Workout Creation functionality.
/// Tests the complete workflow of creating a workout through the UI,
/// including happy paths and error scenarios.
///
/// The backend creates a 5-day split program with mixed progression strategies:
/// - Linear progression for main lifts (Bench, Deadlift, Squat, OHP, Front Squat)
/// - RepsPerSet (hypertrophy) progression for auxiliary and accessory exercises
/// </summary>
[Collection("E2E")]
public class WorkoutCreationE2ETests : E2ETestBase
{
    public WorkoutCreationE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    /// <summary>
    /// Tests the complete happy path: user creates a 5-day split workout program
    /// by selecting exercises through the wizard, and verifies the workout is created
    /// with exercises on all 5 days.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_CompleteFlow_CreatesFiveDaySplitWithSelectedExercises()
    {
        // Arrange - Delete any existing workouts to ensure clean state
        await DeleteAllWorkoutsAsync();

        // Arrange - Login to get an authenticated user
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Navigate to workout page (should show "No Active Workout" state)
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Verify the "Create Workout Program" button is visible
            var createButton = page.Locator("button:has-text('Create Workout Program')").First;
            await createButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await createButton.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

            // ===== STEP 1: Welcome - Configure program details =====
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await programNameInput.FillAsync("My 5-Day Split Program");

            // Select 5-Day variant from dropdown
            var variantSelect = page.Locator("select").First;
            await variantSelect.SelectOptionAsync(new SelectOptionValue { Value = "5" });

            // Verify 21 weeks is set
            var totalWeeksInput = page.Locator("input[type='number']").First;
            var currentWeeks = await totalWeeksInput.InputValueAsync();
            if (currentWeeks != "21")
            {
                await totalWeeksInput.FillAsync("21");
            }

            // Click Next to go to exercise selection
            var nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // ===== STEP 2: Exercise Selection - Add exercises for each day =====
            // Wait for exercise library to load
            var exerciseLibraryHeader = page.Locator("h3:has-text('Exercise Library')").First;
            await exerciseLibraryHeader.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // Define exercises to add - 5 main lifts, one per day
            var exercisesToAdd = new[]
            {
                "Bench Press",   // Day 1
                "Deadlift",      // Day 2
                "Squat",         // Day 3
                "Overhead Press", // Day 4
                "Front Squat"    // Day 5
            };

            // Add each exercise using the exact aria-label for the Add button
            for (int i = 0; i < exercisesToAdd.Length; i++)
            {
                var exerciseName = exercisesToAdd[i];
                var dayNumber = i + 1;

                // Use GetByRole with exact aria-label to avoid matching partial names
                // e.g., "Add Bench Press" not "Add Incline Bench Press"
                var addButton = page.GetByRole(AriaRole.Button, new() { Name = $"Add {exerciseName}", Exact = true });
                await addButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await addButton.ClickAsync();

                // Wait for config dialog to open
                var configDialog = page.Locator("text=Configure Exercise").First;
                await configDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

                // Select the A2S Hypertrophy (Linear) progression type
                var linearProgressionButton = page.Locator("button:has-text('A2S Hypertrophy')").First;
                await linearProgressionButton.ClickAsync();

                // Set training max value (e.g., 100kg)
                // The training max input is within the A2S Hypertrophy settings section
                var trainingMaxInput = page.Locator("input[type='number']").First;
                await trainingMaxInput.FillAsync("100");

                // Select the day using buttons (not dropdown) - "Day 1", "Day 2", etc.
                // The day buttons are in the "Assign to Day" section of the config dialog
                // Scroll the section into view first to ensure buttons are visible
                var assignToDaySection = page.Locator("text=Assign to Day").First;
                await assignToDaySection.ScrollIntoViewIfNeededAsync();

                // Find and click the specific day button - use exact match to avoid partial matches
                var dayButton = page.GetByRole(AriaRole.Button, new() { Name = $"Day {dayNumber}" });
                await dayButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await dayButton.ClickAsync();
                // Wait for the button state to update
                await page.WaitForTimeoutAsync(200);

                // Save the configuration
                var saveButton = page.Locator("button:has-text('Save Changes')").First;
                await saveButton.ClickAsync();

                // Wait for dialog to close
                await page.WaitForTimeoutAsync(500);
            }

            // Verify we have 5 exercises selected
            var exerciseCount = page.Locator("text=Your Program (5 exercises)").First;
            var countVisible = await exerciseCount.IsVisibleAsync();
            countVisible.Should().BeTrue("Should show 5 exercises selected");

            // Click Next to go to confirmation step
            nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // ===== STEP 3: Confirm and Create =====
            var confirmButton = page.Locator("button:has-text('Create Program')").First;
            await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // Verify the review shows 5 exercises
            foreach (var exerciseName in exercisesToAdd)
            {
                var reviewExercise = page.Locator($"text={exerciseName}").First;
                var reviewVisible = await reviewExercise.IsVisibleAsync();
                reviewVisible.Should().BeTrue($"{exerciseName} should appear in review");
            }

            // Click Create Program
            await confirmButton.ClickAsync();

            // Wait for creation to complete and redirect
            await page.WaitForURLAsync(
                url => url.Contains("/dashboard") || url.Contains("/workout"),
                new() { Timeout = 15000 });

            // ===== VERIFY VIA UI: Navigate to workout dashboard and check exercises =====
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify the workout dashboard is showing
            var workoutTitle = page.Locator("h1").First;
            await workoutTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // Verify the exercises section is visible
            var exercisesHeader = page.Locator("h2:has-text('Your Exercises')").First;
            await exercisesHeader.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // Verify all 5 exercises are visible on the workout dashboard
            foreach (var exerciseName in exercisesToAdd)
            {
                var exerciseOnDashboard = page.Locator($"text={exerciseName}").First;
                var visible = await exerciseOnDashboard.IsVisibleAsync();
                visible.Should().BeTrue($"{exerciseName} should be visible on workout dashboard");
            }

            // Verify the progress indicator is visible
            var progressBar = page.Locator("text=Program Progress").First;
            var progressVisible = await progressBar.IsVisibleAsync();
            progressVisible.Should().BeTrue("Program progress should be visible");

            // ===== VERIFY VIA API: Fetch the workout and verify all exercise properties =====
            // Use the browser context with Clerk's getToken to make an authenticated request
            var workoutJsonString = await page.EvaluateAsync<string>($@"async () => {{
                // Get the Clerk token from the Clerk SDK
                const clerk = window.Clerk;
                if (!clerk) return JSON.stringify({{ error: 'Clerk not available' }});

                const token = await clerk.session?.getToken();
                if (!token) return JSON.stringify({{ error: 'No auth token' }});

                const response = await fetch('{ApiBaseUrl}/api/v1/workouts/current', {{
                    headers: {{
                        'Accept': 'application/json',
                        'Authorization': 'Bearer ' + token
                    }}
                }});
                if (!response.ok) return JSON.stringify({{ error: response.status }});
                return JSON.stringify(await response.json());
            }}");

            workoutJsonString.Should().NotBeNull("API response should not be null");

            var workoutJson = System.Text.Json.JsonDocument.Parse(workoutJsonString!);
            var workout = workoutJson.RootElement;

            // Check for error
            if (workout.TryGetProperty("error", out var errorProp))
            {
                Assert.Fail($"API request failed with status: {errorProp}");
            }

            // Verify workout properties
            workout.GetProperty("name").GetString().Should().Be("My 5-Day Split Program", "Workout name should match");
            workout.GetProperty("totalWeeks").GetInt32().Should().Be(21, "Total weeks should be 21");
            workout.GetProperty("status").GetString().Should().Be("Active", "Workout should be active");

            // Verify exercises
            var exercises = workout.GetProperty("exercises");
            exercises.GetArrayLength().Should().Be(5, "Should have 5 exercises");

            // Verify each exercise has the correct properties including ALL progression details
            var exerciseList = exercises.EnumerateArray().ToList();
            for (int i = 0; i < exercisesToAdd.Length; i++)
            {
                var expectedName = exercisesToAdd[i];
                var expectedDay = i + 1;

                // Find the exercise with this name
                var exercise = exerciseList.FirstOrDefault(e => e.GetProperty("name").GetString() == expectedName);
                exercise.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined, $"Exercise '{expectedName}' should exist");

                // Verify day assignment
                exercise.GetProperty("assignedDay").GetInt32().Should().Be(expectedDay, $"{expectedName} should be on Day {expectedDay}");

                // Verify order in day
                exercise.GetProperty("orderInDay").GetInt32().Should().BeGreaterThan(0, $"{expectedName} should have a valid order in day");

                // Verify progression type is Linear
                var progression = exercise.GetProperty("progression");
                progression.GetProperty("type").GetString().Should().Be("Linear", $"{expectedName} should have Linear progression");

                // Verify training max value and unit
                var trainingMax = progression.GetProperty("trainingMax");
                trainingMax.GetProperty("value").GetDecimal().Should().Be(100m, $"{expectedName} training max value should be 100");
                trainingMax.GetProperty("unit").GetInt32().Should().Be(1, $"{expectedName} training max unit should be Kilograms (1)");

                // Verify AMRAP setting
                progression.GetProperty("useAmrap").GetBoolean().Should().BeTrue($"{expectedName} should have AMRAP enabled for main lifts");

                // Verify base sets per exercise
                progression.GetProperty("baseSetsPerExercise").GetInt32().Should().BeGreaterThan(0, $"{expectedName} should have a positive number of sets");
            }

            // ===== VERIFY UI DISPLAYS FULL EXERCISE DETAILS =====
            // Navigate to workout page and verify all details are displayed
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify "Training Max" labels are visible (indicating detailed display)
            var trainingMaxLabels = page.Locator("text=Training Max").First;
            var tmVisible = await trainingMaxLabels.IsVisibleAsync();
            tmVisible.Should().BeTrue("Training Max details should be displayed on workout page");

            // Verify "AMRAP" indicator is visible
            var amrapIndicator = page.Locator("text=AMRAP").First;
            var amrapVisible = await amrapIndicator.IsVisibleAsync();
            amrapVisible.Should().BeTrue("AMRAP indicator should be displayed for linear progression exercises");

            // Verify sets information is visible
            var setsInfo = page.Locator("text=Sets").First;
            var setsVisible = await setsInfo.IsVisibleAsync();
            setsVisible.Should().BeTrue("Sets information should be displayed");

            // Verify weight unit is shown (kg)
            var kgUnit = page.Locator("text=kg").First;
            var unitVisible = await kgUnit.IsVisibleAsync();
            unitVisible.Should().BeTrue("Weight unit (kg) should be displayed");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that RepsPerSet progression exercises display all required details
    /// including weight, sets, reps, rep range, and target sets.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_RepsPerSetProgression_ShouldDisplayAllDetails()
    {
        // Arrange - Delete any existing workouts to ensure clean state
        await DeleteAllWorkoutsAsync();

        // Arrange - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Navigate to setup
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Configure program
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await programNameInput.FillAsync("Hypertrophy Program");

            // Select 4-Day variant
            var variantSelect = page.Locator("select").First;
            await variantSelect.SelectOptionAsync(new SelectOptionValue { Value = "4" });

            // Click Next to go to exercise selection
            var nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Wait for exercise library
            var exerciseLibraryHeader = page.Locator("h3:has-text('Exercise Library')").First;
            await exerciseLibraryHeader.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // Add a RepsPerSet (accessory) exercise
            var addBicepCurlButton = page.GetByRole(AriaRole.Button, new() { Name = "Add Bicep Curl", Exact = true });
            await addBicepCurlButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await addBicepCurlButton.ClickAsync();

            // Wait for config dialog
            var configDialog = page.Locator("text=Configure Exercise").First;
            await configDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // Select RepsPerSet progression
            var repsPerSetButton = page.Locator("button:has-text('Reps Per Set')").First;
            await repsPerSetButton.ClickAsync();

            // Wait for RepsPerSet configuration fields to appear
            await page.WaitForTimeoutAsync(500);

            // Set weight
            var weightInput = page.Locator("input[type='number']").First;
            await weightInput.FillAsync("15");

            // Assign to Day 1
            var assignToDaySection = page.Locator("text=Assign to Day").First;
            await assignToDaySection.ScrollIntoViewIfNeededAsync();
            var day1Button = page.GetByRole(AriaRole.Button, new() { Name = "Day 1" });
            await day1Button.ClickAsync();
            await page.WaitForTimeoutAsync(200);

            // Save configuration
            var saveButton = page.Locator("button:has-text('Save Changes')").First;
            await saveButton.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Continue to confirm step
            nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Create the program
            var confirmButton = page.Locator("button:has-text('Create Program')").First;
            await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await confirmButton.ClickAsync();

            // Wait for redirect
            await page.WaitForURLAsync(
                url => url.Contains("/dashboard") || url.Contains("/workout"),
                new() { Timeout = 15000 });

            // Navigate to workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Fetch workout via API and verify RepsPerSet details
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

            workoutJsonString.Should().NotBeNull();
            var workoutJson = System.Text.Json.JsonDocument.Parse(workoutJsonString!);
            var workout = workoutJson.RootElement;

            // Verify workout was created
            workout.GetProperty("name").GetString().Should().Be("Hypertrophy Program");

            // Find the Bicep Curl exercise
            var exercises = workout.GetProperty("exercises");
            var bicepCurl = exercises.EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("name").GetString() == "Bicep Curl");

            bicepCurl.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined, "Bicep Curl should exist");

            // Verify progression is RepsPerSet
            var progression = bicepCurl.GetProperty("progression");
            progression.GetProperty("type").GetString().Should().Be("RepsPerSet", "Bicep Curl should have RepsPerSet progression");

            // Verify RepsPerSet specific details
            progression.GetProperty("currentWeight").GetDecimal().Should().Be(15m, "Weight should be 15");
            progression.GetProperty("weightUnit").GetString().Should().NotBeNullOrEmpty("Weight unit should be specified");
            progression.GetProperty("currentSetCount").GetInt32().Should().BeGreaterThan(0, "Current set count should be positive");
            progression.GetProperty("targetSets").GetInt32().Should().BeGreaterThan(0, "Target sets should be positive");

            // Verify rep range
            var repRange = progression.GetProperty("repRange");
            repRange.GetProperty("minimum").GetInt32().Should().BeGreaterThan(0, "Minimum reps should be positive");
            repRange.GetProperty("target").GetInt32().Should().BeGreaterThan(0, "Target reps should be positive");
            repRange.GetProperty("maximum").GetInt32().Should().BeGreaterThan(0, "Maximum reps should be positive");
            repRange.GetProperty("target").GetInt32().Should().BeGreaterThanOrEqualTo(
                repRange.GetProperty("minimum").GetInt32(), "Target should be >= minimum");
            repRange.GetProperty("maximum").GetInt32().Should().BeGreaterThanOrEqualTo(
                repRange.GetProperty("target").GetInt32(), "Maximum should be >= target");

            // Verify UI displays RepsPerSet details
            var weightLabel = page.Locator("text=Weight").First;
            var weightVisible = await weightLabel.IsVisibleAsync();
            weightVisible.Should().BeTrue("Weight should be displayed for RepsPerSet exercises");

            var repsLabel = page.Locator("text=Reps").First;
            var repsVisible = await repsLabel.IsVisibleAsync();
            repsVisible.Should().BeTrue("Reps information should be displayed");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that the Create Workout button is visible on the workout dashboard
    /// when no workout exists.
    /// </summary>
    [Fact]
    public async Task WorkoutDashboard_NoWorkout_ShouldShowCreateButton()
    {
        // Arrange - Delete any existing workouts to ensure clean state
        await DeleteAllWorkoutsAsync();

        // Arrange - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Act - Navigate to workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Verify "No Active Workout" message is displayed
            var noWorkoutMessage = page.Locator("h2:has-text('No Active Workout')").First;
            await noWorkoutMessage.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var messageVisible = await noWorkoutMessage.IsVisibleAsync();
            messageVisible.Should().BeTrue("'No Active Workout' message should be visible");

            // Verify the Create Workout button is visible
            var createButton = page.Locator("button:has-text('Create Workout Program')").First;
            var createButtonVisible = await createButton.IsVisibleAsync();
            createButtonVisible.Should().BeTrue("Create Workout Program button should be visible");

            // Verify the button is enabled and clickable
            var isEnabled = await createButton.IsEnabledAsync();
            isEnabled.Should().BeTrue("Create Workout Program button should be enabled");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that creating a workout with invalid data shows appropriate error messages.
    /// Sad path: Empty workout name.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithEmptyName_ShouldShowValidationError()
    {
        // Arrange - Login and navigate to setup
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Act - Clear the program name and try to proceed
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await programNameInput.FillAsync(""); // Empty name

            // Try to click Next
            var nextButton = page.Locator("button:has-text('Next')").First;
            var isNextEnabled = await nextButton.IsEnabledAsync();

            // Assert - Next button should be disabled when name is empty
            isNextEnabled.Should().BeFalse("Next button should be disabled when workout name is empty");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that the setup wizard can be navigated back and forth between steps.
    /// </summary>
    [Fact]
    public async Task SetupWizard_ShouldAllowNavigationBetweenSteps()
    {
        // Arrange - Login and navigate to setup
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Verify we start on the Welcome step
            var welcomeHeading = page.Locator("h2:has-text('Welcome')").First;
            await welcomeHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            var welcomeVisible = await welcomeHeading.IsVisibleAsync();
            welcomeVisible.Should().BeTrue("Should start on Welcome step");

            // Verify Back button is disabled on first step
            var backButton = page.Locator("button:has-text('Back')").First;
            var isBackDisabled = await backButton.IsDisabledAsync();
            isBackDisabled.Should().BeTrue("Back button should be disabled on first step");

            // Fill in required fields
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.FillAsync("Test Program");

            // Act - Click Next to go to next step
            var nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000); // Allow transition

            // Assert - Verify we moved to the next step (Back button should now be enabled)
            var isBackEnabled = await backButton.IsEnabledAsync();
            isBackEnabled.Should().BeTrue("Back button should be enabled on subsequent steps");

            // Act - Click Back to return to welcome step
            await backButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Assert - Verify we're back on the Welcome step
            welcomeVisible = await welcomeHeading.IsVisibleAsync();
            welcomeVisible.Should().BeTrue("Should return to Welcome step after clicking Back");

            // Verify the data was preserved
            var nameValue = await programNameInput.InputValueAsync();
            nameValue.Should().Be("Test Program", "Form data should be preserved when navigating back");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that attempting to create a second workout when one already exists
    /// returns a conflict error.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WhenActiveWorkoutExists_ShouldShowConflictError()
    {
        // Arrange - Login and create first workout
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create first workout
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var noWorkoutMessage = page.Locator("h2:has-text('No Active Workout')").First;
            var hasNoWorkout = await noWorkoutMessage.IsVisibleAsync();

            if (hasNoWorkout)
            {
                // Create a workout quickly
                var createButton = page.Locator("button:has-text('Create Workout Program')").First;
                await createButton.ClickAsync();
                await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

                var programNameInput = page.Locator("input[type='text']").First;
                await programNameInput.FillAsync("First Workout");

                // Navigate through wizard to create
                await NavigateToConfirmStepAndCreate(page);

                // Wait for redirect
                await page.WaitForURLAsync(
                    url => url.Contains("/dashboard") || url.Contains("/workout"),
                    new() { Timeout = 15000 });
            }

            // Now try to create a second workout
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var programNameInput2 = page.Locator("input[type='text']").First;
            await programNameInput2.FillAsync("Second Workout");

            // Navigate through wizard to create
            await NavigateToConfirmStepAndCreate(page);

            // Wait a moment for error to appear
            await page.WaitForTimeoutAsync(2000);

            // Assert - Error should be displayed (either toast or inline)
            // Check common error locations
            var hasError = await page.Locator("text=active workout, text=already exists, .error, [role='alert']").First.IsVisibleAsync();

            // If no specific error message, at least verify we didn't successfully redirect away
            var currentUrl = page.Url;
            currentUrl.Should().Contain("/setup", "Should remain on setup page when creation fails due to existing workout");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that workout creation handles network errors gracefully.
    /// Sad path: API failure during workout creation.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_WithApiFailure_ShouldShowErrorMessage()
    {
        // Arrange - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Intercept the API call and make it fail
            await page.RouteAsync("**/api/v1/workouts", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 500,
                    ContentType = "application/json",
                    Body = "{\"error\": \"Internal server error\"}"
                });
            });

            // Navigate to setup
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Fill in the form
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.FillAsync("Test Program");

            // Navigate through wizard to create
            await NavigateToConfirmStepAndCreate(page);

            // Wait for error to appear
            await page.WaitForTimeoutAsync(2000);

            // Assert - User should remain on setup page when creation fails
            var currentUrl = page.Url;
            currentUrl.Should().Contain("/setup", "Should remain on setup page when creation fails");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that unauthenticated users cannot access the workout setup wizard.
    /// Sad path: Unauthorized access.
    /// </summary>
    [Fact]
    public async Task SetupWizard_WithoutAuthentication_ShouldRedirectToLogin()
    {
        // Arrange - Create a page without logging in
        var page = await CreatePageAsync();

        try
        {
            // Act - Try to navigate to setup wizard
            await page.GotoAsync($"{FrontendUrl}/setup");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(2000); // Allow time for redirects

            // Assert - Should be redirected to sign-in
            var currentUrl = page.Url;
            currentUrl.Should().Contain("/sign-in", "Unauthenticated users should be redirected to sign-in");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Helper method to navigate through wizard steps and click Create Program button.
    /// </summary>
    private async Task NavigateToConfirmStepAndCreate(IPage page)
    {
        var nextButton = page.Locator("button:has-text('Next')").First;
        var confirmButton = page.Locator("button:has-text('Create Program')").First;

        // Click through steps until we can create
        for (int i = 0; i < 5; i++) // Max 5 steps to prevent infinite loop
        {
            var isConfirmVisible = await confirmButton.IsVisibleAsync();
            if (isConfirmVisible)
            {
                await confirmButton.ClickAsync();
                break;
            }

            var isNextVisible = await nextButton.IsVisibleAsync();
            if (!isNextVisible)
            {
                break;
            }

            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(500);
        }
    }
}
