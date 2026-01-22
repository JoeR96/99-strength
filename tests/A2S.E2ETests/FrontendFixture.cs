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
    private bool _weStartedTheServer;

    public string FrontendUrl { get; } = "http://localhost:5173";

    public FrontendFixture()
    {
        // Navigate from test project to frontend project
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        _frontendPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "A2S.Web"));
    }

    public async Task InitializeAsync()
    {
        // Check if frontend is already running and responding with valid content
        if (await IsFrontendHealthyAsync())
        {
            Console.WriteLine("Frontend already running and healthy on port 5173");
            _weStartedTheServer = false;
            return;
        }

        // Kill any stale processes on port 5173
        await KillProcessOnPortAsync(5173);

        // Ensure npm packages are installed
        await RunNpmInstallAsync();

        // Start the frontend dev server
        await StartFrontendAsync();

        // Wait for the frontend to be ready
        await WaitForFrontendAsync();

        _weStartedTheServer = true;
        Console.WriteLine("Frontend started successfully by test fixture");
    }

    public async Task DisposeAsync()
    {
        // Only stop the frontend if we started it
        if (_weStartedTheServer)
        {
            await StopFrontendAsync();
        }
    }

    /// <summary>
    /// Checks if the frontend is not just listening, but actually serving content.
    /// </summary>
    private async Task<bool> IsFrontendHealthyAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(FrontendUrl);
            if (!response.IsSuccessStatusCode)
                return false;

            // Verify we get actual HTML content, not an error page
            var content = await response.Content.ReadAsStringAsync();
            return content.Contains("<!DOCTYPE html>") || content.Contains("<html");
        }
        catch
        {
            return false;
        }
    }

    private async Task KillProcessOnPortAsync(int port)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Find and kill processes on the port
                var findPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netstat -ano | findstr :{port}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var findProcess = Process.Start(findPsi);
                if (findProcess != null)
                {
                    var output = await findProcess.StandardOutput.ReadToEndAsync();
                    await findProcess.WaitForExitAsync();

                    // Parse PIDs from netstat output and kill them
                    var killedPids = new HashSet<int>();
                    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 4 && int.TryParse(parts[^1], out var pid) && pid > 0 && !killedPids.Contains(pid))
                        {
                            try
                            {
                                var proc = Process.GetProcessById(pid);
                                proc.Kill(entireProcessTree: true);
                                killedPids.Add(pid);
                                Console.WriteLine($"Killed stale process on port {port}: PID {pid}");
                            }
                            catch { /* Process may have already exited */ }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not clean up port {port}: {ex.Message}");
        }
    }

    private async Task RunNpmInstallAsync()
    {
        if (!Directory.Exists(Path.Combine(_frontendPath, "node_modules")))
        {
            Console.WriteLine("Installing npm packages...");

            var psi = new ProcessStartInfo
            {
                FileName = GetNpmCommand(),
                Arguments = GetNpmArguments("install"),
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

    /// <summary>
    /// Gets the appropriate command to run npm on the current platform.
    /// On Windows, npm must be run via cmd.exe when UseShellExecute is false.
    /// </summary>
    private static string GetNpmCommand()
    {
        return OperatingSystem.IsWindows() ? "cmd.exe" : "npm";
    }

    /// <summary>
    /// Gets the arguments for running an npm command on the current platform.
    /// </summary>
    private static string GetNpmArguments(string npmCommand)
    {
        return OperatingSystem.IsWindows() ? $"/c npm {npmCommand}" : npmCommand;
    }

    private async Task StartFrontendAsync()
    {
        Console.WriteLine($"Starting frontend from: {_frontendPath}");

        var psi = new ProcessStartInfo
        {
            FileName = GetNpmCommand(),
            Arguments = GetNpmArguments("run dev"),
            WorkingDirectory = _frontendPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set environment to prevent browser opening
        psi.Environment["BROWSER"] = "none";

        _frontendProcess = Process.Start(psi);

        if (_frontendProcess == null)
        {
            throw new Exception("Failed to start frontend process");
        }

        // Start async output reading so the process doesn't hang on output buffer
        _ = Task.Run(async () =>
        {
            try
            {
                while (_frontendProcess != null && !_frontendProcess.HasExited)
                {
                    var line = await _frontendProcess.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        Console.WriteLine($"[FRONTEND] {line}");
                    }
                }
            }
            catch { /* Process ended */ }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                while (_frontendProcess != null && !_frontendProcess.HasExited)
                {
                    var line = await _frontendProcess.StandardError.ReadLineAsync();
                    if (line != null)
                    {
                        Console.WriteLine($"[FRONTEND ERROR] {line}");
                    }
                }
            }
            catch { /* Process ended */ }
        });

        await Task.CompletedTask;
    }

    private async Task WaitForFrontendAsync(int timeoutSeconds = 120)
    {
        Console.WriteLine("Waiting for frontend to be ready...");

        var stopwatch = Stopwatch.StartNew();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                var response = await client.GetAsync(FrontendUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (content.Contains("<!DOCTYPE html>") || content.Contains("<html"))
                    {
                        Console.WriteLine($"Frontend ready after {stopwatch.Elapsed.TotalSeconds:F1}s");
                        return;
                    }
                }
            }
            catch
            {
                // Server not ready yet
            }

            // Check if process died
            if (_frontendProcess != null && _frontendProcess.HasExited)
            {
                throw new Exception($"Frontend process exited unexpectedly with code {_frontendProcess.ExitCode}");
            }

            await Task.Delay(1000);
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
