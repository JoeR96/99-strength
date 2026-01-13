using Microsoft.Playwright;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Debug test to capture console errors
/// </summary>
[Collection("E2E")]
public class DebugConsoleTest : E2ETestBase
{
    [Fact]
    public async Task Debug_CaptureConsoleErrors()
    {
        var page = await CreatePageAsync();
        var consoleMessages = new List<string>();
        var errors = new List<string>();

        page.Console += (_, msg) =>
        {
            var message = $"[{msg.Type}] {msg.Text}";
            consoleMessages.Add(message);
            Console.WriteLine(message);
        };

        page.PageError += (_, error) =>
        {
            var errorMsg = $"PAGE ERROR: {error}";
            errors.Add(errorMsg);
            Console.WriteLine(errorMsg);
        };

        try
        {
            await page.GotoAsync($"{FrontendUrl}/sign-in");

            // Wait a bit to let errors happen
            await Task.Delay(3000);

            // Try to get page content
            var html = await page.ContentAsync();
            System.IO.File.WriteAllText("page-with-errors.html", html);

            // Print summary
            Console.WriteLine($"\n=== SUMMARY ===");
            Console.WriteLine($"Console messages: {consoleMessages.Count}");
            Console.WriteLine($"Page errors: {errors.Count}");

            if (errors.Any())
            {
                Console.WriteLine("\n=== ERRORS ===");
                foreach (var error in errors)
                {
                    Console.WriteLine(error);
                }
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
