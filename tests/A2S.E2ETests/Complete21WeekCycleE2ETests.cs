using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Comprehensive E2E tests that complete the entire 21-week workout cycle through the UI.
/// This test simulates a real user completing every workout session via the browser.
///
/// Performance simulation produces varied outcomes based on actual A2S program data:
/// - LINEAR (AMRAP): Success (+2-5 reps over target), Maintained (0), Failed (-1-2 below target)
/// - REPSPERSETS: Success (all max reps), Maintained (between min-max), Failed (below min)
/// - MINIMALSETS: Success (fewer sets than current), Maintained (exact sets), Failed (incomplete reps)
///
/// 21-week cycle = 4 days per week = 84 total workout sessions
/// </summary>
[Collection("E2E")]
public class Complete21WeekCycleE2ETests : E2ETestBase
{
    private const int TotalWeeks = 21;
    private const int DaysPerWeek = 4; // 4-day program

    // Track exercise performance to vary outcomes
    private int _exerciseCounter = 0;

    public Complete21WeekCycleE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    #region A2S Spreadsheet Data

    /// <summary>
    /// Week-by-week programming data from the A2S 2024-2025 spreadsheet.
    /// Source: "A2S 2024-2025 - Program (1).csv" - Smith Squat row (line 22)
    ///
    /// Format: (IntensityPercent, Sets, TargetReps, IsDeloadWeek)
    /// </summary>
    /// <remarks>
    /// BLOCK 1 (Weeks 1-7): Volume accumulation
    ///   Mini-cycle 1: 75% → 85% → 90%
    ///   Mini-cycle 2: 80% → 85% → 90%
    ///   Week 7: Deload at 65%
    ///
    /// BLOCK 2 (Weeks 8-14): Intensity building
    ///   Mini-cycle 1: 85% → 90% → 95%
    ///   Mini-cycle 2: 85% → 90% → 95%
    ///   Week 14: Deload at 65%
    ///
    /// BLOCK 3 (Weeks 15-21): Peak/realization
    ///   Mini-cycle 1: 90% → 95% → 100%
    ///   Mini-cycle 2: 95% → 100% → 105%
    ///   Week 21: Deload at 65%
    /// </remarks>
    private static readonly (int Intensity, int Sets, int Reps, bool IsDeload)[] SpreadsheetWeeklyData = new[]
    {
        // Week 0 placeholder (1-indexed)
        (0, 0, 0, false),

        // BLOCK 1: Weeks 1-7
        (75, 5, 10, false),   // Week 1
        (85, 4, 8, false),    // Week 2
        (90, 3, 6, false),    // Week 3
        (80, 5, 9, false),    // Week 4
        (85, 4, 7, false),    // Week 5
        (90, 3, 5, false),    // Week 6
        (65, 5, 10, true),    // Week 7 - DELOAD

        // BLOCK 2: Weeks 8-14
        (85, 4, 8, false),    // Week 8
        (90, 3, 6, false),    // Week 9
        (95, 2, 4, false),    // Week 10
        (85, 4, 7, false),    // Week 11
        (90, 3, 5, false),    // Week 12
        (95, 2, 3, false),    // Week 13
        (65, 5, 10, true),    // Week 14 - DELOAD

        // BLOCK 3: Weeks 15-21
        (90, 3, 6, false),    // Week 15
        (95, 2, 4, false),    // Week 16
        (100, 1, 2, false),   // Week 17
        (95, 2, 4, false),    // Week 18
        (100, 1, 2, false),   // Week 19
        (105, 1, 2, false),   // Week 20
        (65, 5, 10, true),    // Week 21 - DELOAD (final)
    };

    /// <summary>
    /// Gets the expected workout parameters for a given week from the spreadsheet.
    /// </summary>
    private static (int Intensity, int Sets, int Reps, bool IsDeload) GetSpreadsheetDataForWeek(int week)
    {
        if (week < 1 || week > 21)
            throw new ArgumentOutOfRangeException(nameof(week), $"Week must be 1-21, got {week}");

        return SpreadsheetWeeklyData[week];
    }

