using System.Diagnostics;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// Fixture that manages the frontend dev server for E2E tests.
/// Starts the Vite dev server before tests and stops it after.
/// </summary>
public class FrontendFixture : IAsyncLifetime
{
    private Process? _frontendProcess;
    private readonly string _frontendPath;

    public string FrontendUrl { get; } = "http://localhost:5173";

    public FrontendFixture()
    {
        // Navigate from test project to frontend project
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        _frontendPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "A2S.Web"));
    }

    public async Task InitializeAsync()
    {
        // Check if frontend is already running
        if (await IsFrontendRunningAsync())
        {
            Console.WriteLine("Frontend already running on port 5173");
            return;
        }

        // Ensure npm packages are installed
        await RunNpmInstallAsync();

        // Start the frontend dev server
        await StartFrontendAsync();

        // Wait for the frontend to be ready
        await WaitForFrontendAsync();
    }

    public async Task DisposeAsync()
    {
        await StopFrontendAsync();
    }

    private async Task<bool> IsFrontendRunningAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync(FrontendUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunNpmInstallAsync()
    {
        if (!Directory.Exists(Path.Combine(_frontendPath, "node_modules")))
        {
            Console.WriteLine("Installing npm packages...");

            var psi = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "install",
                WorkingDirectory = _frontendPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new Exception("Failed to start npm install process");
            }

            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"npm install failed: {error}");
            }
        }
    }

    private async Task StartFrontendAsync()
    {
        Console.WriteLine($"Starting frontend from: {_frontendPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "npm",
            Arguments = "run dev",
            WorkingDirectory = _frontendPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["BROWSER"] = "none" // Don't open browser
            }
        };

        _frontendProcess = Process.Start(psi);

        if (_frontendProcess == null)
        {
            throw new Exception("Failed to start frontend process");
        }

        // Don't wait for exit - the dev server runs continuously
        await Task.CompletedTask;
    }

    private async Task WaitForFrontendAsync(int timeoutSeconds = 60)
    {
        Console.WriteLine("Waiting for frontend to be ready...");

        var stopwatch = Stopwatch.StartNew();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                var response = await client.GetAsync(FrontendUrl);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Frontend ready after {stopwatch.Elapsed.TotalSeconds:F1}s");
                    return;
                }
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Frontend did not start within {timeoutSeconds} seconds");
    }

    private async Task StopFrontendAsync()
    {
        if (_frontendProcess != null && !_frontendProcess.HasExited)
        {
            Console.WriteLine("Stopping frontend...");

            try
            {
                // Kill the process tree (npm spawns child processes)
                _frontendProcess.Kill(entireProcessTree: true);
                await _frontendProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping frontend: {ex.Message}");
            }
            finally
            {
                _frontendProcess.Dispose();
                _frontendProcess = null;
            }
        }
    }
}

