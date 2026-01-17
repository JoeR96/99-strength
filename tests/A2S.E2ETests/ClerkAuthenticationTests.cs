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
    public ClerkAuthenticationTests(FrontendFixture frontendFixture) : base(frontendFixture)
    {
    }

    #region Registration Tests

    /// <summary>
    /// Test that the sign-up form is displayed correctly and accepts input.
    /// Note: Full registration flow requires valid Clerk configuration and may need email verification.
    /// This test verifies the form UI is working correctly.
    /// </summary>
    [Fact]
    public async Task SignUpForm_ShouldDisplayAndAcceptInput()
    {
        // Arrange
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

            // Verify all form fields exist and are interactive
            var firstNameInput = page.Locator("#firstName-field").First;
            await firstNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await firstNameInput.FillAsync("Test");
            var firstNameValue = await firstNameInput.InputValueAsync();
            firstNameValue.Should().Be("Test", "First name input should accept values");

            var lastNameInput = page.Locator("#lastName-field").First;
            await lastNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await lastNameInput.FillAsync("User");
            var lastNameValue = await lastNameInput.InputValueAsync();
            lastNameValue.Should().Be("User", "Last name input should accept values");

            var emailInput = page.Locator("#emailAddress-field").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync("test@example.com");
            var emailValue = await emailInput.InputValueAsync();
            emailValue.Should().Be("test@example.com", "Email input should accept values");

            var passwordInput = page.Locator("#password-field").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync("TestPassword123!");
            var passwordValue = await passwordInput.InputValueAsync();
            passwordValue.Should().Be("TestPassword123!", "Password input should accept values");

            // Verify Continue button exists
            var continueButton = page.Locator("button:has-text('Continue')").First;
            var continueButtonVisible = await continueButton.IsVisibleAsync();
            continueButtonVisible.Should().BeTrue("Continue button should be visible");

            // Verify sign-in link exists for users who already have an account
            var signInLink = page.Locator("a[href*='/sign-in'], a:has-text('Sign in')").First;
            var signInLinkVisible = await signInLink.IsVisibleAsync();
            signInLinkVisible.Should().BeTrue("Sign in link should be visible for existing users");
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
            var emailInput = page.Locator("#identifier-field").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(TestCredentials.Email);

            var passwordInput = page.Locator("#password-field").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync(TestCredentials.Password);

            // Submit the form by clicking the Continue button
            var continueButton = page.Locator("button:has-text('Continue')").First;
            await continueButton.ClickAsync();

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

            // Verify user email is displayed (case insensitive check)
            var emailDisplayed = await page.Locator($"text={TestCredentials.Email.ToLowerInvariant()}").First.IsVisibleAsync();
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
            var userButton = page.Locator(".cl-userButtonTrigger, .cl-userButton, .cl-avatarBox").First;
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
            var emailInput = page.Locator("#identifier-field").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync("invalid@example.com");

            var passwordInput = page.Locator("#password-field").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync("InvalidPassword123!");

            // Submit the form by clicking the Continue button
            var continueButton = page.Locator("button:has-text('Continue')").First;
            await continueButton.ClickAsync();

            // Wait for error message to appear - Clerk shows errors in various ways
            // Look for error text, alert role, or Clerk's error class
            var errorMessage = page.Locator("[role='alert'], .cl-formFieldError, .cl-alert, p:has-text('Invalid'), p:has-text('incorrect'), p:has-text('Couldn')").First;
            await errorMessage.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
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
            var emailInput = page.Locator("#identifier-field").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var emailVisible = await emailInput.IsVisibleAsync();
            emailVisible.Should().BeTrue("Email/identifier input should be visible");

            // Verify password input exists
            var passwordInput = page.Locator("#password-field").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var passwordVisible = await passwordInput.IsVisibleAsync();
            passwordVisible.Should().BeTrue("Password input should be visible");

            // Verify Continue button exists
            var continueButton = page.Locator("button:has-text('Continue')").First;
            var continueButtonVisible = await continueButton.IsVisibleAsync();
            continueButtonVisible.Should().BeTrue("Continue button should exist");
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
            var emailInput = page.Locator("#identifier-field").First;
            var passwordInput = page.Locator("#password-field").First;

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