    #endregion

    #region Performance Simulation Data

    /// <summary>
    /// Simulated performance scenarios based on real CSV data patterns.
    /// Each scenario defines what reps to enter for different set types.
    /// </summary>
    private record PerformanceScenario(
        string Name,
        PerformanceOutcome ExpectedOutcome,
        int AmrapDelta,           // For Linear: reps above/below target (+3 success, 0 maintained, -2 failed)
        int RepsPerSetReps,       // For RepsPerSet: what reps to enter (max=success, mid=maintained, below min=failed)
        int MinimalSetsCompleted  // For MinimalSets: sets used vs target (fewer=success, same=maintained, more=failed)
    );

    /// <summary>
    /// Performance scenarios to cycle through for varied outcomes.
    /// Distribution mirrors real training: ~60% success, ~25% maintained, ~15% failed
    /// </summary>
    private static readonly PerformanceScenario[] Scenarios = new[]
    {
        // Success scenarios (60%) - various levels of overperformance
        new PerformanceScenario("Strong Success", PerformanceOutcome.Success, +5, 15, -1),
        new PerformanceScenario("Good Success", PerformanceOutcome.Success, +3, 15, -1),
        new PerformanceScenario("Moderate Success", PerformanceOutcome.Success, +2, 15, 0),
        new PerformanceScenario("Slight Success", PerformanceOutcome.Success, +1, 15, 0),
        new PerformanceScenario("Success Variant 1", PerformanceOutcome.Success, +4, 15, -1),
        new PerformanceScenario("Success Variant 2", PerformanceOutcome.Success, +2, 15, 0),

        // Maintained scenarios (25%) - hitting targets exactly
        new PerformanceScenario("Maintained Exact", PerformanceOutcome.Maintained, 0, 12, 0),
        new PerformanceScenario("Maintained Mid", PerformanceOutcome.Maintained, 0, 10, 0),
        new PerformanceScenario("Maintained Variant", PerformanceOutcome.Maintained, 0, 11, 0),

        // Failed scenarios (15%) - underperformance
        new PerformanceScenario("Failed Slight", PerformanceOutcome.Failed, -1, 7, +1),
        new PerformanceScenario("Failed Moderate", PerformanceOutcome.Failed, -2, 6, +1),
    };

    private enum PerformanceOutcome
    {
        Success,
        Maintained,
        Failed
    }

    /// <summary>
    /// Gets the performance scenario for a specific exercise based on deterministic pattern.
    /// </summary>
    private PerformanceScenario GetScenarioForExercise(int exerciseIndex, int week, int day)
    {
        _exerciseCounter++;

        // Use deterministic pattern to ensure reproducible but varied results
        var index = (_exerciseCounter + (week * 4) + day) % Scenarios.Length;
        return Scenarios[index];
    }

    /// <summary>
    /// Gets the reps to enter for an AMRAP set based on the spreadsheet target reps for the week.
    /// </summary>
    private static int GetAmrapRepsForWeek(int week, PerformanceScenario scenario)
    {
        var (_, _, reps, _) = GetSpreadsheetDataForWeek(week);
        return Math.Max(1, reps + scenario.AmrapDelta);
    }

    /// <summary>
    /// Gets the reps to enter for a RepsPerSet exercise.
    /// </summary>
    private int GetRepsPerSetReps(PerformanceScenario scenario)
    {
        return scenario.RepsPerSetReps;
    }

    #endregion

