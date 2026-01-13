using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Debug test to inspect page content
/// </summary>
[Collection("E2E")]
public class DebugTest : E2ETestBase
{
    [Fact]
    public async Task Debug_PrintPageContent()
    {
        var page = await CreatePageAsync();

        try
        {
            await page.GotoAsync($"{FrontendUrl}/sign-in");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Take screenshot
            await page.ScreenshotAsync(new() { Path = "login-page-debug.png", FullPage = true });

            // Get page title
            var title = await page.TitleAsync();
            Console.WriteLine($"Page Title: {title}");

            // Get all h1 elements
            var h1Elements = await page.Locator("h1").AllAsync();
            Console.WriteLine($"Number of H1 elements: {h1Elements.Count}");
            foreach (var h1 in h1Elements)
            {
                var text = await h1.TextContentAsync();
                Console.WriteLine($"  H1: {text}");
            }

            // Get all input elements
            var inputs = await page.Locator("input").AllAsync();
            Console.WriteLine($"Number of input elements: {inputs.Count}");
            foreach (var input in inputs)
            {
                var type = await input.GetAttributeAsync("type");
                var id = await input.GetAttributeAsync("id");
                var placeholder = await input.GetAttributeAsync("placeholder");
                Console.WriteLine($"  Input: type={type}, id={id}, placeholder={placeholder}");
            }

            // Get all buttons
            var buttons = await page.Locator("button").AllAsync();
            Console.WriteLine($"Number of buttons: {buttons.Count}");
            foreach (var button in buttons)
            {
                var text = await button.TextContentAsync();
                var type = await button.GetAttributeAsync("type");
                Console.WriteLine($"  Button: type={type}, text={text}");
            }

            // Get all links
            var links = await page.Locator("a").AllAsync();
            Console.WriteLine($"Number of links: {links.Count}");
            foreach (var link in links)
            {
                var href = await link.GetAttributeAsync("href");
                var text = await link.TextContentAsync();
                Console.WriteLine($"  Link: href={href}, text={text}");
            }

            // Print page HTML
            var html = await page.ContentAsync();
            System.IO.File.WriteAllText("login-page-debug.html", html);
            Console.WriteLine("HTML saved to login-page-debug.html");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
