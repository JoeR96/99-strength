using A2S.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Base class for E2E tests that provides both backend API and frontend browser automation.
/// Uses TestWebApplicationFactory for the backend and Playwright for browser automation.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    protected TestWebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient ApiClient { get; private set; } = null!;
    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected string ApiBaseUrl { get; private set; } = null!;

    /// <summary>
    /// Override this to specify the frontend URL.
    /// For now, tests assume frontend is running on localhost:5173 (Vite dev server).
    /// In CI/CD, you would build and serve the frontend as part of the test setup.
    /// </summary>
    protected virtual string FrontendUrl => "http://localhost:5173";

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
        return await context.NewPageAsync();
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

        var emailInput = page.Locator("input[name='identifier'], input[type='email']").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(email);

        var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await passwordInput.FillAsync(password);
    }

    /// <summary>
    /// Fills in the Clerk sign-up form with the provided credentials.
    /// </summary>
    protected async Task FillSignUpFormAsync(IPage page, string email, string password)
    {
        await WaitForClerkSignUpAsync(page);

        var emailInput = page.Locator("input[name='emailAddress'], input[type='email']").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(email);

        var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
        await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await passwordInput.FillAsync(password);
    }

    /// <summary>
    /// Submits a Clerk form by pressing Enter on the password field.
    /// </summary>
    protected async Task SubmitClerkFormAsync(IPage page)
    {
        var passwordInput = page.Locator("input[name='password'], input[type='password']").First;
        await passwordInput.PressAsync("Enter");
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
