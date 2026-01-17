using A2S.Tests.Shared;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Base class for E2E tests that provides both backend API and frontend browser automation.
/// Uses TestWebApplicationFactory for the backend and Playwright for browser automation.
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly FrontendFixture FrontendFixture;
    protected TestWebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient ApiClient { get; private set; } = null!;
    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected string ApiBaseUrl { get; private set; } = null!;

    /// <summary>
    /// The frontend URL from the fixture.
    /// </summary>
    protected string FrontendUrl => FrontendFixture.FrontendUrl;

    protected E2ETestBase(FrontendFixture frontendFixture)
    {
        FrontendFixture = frontendFixture;
    }

    public virtual async Task InitializeAsync()
    {
        // Initialize backend
        Factory = new TestWebApplicationFactory<Program>();
        await Factory.InitializeAsync();
        ApiClient = Factory.CreateClient();
        ApiBaseUrl = ApiClient.BaseAddress?.ToString() ?? throw new InvalidOperationException("API base URL not set");

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
        ApiClient?.Dispose();

        if (Factory != null)
        {
            await Factory.DisposeAsync();
        }
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
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
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
        await page.WaitForSelectorAsync("h1", new() { Timeout = 15000 });
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
    /// </summary>
    protected async Task FillSignInFormAsync(IPage page, string email, string password)
    {
        await WaitForClerkSignInAsync(page);

        var emailInput = page.Locator("#identifier-field").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(email);

        var passwordInput = page.Locator("#password-field").First;
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
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
}