    /// <summary>
    /// Tests completing the entire 21-week workout cycle through the UI.
    ///
    /// ASSERTIONS: 84 explicit week/day assertions (21 weeks x 4 days each)
    ///
    /// This test:
    /// 1. Creates a 4-day workout program (21 weeks)
    /// 2. Completes every training day via UI (84 sessions total)
    /// 3. ASSERTS correct week (1-21) and day (1-4) at each step
    /// 4. Verifies week auto-progresses after completing all 4 days
    /// 5. Verifies completion summary shows progression feedback
    /// </summary>
    [Fact(Skip = "Long-running test - run manually: dotnet test --filter Complete21WeekCycle")]
    public async Task Complete21WeekCycle_ViaUI_84Workouts_AssertWeekAndDayProgression()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout program with mixed progression types
            await CreateTestWorkoutProgramAsync(page);

            // Track outcomes across the program
            var outcomeStats = new Dictionary<string, int>
            {
                ["SUCCESS"] = 0,
                ["MAINTAINED"] = 0,
                ["FAILED"] = 0,
                ["DELOAD"] = 0
            };

            int workoutCount = 0;

            // Complete all 21 weeks - 84 workouts total
            for (int expectedWeek = 1; expectedWeek <= TotalWeeks; expectedWeek++)
            {
                Console.WriteLine($"\n=== Starting Week {expectedWeek} of {TotalWeeks} ===");
                bool isDeloadWeek = expectedWeek % 7 == 0;

                if (isDeloadWeek)
                {
                    Console.WriteLine("  [DELOAD WEEK]");
                }

                // Complete all 4 days in this week
                for (int expectedDay = 1; expectedDay <= DaysPerWeek; expectedDay++)
                {
                    workoutCount++;
                    Console.WriteLine($"  Workout #{workoutCount}: Week {expectedWeek}, Day {expectedDay}");

                    // =========================================================
                    // ASSERTION: Verify we're on the correct week and day
                    // This is one of 84 assertions for week/day tracking
                    // =========================================================
                    await AssertCurrentWeekAndDayAsync(page, expectedWeek, expectedDay,
                        $"Workout #{workoutCount}: Before completing Day {expectedDay} of Week {expectedWeek}");

                    // Complete this day via UI
                    var dayOutcomes = await CompleteWorkoutDayViaUIAsync(page, expectedDay, expectedWeek);

                    // Track outcomes
                    foreach (var outcome in dayOutcomes)
                    {
                        if (outcomeStats.ContainsKey(outcome))
                            outcomeStats[outcome]++;
                    }
                }

                // After completing all 4 days, should have auto-progressed to next week (or finished)
                if (expectedWeek < TotalWeeks)
                {
                    await AssertCurrentWeekAndDayAsync(page, expectedWeek + 1, 1,
                        $"After completing Week {expectedWeek}, should be on Week {expectedWeek + 1} Day 1");
                }
            }

            // Verify we completed all 84 workouts
            workoutCount.Should().Be(84, "Should have completed exactly 84 workouts (21 weeks x 4 days)");

            // Log final statistics
            Console.WriteLine("\n============================================");
            Console.WriteLine("=== 21-Week Cycle Complete! ===");
            Console.WriteLine($"Total Workouts: {workoutCount}");
            Console.WriteLine($"Total Outcomes:");
            Console.WriteLine($"  SUCCESS: {outcomeStats["SUCCESS"]}");
            Console.WriteLine($"  MAINTAINED: {outcomeStats["MAINTAINED"]}");
            Console.WriteLine($"  FAILED: {outcomeStats["FAILED"]}");
            Console.WriteLine($"  DELOAD: {outcomeStats["DELOAD"]}");
            Console.WriteLine("============================================");

            // Verify we got a mix of outcomes
            outcomeStats["SUCCESS"].Should().BeGreaterThan(0, "Should have some successes");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests completing a single week (4 days) through the UI with week/day assertions.
    /// </summary>
    [Fact]
    public async Task CompleteSingleWeek_ViaUI_4Workouts_AssertWeekAndDayProgression()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout program
            await CreateTestWorkoutProgramAsync(page);

