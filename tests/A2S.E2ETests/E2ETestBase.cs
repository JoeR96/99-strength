using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Base class for E2E tests that provides both backend API and frontend browser automation.
/// Uses E2EWebApplicationFactory for the backend (with real Clerk auth) and Playwright for browser automation.
/// Both the backend and frontend are started via collection fixtures shared across tests.
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly FrontendFixture FrontendFixture;
    protected readonly E2EWebApplicationFactory ApiFactory;
    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// The frontend URL from the fixture.
    /// </summary>
    protected string FrontendUrl => FrontendFixture.FrontendUrl;

    /// <summary>
    /// The API base URL from the factory.
    /// </summary>
    protected string ApiBaseUrl => ApiFactory.ApiBaseUrl;

    protected E2ETestBase(FrontendFixture frontendFixture, E2EWebApplicationFactory apiFactory)
    {
        FrontendFixture = frontendFixture;
        ApiFactory = apiFactory;
    }

    public virtual async Task InitializeAsync()
    {
        // Backend and frontend are already started by collection fixtures

        // Initialize Playwright
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true, // Set to false for debugging
            SlowMo = 0 // Slow down by N ms for debugging
        });
    }

    public virtual async Task DisposeAsync()
    {
        if (Browser != null)
        {
            await Browser.DisposeAsync();
        }

        Playwright?.Dispose();
    }

    /// <summary>
    /// Creates a new browser context with isolated storage.
    /// </summary>
    protected async Task<IBrowserContext> CreateBrowserContextAsync()
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            RecordVideoDir = "test-results/videos/", // Record videos for debugging
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 },
            IgnoreHTTPSErrors = true // Ignore HTTPS certificate errors for localhost
        });
    }

    /// <summary>
    /// Creates a new page in a new browser context.
    /// </summary>
    protected async Task<IPage> CreatePageAsync()
    {
        var context = await CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        // Capture console messages for debugging
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER {msg.Type.ToUpper()}] {msg.Text}");
        page.PageError += (_, error) => Console.WriteLine($"[PAGE ERROR] {error}");

        return page;
    }

    /// <summary>
    /// Navigates to the login page and waits for it to load.
    /// </summary>
    protected async Task<IPage> NavigateToLoginPageAsync()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{FrontendUrl}/sign-in");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for Clerk's sign-in component to load (more robust than waiting for h1)
        // Clerk uses various selectors, so we wait for common ones
        await page.WaitForSelectorAsync(".cl-card, .cl-rootBox, [data-clerk-component], #clerk-components", new() { Timeout = 30000 });
        return page;
    }

    /// <summary>
    /// Navigates to the sign-up page and waits for it to load.
    /// </summary>
    protected async Task<IPage> NavigateToSignUpPageAsync()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{FrontendUrl}/sign-up");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync("h1", new() { Timeout = 15000 });
        return page;
    }

    /// <summary>
    /// Waits for Clerk's sign-in component to be fully loaded.
    /// </summary>
    protected async Task WaitForClerkSignInAsync(IPage page, int timeout = 15000)
    {
        await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = timeout });
    }

    /// <summary>
    /// Waits for Clerk's sign-up component to be fully loaded.
    /// </summary>
    protected async Task WaitForClerkSignUpAsync(IPage page, int timeout = 15000)
    {
        await page.WaitForSelectorAsync(".cl-card, [data-clerk-component]", new() { Timeout = timeout });
    }

    /// <summary>
    /// Fills in the Clerk sign-in form with the provided credentials.
    /// Clerk uses a 2-step flow: first email, then password on a separate screen.
    /// </summary>
    protected async Task FillSignInFormAsync(IPage page, string email, string password)
    {
        await WaitForClerkSignInAsync(page);

        // Step 1: Enter email
        var emailInput = page.Locator("#identifier-field").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await emailInput.FillAsync(email);

        // Click Continue to proceed to password step
        var continueButton = page.Locator("button:has-text('Continue')").First;
        await continueButton.ClickAsync();

        // Step 2: Wait for password field and enter password
        var passwordInput = page.Locator("#password-field").First;
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await passwordInput.FillAsync(password);
    }

    /// <summary>
    /// Fills in the Clerk sign-up form with the provided credentials.
    /// </summary>
    protected async Task FillSignUpFormAsync(IPage page, string email, string password)
    {
        await WaitForClerkSignUpAsync(page);

        var emailInput = page.Locator("#emailAddress-field").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(email);

        var passwordInput = page.Locator("#password-field").First;
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await passwordInput.FillAsync(password);
    }

    /// <summary>
    /// Submits a Clerk form by clicking the Continue button.
    /// </summary>
    protected async Task SubmitClerkFormAsync(IPage page)
    {
        var continueButton = page.Locator("button:has-text('Continue')").First;
        await continueButton.ClickAsync();
    }

    /// <summary>
    /// Waits for navigation to the dashboard after authentication.
    /// </summary>
    protected async Task WaitForDashboardAsync(IPage page, int timeout = 30000)
    {
        await page.WaitForURLAsync(url => url.Contains("/dashboard"), new() { Timeout = timeout });
    }

    /// <summary>
    /// Performs a complete login flow and returns a page on the dashboard.
    /// </summary>
    protected async Task<IPage> LoginAndNavigateToDashboardAsync(string? email = null, string? password = null)
    {
        var page = await NavigateToLoginPageAsync();
        await FillSignInFormAsync(page, email ?? TestCredentials.Email, password ?? TestCredentials.Password);
        await SubmitClerkFormAsync(page);
        await WaitForDashboardAsync(page);
        return page;
    }

    /// <summary>
    /// Deletes all workouts to reset the test state.
    /// Call this before tests that expect no workout to exist.
    /// </summary>
    protected async Task DeleteAllWorkoutsAsync()
    {
        await ApiFactory.DeleteAllWorkoutsAsync();
    }
}
