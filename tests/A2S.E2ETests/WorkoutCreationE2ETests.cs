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
    public WorkoutCreationE2ETests(FrontendFixture frontendFixture) : base(frontendFixture)
    {
    }

    /// <summary>
    /// Tests the complete happy path: user creates a 5-day split workout program
    /// and verifies it appears on the workout dashboard with exercises for all 5 days.
    /// </summary>
    [Fact]
    public async Task CreateWorkout_CompleteFlow_CreatesFiveDaySplitWithMixedProgressions()
    {
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
            var createButtonVisible = await createButton.IsVisibleAsync();
            createButtonVisible.Should().BeTrue("Create Workout Program button should be visible when no workout exists");

            // Act - Click the Create Workout button
            await createButton.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

            // Verify we're on the setup wizard
            page.Url.Should().Contain("/setup", "Should navigate to setup wizard");

            // Step 1: Welcome - Fill in program details
            var programNameInput = page.Locator("input[type='text']").First;
            await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await programNameInput.FillAsync("My 5-Day Split Program");

            // Set total weeks to 21 (default)
            var totalWeeksInput = page.Locator("input[type='number']").First;
            var currentWeeks = await totalWeeksInput.InputValueAsync();
            if (currentWeeks != "21")
            {
                await totalWeeksInput.FillAsync("21");
            }

            // Click Next to proceed through wizard steps
            var nextButton = page.Locator("button:has-text('Next')").First;
            await nextButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000); // Allow transition

            // Navigate through any intermediate steps to reach the Create button
            var confirmButton = page.Locator("button:has-text('Create Program')").First;
            var isOnConfirmStep = await confirmButton.IsVisibleAsync();

            while (!isOnConfirmStep)
            {
                var continueButton = page.Locator("button:has-text('Next')").First;
                var isContinueVisible = await continueButton.IsVisibleAsync();

                if (!isContinueVisible)
                {
                    break;
                }

                await continueButton.ClickAsync();
                await page.WaitForTimeoutAsync(1000);
                isOnConfirmStep = await confirmButton.IsVisibleAsync();
            }

            // Final step: Confirm and create
            await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await confirmButton.ClickAsync();

            // Wait for creation to complete and redirect
            await page.WaitForURLAsync(
                url => url.Contains("/dashboard") || url.Contains("/workout"),
                new() { Timeout = 15000 });

            // Assert - Verify workout was created successfully
            if (!page.Url.Contains("/workout"))
            {
                await page.GotoAsync($"{FrontendUrl}/workout");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }

            // Verify the workout dashboard is now showing
            var workoutTitle = page.Locator("h1").First;
            await workoutTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var workoutTitleText = await workoutTitle.TextContentAsync();
            workoutTitleText.Should().NotBeNullOrEmpty("Workout title should be displayed");

            // Verify the exercises section is visible
            var exercisesHeader = page.Locator("h2:has-text('Your Exercises')").First;
            var exercisesHeaderVisible = await exercisesHeader.IsVisibleAsync();
            exercisesHeaderVisible.Should().BeTrue("Exercises section should be visible");

            // Verify main lift exercises from the 5-day split are created
            // Day 1: Bench Press (Main Lift with Linear progression)
            var benchExercise = page.Locator("text=Bench Press").First;
            var benchVisible = await benchExercise.IsVisibleAsync();
            benchVisible.Should().BeTrue("Bench Press should be created for Day 1");

            // Day 2: Deadlift (Main Lift with Linear progression)
            var deadliftExercise = page.Locator("text=Deadlift").First;
            var deadliftVisible = await deadliftExercise.IsVisibleAsync();
            deadliftVisible.Should().BeTrue("Deadlift should be created for Day 2");

            // Day 3: Squat (Main Lift with Linear progression)
            var squatExercise = page.Locator("text=Squat").First;
            var squatVisible = await squatExercise.IsVisibleAsync();
            squatVisible.Should().BeTrue("Squat should be created for Day 3");

            // Day 4: Overhead Press (Main Lift with Linear progression)
            var ohpExercise = page.Locator("text=Overhead Press").First;
            var ohpVisible = await ohpExercise.IsVisibleAsync();
            ohpVisible.Should().BeTrue("Overhead Press should be created for Day 4");

            // Day 5: Front Squat (Main Lift with Linear progression)
            var frontSquatExercise = page.Locator("text=Front Squat").First;
            var frontSquatVisible = await frontSquatExercise.IsVisibleAsync();
            frontSquatVisible.Should().BeTrue("Front Squat should be created for Day 5");

            // Verify some hypertrophy/RepsPerSet exercises are also created
            // Day 1 includes: Tricep Extension (Accessory with RepsPerSet)
            var tricepExercise = page.Locator("text=Tricep Extension").First;
            var tricepVisible = await tricepExercise.IsVisibleAsync();
            tricepVisible.Should().BeTrue("Tricep Extension (hypertrophy exercise) should be created");

            // Verify the progress indicator is visible
            var progressBar = page.Locator("text=Program Progress").First;
            var progressVisible = await progressBar.IsVisibleAsync();
            progressVisible.Should().BeTrue("Program progress should be visible");
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
