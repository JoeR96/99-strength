using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Comprehensive E2E tests for Clerk authentication flows.
/// Tests both registration with random credentials and login with existing user.
/// Verifies complete flow including dashboard redirect and authenticated content.
/// </summary>
[Collection("E2E")]
public class ClerkAuthenticationTests : E2ETestBase
{
    #region Registration Tests

    /// <summary>
    /// Test that a new user can register with random email/password and reach the dashboard.
    /// This test:
    /// 1. Generates random credentials
    /// 2. Navigates to sign-up page
    /// 3. Fills and submits registration form
    /// 4. Verifies redirect to dashboard
    /// 5. Verifies dashboard displays authenticated content
    /// </summary>
    [Fact]
    public async Task Registration_WithRandomCredentials_ShouldSucceedAndReachDashboard()
    {
        // Arrange
        var email = TestCredentials.GenerateRandomEmail();
        var password = TestCredentials.GenerateRandomPassword();
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-up page
            await page.GotoAsync($"{FrontendUrl}/sign-up");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for React to hydrate
            await page.WaitForSelectorAsync("h1", new() { Timeout = 15000 });

            // Verify page heading
            var heading = await page.Locator("h1").First.TextContentAsync();
            heading.Should().Contain("A2S Workout Tracker");

            // Wait for Clerk SignUp component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in the registration form
            var emailInput = page.Locator("input[name='emailAddress'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(email);

            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync(password);

            // Submit the form
            await passwordInput.PressAsync("Enter");

            // Wait for Clerk to process registration and redirect to dashboard
            // Note: This assumes instant sign-up is enabled in Clerk (no email verification)
            await page.WaitForURLAsync(url => url.Contains("/dashboard"), new() { Timeout = 60000 });

            // Assert - Verify we're on the dashboard
            var currentUrl = page.Url;
            currentUrl.Should().Contain("/dashboard", "User should be redirected to dashboard after registration");

            // Verify welcome message
            var welcomeHeading = page.Locator("h2:has-text('Welcome')").First;
            await welcomeHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var welcomeText = await welcomeHeading.TextContentAsync();
            welcomeText.Should().NotBeNullOrEmpty("Welcome message should be visible");

            // Verify authentication message
            var authMessage = await page.Locator("text=You are successfully authenticated").First.IsVisibleAsync();
            authMessage.Should().BeTrue("Dashboard should show authentication confirmation");

            // Verify user email is displayed
            var emailDisplayed = await page.Locator($"text={email}").First.IsVisibleAsync();
            emailDisplayed.Should().BeTrue("User email should be displayed on dashboard");

            // Verify user button (for sign-out) is present
            var userButton = page.Locator(".cl-userButton, [data-clerk-component='userButton']").First;
            await userButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var userButtonVisible = await userButton.IsVisibleAsync();
            userButtonVisible.Should().BeTrue("User button should be visible for authenticated users");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #endregion

    #region Login Tests

    /// <summary>
    /// Test that an existing user can login with valid credentials and reach the dashboard.
    /// This is the primary authentication test that verifies:
    /// 1. Login form is displayed correctly
    /// 2. User can enter credentials and submit
    /// 3. Clerk authenticates the user
    /// 4. User is redirected to dashboard
    /// 5. Dashboard displays user information (email, user ID, welcome message)
    /// 6. User button for sign-out is available
    /// </summary>
    [Fact]
    public async Task Login_WithValidCredentials_ShouldSucceedAndReachDashboard()
    {
        // Arrange
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-in page
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for React to hydrate
            await page.WaitForSelectorAsync("h1", new() { Timeout = 15000 });

            // Verify page elements
            var heading = await page.Locator("h1").First.TextContentAsync();
            heading.Should().Contain("A2S Workout Tracker", "Login page should display app title");

            var subtitle = await page.Locator("text=Sign in to track your progress").First.IsVisibleAsync();
            subtitle.Should().BeTrue("Login page should display subtitle");

            // Wait for Clerk SignIn component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in the login form with test credentials
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(TestCredentials.Email);

            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync(TestCredentials.Password);

            // Submit the form
            await passwordInput.PressAsync("Enter");

            // Wait for Clerk to authenticate and redirect to dashboard
            await page.WaitForURLAsync(url => url.Contains("/dashboard"), new() { Timeout = 30000 });

            // Assert - Verify we're on the dashboard
            var currentUrl = page.Url;
            currentUrl.Should().Contain("/dashboard", "User should be redirected to dashboard after login");

            // Verify welcome message with user name
            var welcomeHeading = page.Locator("h2:has-text('Welcome')").First;
            await welcomeHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var welcomeText = await welcomeHeading.TextContentAsync();
            welcomeText.Should().NotBeNullOrEmpty("Welcome message should be displayed");

            // Verify authentication confirmation message
            var authMessage = await page.Locator("text=You are successfully authenticated").First.IsVisibleAsync();
            authMessage.Should().BeTrue("Dashboard should confirm user is authenticated");

            // Verify user email is displayed
            var emailDisplayed = await page.Locator($"text={TestCredentials.Email}").First.IsVisibleAsync();
            emailDisplayed.Should().BeTrue($"User email '{TestCredentials.Email}' should be displayed on dashboard");

            // Verify User Info section exists
            var userInfoHeading = await page.Locator("h3:has-text('User Info')").First.IsVisibleAsync();
            userInfoHeading.Should().BeTrue("User Info section should be visible");

            // Verify Email label exists
            var emailLabel = await page.Locator("dt:has-text('Email')").First.IsVisibleAsync();
            emailLabel.Should().BeTrue("Email label should be visible in User Info");

            // Verify User ID label exists
            var userIdLabel = await page.Locator("dt:has-text('User ID')").First.IsVisibleAsync();
            userIdLabel.Should().BeTrue("User ID label should be visible in User Info");

            // Verify user button (for sign-out) is present and functional
            var userButton = page.Locator(".cl-userButton, [data-clerk-component='userButton']").First;
            await userButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var userButtonVisible = await userButton.IsVisibleAsync();
            userButtonVisible.Should().BeTrue("User button should be visible for sign-out");

            // Verify navigation bar is present
            var nav = await page.Locator("nav").First.IsVisibleAsync();
            nav.Should().BeTrue("Navigation bar should be visible on dashboard");

            // Verify main content area is present
            var main = await page.Locator("main").First.IsVisibleAsync();
            main.Should().BeTrue("Main content area should be visible on dashboard");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Test that invalid credentials show appropriate error messages.
    /// </summary>
    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldShowErrorMessage()
    {
        // Arrange
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-in page
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in invalid credentials
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync("invalid@example.com");

            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync("InvalidPassword123!");

            // Submit the form
            await passwordInput.PressAsync("Enter");

            // Wait for error message to appear
            await page.WaitForTimeoutAsync(3000);

            // Assert - Clerk should display an error message
            var errorMessage = page.Locator(".cl-formFieldError, [role='alert']").First;
            await errorMessage.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var errorVisible = await errorMessage.IsVisibleAsync();
            errorVisible.Should().BeTrue("Error message should be displayed for invalid credentials");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #endregion

    #region Form Validation Tests

    /// <summary>
    /// Test that the login form displays all required fields.
    /// </summary>
    [Fact]
    public async Task LoginForm_ShouldDisplayRequiredFields()
    {
        // Arrange
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-in page
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Assert - Verify email/identifier input exists
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var emailVisible = await emailInput.IsVisibleAsync();
            emailVisible.Should().BeTrue("Email/identifier input should be visible");

            // Verify password input exists
            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var passwordVisible = await passwordInput.IsVisibleAsync();
            passwordVisible.Should().BeTrue("Password input should be visible");

            // Verify submit button exists
            var submitButtons = await page.Locator("button[type='submit']").CountAsync();
            submitButtons.Should().BeGreaterThan(0, "At least one submit button should exist");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Test that form fields are interactive and accept input.
    /// </summary>
    [Fact]
    public async Task LoginForm_ShouldBeInteractive()
    {
        // Arrange
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-in page
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in test data
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;

            await emailInput.FillAsync("test@example.com");
            await passwordInput.FillAsync("TestPassword123!");

            // Assert - Verify the inputs accepted the values
            var emailValue = await emailInput.InputValueAsync();
            var passwordValue = await passwordInput.InputValueAsync();

            emailValue.Should().Be("test@example.com", "Email input should accept values");
            passwordValue.Should().Be("TestPassword123!", "Password input should accept values");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #endregion

    #region Navigation Tests

    /// <summary>
    /// Test that sign-up link on login page navigates correctly.
    /// </summary>
    [Fact]
    public async Task LoginPage_ShouldHaveSignUpLink()
    {
        // Arrange
        var page = await CreatePageAsync();

        try
        {
            // Act - Navigate to sign-in page
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Assert - Verify sign-up link exists
            var signUpLink = page.Locator("a[href*='/sign-up'], a:has-text('Sign up')").First;
            await signUpLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var signUpVisible = await signUpLink.IsVisibleAsync();
            signUpVisible.Should().BeTrue("Sign up link should be visible");

            // Click and verify navigation
            await signUpLink.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/sign-up"), new() { Timeout = 10000 });
            page.Url.Should().Contain("/sign-up", "Should navigate to sign-up page");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    #endregion
}
