using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// E2E tests for User Management functionality.
/// Tests the auto-provisioning of users and user data display through the frontend.
/// </summary>
[Collection("E2E")]
public class UserManagementE2ETests : E2ETestBase
{
    public UserManagementE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    /// <summary>
    /// Tests that when a user logs in for the first time, their account is auto-provisioned
    /// and they can see the dashboard with personalized content.
    /// </summary>
    [Fact]
    public async Task Dashboard_ShouldDisplayUserInfo_AfterLogin()
    {
        // Arrange & Act - Login with valid credentials
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Assert - Verify the welcome message shows (indicates user was provisioned and recognized)
            var welcomeHeading = page.Locator("h2:has-text('Welcome back')").First;
            await welcomeHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var welcomeVisible = await welcomeHeading.IsVisibleAsync();
            welcomeVisible.Should().BeTrue("Welcome message should be visible on dashboard");

            // Verify Quick Stats card is visible (confirms dashboard loaded properly)
            var quickStats = page.Locator("text=Quick Stats").First;
            var quickStatsVisible = await quickStats.IsVisibleAsync();
            quickStatsVisible.Should().BeTrue("Quick Stats card should be visible on dashboard");

            // Verify the user button is present (shows user is authenticated)
            var userButton = page.Locator(".cl-userButtonTrigger, .cl-userButton, .cl-avatarBox").First;
            await userButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var userButtonVisible = await userButton.IsVisibleAsync();
            userButtonVisible.Should().BeTrue("User button should be visible, indicating user is authenticated");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that a user remains logged in and can access protected pages
    /// within the same session.
    /// </summary>
    [Fact]
    public async Task AuthenticatedUser_ShouldAccessProtectedPages()
    {
        // Arrange - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Verify we're on dashboard
            page.Url.Should().Contain("/dashboard");

            // Refresh the page to ensure session persists
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Should still be on dashboard (session persists)
            await page.WaitForURLAsync(url => url.Contains("/dashboard"), new() { Timeout = 15000 });
            page.Url.Should().Contain("/dashboard", "User should remain authenticated after page refresh");

            // Verify authenticated content is still visible
            var welcomeMessage = page.Locator("h2:has-text('Welcome')").First;
            await welcomeMessage.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var welcomeVisible = await welcomeMessage.IsVisibleAsync();
            welcomeVisible.Should().BeTrue("Welcome message should still be visible after refresh");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that the user button is displayed for authenticated users
    /// and allows access to sign-out functionality.
    /// </summary>
    [Fact]
    public async Task Dashboard_ShouldDisplayUserButton_ForSignOut()
    {
        // Arrange - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Assert - Verify user button is displayed (Clerk's UserButton component)
            var userButton = page.Locator(".cl-userButtonTrigger, .cl-userButton, .cl-avatarBox").First;
            await userButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var userButtonVisible = await userButton.IsVisibleAsync();
            userButtonVisible.Should().BeTrue("User button should be visible for authenticated users");

            // Click the user button to open the menu
            await userButton.ClickAsync();

            // Verify the sign-out option is available in the menu
            var signOutOption = page.Locator("button:has-text('Sign out'), [data-localization-key='signOut']").First;
            await signOutOption.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            var signOutVisible = await signOutOption.IsVisibleAsync();
            signOutVisible.Should().BeTrue("Sign out option should be available in user menu");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that signing out redirects the user appropriately
    /// and they no longer have access to protected pages.
    /// </summary>
    [Fact]
    public async Task SignOut_ShouldRedirectToLoginPage()
    {
        // Arrange - Login first
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Act - Click user button to open menu
            var userButton = page.Locator(".cl-userButtonTrigger, .cl-userButton, .cl-avatarBox").First;
            await userButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await userButton.ClickAsync();

            // Click sign out
            var signOutButton = page.Locator("button:has-text('Sign out'), [data-localization-key='signOut']").First;
            await signOutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await signOutButton.ClickAsync();

            // Wait for redirect to sign-in page or home page
            await page.WaitForURLAsync(
                url => url.Contains("/sign-in") || !url.Contains("/dashboard"),
                new() { Timeout = 15000 });

            // Assert - User should no longer be on dashboard
            page.Url.Should().NotContain("/dashboard", "User should be redirected away from dashboard after sign out");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that the dashboard shows personalized welcome content confirming authentication.
    /// </summary>
    [Fact]
    public async Task Dashboard_ShouldShowAuthenticationConfirmation()
    {
        // Arrange & Act - Login
        var page = await LoginAndNavigateToDashboardAsync();

        try
        {
            // Assert - Verify personalized welcome message (confirms authentication)
            var welcomeHeading = page.Locator("h2:has-text('Welcome back')").First;
            await welcomeHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var welcomeText = await welcomeHeading.TextContentAsync();
            welcomeText.Should().NotBeNullOrEmpty("Welcome message should confirm user is authenticated");

            // Verify the A2S Workout Tracker title is visible in nav
            var navTitle = page.Locator("h1:has-text('A2S Workout Tracker')").First;
            var navTitleVisible = await navTitle.IsVisibleAsync();
            navTitleVisible.Should().BeTrue("App title should be visible in navigation");

            // Verify the "Start A2S Program" button is visible
            var startButton = page.Locator("button:has-text('Start A2S Program')").First;
            var startButtonVisible = await startButton.IsVisibleAsync();
            startButtonVisible.Should().BeTrue("Start A2S Program button should be visible");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Tests that unauthenticated users cannot access the dashboard.
    /// </summary>
    [Fact]
    public async Task Dashboard_ShouldRequireAuthentication()
    {
        // Arrange - Create a new page without logging in
        var page = await CreatePageAsync();

        try
        {
            // Act - Try to navigate directly to dashboard
            await page.GotoAsync($"{FrontendUrl}/dashboard");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for either redirect to sign-in or some indication of unauthenticated state
            // Clerk should redirect unauthenticated users
            await Task.Delay(2000); // Allow time for any redirects

            // Assert - Either redirected to sign-in or dashboard content is not visible
            var currentUrl = page.Url;
            var isDashboard = currentUrl.Contains("/dashboard");

            if (isDashboard)
            {
                // If still on dashboard URL, personalized content should not be visible
                var welcomeContent = page.Locator("h2:has-text('Welcome back')").First;
                var welcomeContentVisible = await welcomeContent.IsVisibleAsync();
                welcomeContentVisible.Should().BeFalse("Personalized content should not be visible for unauthenticated users");
            }
            else
            {
                // User was redirected away from dashboard
                currentUrl.Should().Contain("/sign-in", "Unauthenticated users should be redirected to sign-in");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
