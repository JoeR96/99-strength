using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// End-to-end tests for authentication flow.
/// Tests the full stack: Frontend (React) -> Backend (API) -> Database (PostgreSQL).
///
/// Note: These tests verify the UI flow with Clerk's pre-built SignIn component.
/// Actual Clerk authentication requires valid credentials and API keys.
/// </summary>
[Collection("E2E")]
public class AuthE2ETests : E2ETestBase
{
    public AuthE2ETests(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
        : base(frontendFixture, apiFactory)
    {
    }

    /// <summary>
    /// Helper method to navigate to login page and wait for React to load.
    /// </summary>
    private new async Task<IPage> NavigateToLoginPageAsync()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{FrontendUrl}/sign-in");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for React to hydrate by waiting for the main heading
        await page.WaitForSelectorAsync("h1",  new() { Timeout = 15000 });

        return page;
    }

    [Fact]
    public async Task LoginPage_ShouldDisplayCorrectly()
    {
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Assert - Verify page title
            string? pageTitle = await page.TitleAsync();
            pageTitle.Should().NotBeNullOrEmpty();

            // Verify our app heading (not Clerk's heading)
            string? heading = await page.Locator("h1").First.TextContentAsync();
            heading.Should().Contain("A2S Workout Tracker");

            // Verify subtitle
            bool subtitleVisible = await page.Locator("text=Sign in to track your progress").IsVisibleAsync();
            subtitleVisible.Should().BeTrue("Subtitle should be visible");

            // Verify Clerk's SignIn component is loaded (look for Clerk's card)
            var clerkCard = page.Locator(".cl-card, [data-clerk-component]").First;
            await clerkCard.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            bool clerkComponentVisible = await clerkCard.IsVisibleAsync();
            clerkComponentVisible.Should().BeTrue("Clerk SignIn component should be visible");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task LoginForm_ShouldHaveRequiredFields()
    {
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Assert - Verify email/identifier input exists (Clerk uses identifier-field)
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            bool emailInputVisible = await emailInput.IsVisibleAsync();
            emailInputVisible.Should().BeTrue("Email/identifier input should be visible");

            // Verify password input exists
            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            bool passwordInputVisible = await passwordInput.IsVisibleAsync();
            passwordInputVisible.Should().BeTrue("Password input should be visible");

            // Verify submit button exists (Clerk may use hidden buttons for a11y, check for any button)
            var submitButtons = page.Locator("button[type='submit']");
            int buttonCount = await submitButtons.CountAsync();
            buttonCount.Should().BeGreaterThan(0, "At least one submit button should exist");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task LoginForm_ShouldValidateRequiredFields()
    {
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Verify input fields have required validation (HTML5 or Clerk's own)
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

            // Check if fields have required attribute or aria-required
            string? emailRequired = await emailInput.GetAttributeAsync("required");
            string? emailAriaRequired = await emailInput.GetAttributeAsync("aria-required");
            string? passwordRequired = await passwordInput.GetAttributeAsync("required");
            string? passwordAriaRequired = await passwordInput.GetAttributeAsync("aria-required");

            // At least one validation mechanism should be present for either field
            bool hasValidation = emailRequired != null || emailAriaRequired != null ||
                                passwordRequired != null || passwordAriaRequired != null;
            hasValidation.Should().BeTrue("Form fields should have validation attributes");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task LoginForm_ShouldBeInteractive()
    {
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in form with test data to verify it's interactive
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;

            await emailInput.FillAsync("test@example.com");
            await passwordInput.FillAsync("TestPassword123!");

            // Verify the inputs accepted the values
            string? emailValue = await emailInput.InputValueAsync();
            string? passwordValue = await passwordInput.InputValueAsync();

            emailValue.Should().Be("test@example.com", "Email input should accept values");
            passwordValue.Should().Be("TestPassword123!", "Password input should accept values");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task LoginPage_ShouldHaveSignUpLink()
    {
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Wait for Clerk component to load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Assert - Verify sign up link exists (Clerk creates these links)
            // Clerk's SignIn component has a "Don't have an account? Sign up" link
            var signUpLink = page.Locator("a[href*='/sign-up'], a:has-text('Sign up')").First;
            await signUpLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            bool signUpLinkVisible = await signUpLink.IsVisibleAsync();
            signUpLinkVisible.Should().BeTrue("Sign up link should be visible in Clerk component");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Backend_API_ShouldBeRunning()
    {
        // This test verifies the backend API is running and accessible
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

        // Act - Make a request to the API base URL
        var response = await httpClient.GetAsync($"{ApiBaseUrl}/");

        // Assert - API should respond (even with 404, it's running)
        response.Should().NotBeNull();

        // Verify we can make requests to the API
        // We expect 404 because there's no endpoint at root, but the server is responding
        int statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(new[] { 200, 404 }, "API should be running and responding to requests");
    }

    [Fact]
    public async Task FullStack_LoginFlow_WithValidCredentials_ShouldSucceed()
    {
        // This test verifies the complete login flow with Clerk authentication
        var page = await NavigateToLoginPageAsync();

        try
        {
            // Wait for Clerk component to fully load
            await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = 15000 });

            // Fill in credentials using Clerk's input fields
            var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(TestCredentials.Email);

            var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await passwordInput.FillAsync(TestCredentials.Password);

            // Submit the form - use Enter key or find visible button (Clerk may hide primary button)
            await passwordInput.PressAsync("Enter");

            // Wait for navigation to dashboard (with generous timeout for Clerk auth)
            // Clerk may take time to authenticate and redirect
            await page.WaitForURLAsync(url => url.Contains("/dashboard"), new() { Timeout = 30000 });

            // Verify we're on the dashboard page
            string currentUrl = page.Url;
            currentUrl.Should().Contain("/dashboard", "User should be redirected to dashboard after successful login");

            // Verify user is authenticated by checking for dashboard content
            // Look for Clerk's UserButton or dashboard-specific elements
            var dashboardContent = page.Locator("h1, .cl-userButton, [data-testid='dashboard']").First;
            await dashboardContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            bool isDashboardVisible = await dashboardContent.IsVisibleAsync();
            isDashboardVisible.Should().BeTrue("Dashboard page should be visible with authenticated content");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