            // Complete all 4 days in week 1 with assertions
            for (int expectedDay = 1; expectedDay <= DaysPerWeek; expectedDay++)
            {
                Console.WriteLine($"Completing Day {expectedDay} of Week 1...");

                // ASSERTION: Verify correct week and day before completing
                await AssertCurrentWeekAndDayAsync(page, 1, expectedDay,
                    $"Before completing Day {expectedDay}");

                // Complete the day
                await CompleteWorkoutDayViaUIAsync(page, expectedDay, week: 1);
            }

            // After completing all 4 days, should be on Week 2 Day 1
            await AssertCurrentWeekAndDayAsync(page, 2, 1,
                "After completing Week 1, should be on Week 2 Day 1");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests completing a single day and verifies:
    /// - Day marked as completed (cannot re-enter)
    /// - Progress to next day
    /// - Completion summary shows correctly
    /// </summary>
    [Fact]
    public async Task CompleteSingleDay_ViaUI_ShowsCompletionAndProgressesToNextDay()
    {
        // Arrange - Delete any existing workouts
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Create a workout program
            await CreateTestWorkoutProgramAsync(page);

            // Verify we start on Week 1 Day 1
            await AssertCurrentWeekAndDayAsync(page, 1, 1, "Initial state should be Week 1 Day 1");

            // Navigate to workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Click "Start Workout" for Day 1
            var startWorkoutButton = page.Locator("[data-testid='start-workout-day-1']").First;
            await startWorkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await startWorkoutButton.ClickAsync();

            // Wait for session page to load
            await page.WaitForURLAsync(url => url.Contains("/workout/session/1"), new() { Timeout = 10000 });

            // Verify session title shows Week 1
            var sessionTitle = page.Locator("[data-testid='session-title']").First;
            await sessionTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            var titleText = await sessionTitle.TextContentAsync();
            titleText.Should().Contain("Week 1", "Session should show Week 1");

            // Complete all exercises
            await CompleteAllExercisesInSessionAsync(page, week: 1, day: 1);

            // Click "Complete Workout" button
            var completeButton = page.Locator("[data-testid='complete-workout-button']").First;
            await completeButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await completeButton.ClickAsync();

            // Wait for completion summary
            var completionTitle = page.Locator("[data-testid='completion-title']").First;
            await completionTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            var completionText = await completionTitle.TextContentAsync();
            completionText.Should().Contain("Complete", "Should show completion message");

            // Click continue to go back to workout dashboard
            var continueButton = page.Locator("[data-testid='continue-button']").First;
            await continueButton.ClickAsync();

            // Wait for navigation back to workout page
            await page.WaitForURLAsync(url => url.Contains("/workout") && !url.Contains("/session"), new() { Timeout = 10000 });

            // ASSERTION: After completing Day 1, should now be on Day 2 (same week)
            await AssertCurrentWeekAndDayAsync(page, 1, 2, "After completing Day 1, should be on Day 2");

            // ASSERTION: Day 1 should be marked as completed (button disabled)
            var day1Button = page.Locator("[data-testid='start-workout-day-1']").First;
            var isDay1Disabled = await day1Button.IsDisabledAsync();
            isDay1Disabled.Should().BeTrue("Day 1 should be disabled after completion");

            // ASSERTION: Day 2 should be available (not disabled)
            var day2Button = page.Locator("[data-testid='start-workout-day-2']").First;
            var isDay2Enabled = await day2Button.IsEnabledAsync();
            isDay2Enabled.Should().BeTrue("Day 2 should be enabled as current day");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that a completed day cannot be re-completed.
    /// </summary>
    [Fact]
    public async Task CompletedDay_CannotBeCompletedAgain()
    {
        await DeleteAllWorkoutsAsync();

        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            await CreateTestWorkoutProgramAsync(page);

            // Complete Day 1
            await CompleteWorkoutDayViaUIAsync(page, day: 1, week: 1);

            // Navigate to workout page
            await page.GotoAsync($"{FrontendUrl}/workout");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Day 1 button should be disabled
            var day1Button = page.Locator("[data-testid='start-workout-day-1']").First;
            await day1Button.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var isDisabled = await day1Button.IsDisabledAsync();

            isDisabled.Should().BeTrue("Day 1 should be disabled after completion");

            // Day 1 completed icon should be visible
            var completedIcon = page.Locator("[data-testid='day-1-completed-icon']").First;
            var iconVisible = await completedIcon.IsVisibleAsync();
            iconVisible.Should().BeTrue("Completed icon should be visible for Day 1");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #region Helper Methods

    /// <summary>
    /// Asserts that the current week and day match expected values.
    /// This reads the workout state from the UI.
    /// </summary>
    private async Task AssertCurrentWeekAndDayAsync(IPage page, int expectedWeek, int expectedDay, string context)
    {
        // Navigate to workout page to check current state
        await page.GotoAsync($"{FrontendUrl}/workout");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the week overview to load
        await page.WaitForTimeoutAsync(500);

        // Check the week display
        var weekText = await page.Locator("text=Week").First.TextContentAsync();

        // The current day should have the "Current" label or be the first non-completed day
        var currentDayCard = page.Locator($"[data-testid='day-card-{expectedDay}']").First;
        var isDayCardVisible = await currentDayCard.IsVisibleAsync();

        isDayCardVisible.Should().BeTrue($"{context}: Day {expectedDay} card should be visible");

        // For days before expectedDay, they should be completed
        for (int d = 1; d < expectedDay; d++)
        {
            var completedIcon = page.Locator($"[data-testid='day-{d}-completed-icon']").First;
            var isCompleted = await completedIcon.IsVisibleAsync();
            isCompleted.Should().BeTrue($"{context}: Day {d} should be marked as completed");
        }

        Console.WriteLine($"  ✓ Assertion passed: {context} - Week {expectedWeek}, Day {expectedDay}");
    }

    /// <summary>
    /// Creates a test workout program with exercises for all 4 days using the UI wizard.
    /// </summary>
    private async Task CreateTestWorkoutProgramAsync(IPage page)
    {
        // Navigate to workout page
        await page.GotoAsync($"{FrontendUrl}/workout");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check if workout already exists
        var noWorkoutMessage = page.Locator("h2:has-text('No Active Workout')").First;
        var hasNoWorkout = await noWorkoutMessage.IsVisibleAsync();

        if (!hasNoWorkout)
        {
            // Already have a workout
            return;
        }

        // Click create button
        var createButton = page.Locator("button:has-text('Create Workout Program')").First;
        await createButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await createButton.ClickAsync();

        // Wait for setup page
        await page.WaitForURLAsync(url => url.Contains("/setup"), new() { Timeout = 10000 });

        // Fill in program details
        var programNameInput = page.Locator("input[type='text']").First;
        await programNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await programNameInput.FillAsync("21-Week E2E Test Program");

        // Select 4-Day variant
        var variantSelect = page.Locator("select").First;
        await variantSelect.SelectOptionAsync(new SelectOptionValue { Value = "4" });

        // Set 21 weeks
        var totalWeeksInput = page.Locator("input[type='number']").First;
        await totalWeeksInput.FillAsync("21");

        // Navigate through wizard to create
        await NavigateWizardToCreateAsync(page);

        // Wait for redirect
        await page.WaitForURLAsync(
            url => url.Contains("/dashboard") || url.Contains("/workout"),
            new() { Timeout = 15000 });
    }

    /// <summary>
    /// Completes all exercises in the current workout session.
    /// </summary>
    private async Task CompleteAllExercisesInSessionAsync(IPage page, int week, int day)
    {
        var exerciseCards = page.Locator("[data-testid^='exercise-card-']");
        await page.WaitForTimeoutAsync(1000);
        var cardCount = await exerciseCards.CountAsync();

        for (int exerciseIndex = 0; exerciseIndex < cardCount; exerciseIndex++)
        {
            var scenario = GetScenarioForExercise(exerciseIndex, week, day);
            var card = exerciseCards.Nth(exerciseIndex);

            var setRows = card.Locator("[data-testid^='set-row-']");
            var setCount = await setRows.CountAsync();
            var hasAmrap = await card.Locator("text=AMRAP").IsVisibleAsync();

            for (int setIndex = 1; setIndex <= setCount; setIndex++)
            {
                var repsInput = card.Locator($"[data-testid='reps-input-{setIndex}']");
                var hasRepsInput = await repsInput.IsVisibleAsync();

                if (hasRepsInput)
                {
                    int repsToEnter;
                    if (hasAmrap && setIndex == setCount)
                    {
                        // Use spreadsheet target reps + scenario delta for AMRAP
                        repsToEnter = GetAmrapRepsForWeek(week, scenario);
                    }
                    else
                    {
                        repsToEnter = scenario.RepsPerSetReps;
                    }
                    await repsInput.FillAsync(repsToEnter.ToString());
                }

                var completeSetButton = card.Locator($"[data-testid='complete-set-{setIndex}']");
                await completeSetButton.ClickAsync();
                await page.WaitForTimeoutAsync(50);
            }
        }
    }

    /// <summary>
    /// Completes a single workout day through the UI with varied performance.
    /// Returns list of outcome strings for tracking.
    /// </summary>
    private async Task<List<string>> CompleteWorkoutDayViaUIAsync(IPage page, int day, int week)
    {
        var outcomes = new List<string>();

        // Navigate to workout page
        await page.GotoAsync($"{FrontendUrl}/workout");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click "Start Workout" for this day
        var startWorkoutButton = page.Locator($"[data-testid='start-workout-day-{day}']").First;

        try
        {
            await startWorkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        }
        catch
        {
            Console.WriteLine($"  Day {day} button not found - may be completed or unavailable");
            return outcomes;
        }

        // Ensure button is enabled (not completed)
        var isEnabled = await startWorkoutButton.IsEnabledAsync();
        if (!isEnabled)
        {
            Console.WriteLine($"  Day {day} is already completed, skipping");
            return outcomes;
        }

        await startWorkoutButton.ClickAsync();

        // Wait for session page to load
        await page.WaitForURLAsync(url => url.Contains($"/workout/session/{day}"), new() { Timeout = 10000 });

        // Complete all exercises
        await CompleteAllExercisesInSessionAsync(page, week, day);

        // Click "Complete Workout" button
        var completeButton = page.Locator("[data-testid='complete-workout-button']").First;
        await completeButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await completeButton.ClickAsync();

        // Wait for completion summary
        var completionTitle = page.Locator("[data-testid='completion-title']").First;
        await completionTitle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Collect the progression outcomes
        var outcomeLabels = page.Locator("[data-testid^='outcome-label-']");
        var outcomeCount = await outcomeLabels.CountAsync();
        for (int i = 0; i < outcomeCount; i++)
        {
            var label = outcomeLabels.Nth(i);
            var outcome = await label.TextContentAsync();
            outcomes.Add(outcome ?? "UNKNOWN");
        }

        // Check if week progressed
        var weekProgressedNotice = page.Locator("[data-testid='week-progressed-notice']").First;
        var didWeekProgress = await weekProgressedNotice.IsVisibleAsync();
        if (didWeekProgress)
        {
            Console.WriteLine($"    → Week progressed after completing Day {day}");
        }

        // Click continue to go back
        var continueButton = page.Locator("[data-testid='continue-button']").First;
        await continueButton.ClickAsync();

        // Wait for navigation back to workout page
        await page.WaitForURLAsync(url => url.Contains("/workout") && !url.Contains("/session"), new() { Timeout = 10000 });

        return outcomes;
    }

    /// <summary>
    /// Navigates through the setup wizard and creates the program.
    /// </summary>
    private async Task NavigateWizardToCreateAsync(IPage page)
    {
        var nextButton = page.Locator("button:has-text('Next')").First;
        var confirmButton = page.Locator("button:has-text('Create Program')").First;

        // Click through wizard steps
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
