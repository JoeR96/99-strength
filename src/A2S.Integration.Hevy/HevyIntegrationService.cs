using System.Globalization;
using System.Net.Http.Json;
using A2S.Application.Interfaces;
using A2S.Integration.Hevy.Configuration;
using A2S.Integration.Hevy.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace A2S.Integration.Hevy;

/// <summary>
/// Anti-Corruption Layer implementation for Hevy API integration.
/// Receives application-layer DTOs — no domain entity references.
/// </summary>
public sealed class HevyIntegrationService : IHevyIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HevyIntegrationService> _logger;
    private readonly HevyOptions _options;

    internal const string HttpClientName = "HevyApi";

    private static readonly string[] DayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    public HevyIntegrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<HevyIntegrationService> logger,
        IOptions<HevyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<HevyRoutineSyncResult> SyncRoutineForDayAsync(
        HevySyncRoutineRequest request,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var routineTitle = $"{request.WorkoutName} - Week {request.WeekNumber} Day {request.DayNumber}";

            var existingRoutine = await FindRoutineByTitleAsync(routineTitle, apiKey, ct);
            if (existingRoutine != null)
            {
                _logger.LogInformation("Routine already exists: {Title}", routineTitle);
                return HevyRoutineSyncResult.Succeeded(existingRoutine.Id, routineTitle, alreadyExists: true);
            }

            if (request.Exercises.Count == 0)
            {
                return HevyRoutineSyncResult.Failed($"No exercises found for Day {request.DayNumber}");
            }

            var routineExercises = request.Exercises.Select(e => new HevyRoutineExerciseDto
            {
                ExerciseTemplateId = e.ExternalTemplateId,
                SupersetId = null,
                RestSeconds = 120,
                Notes = e.Notes,
                Sets = e.PlannedSets.Select(s => new HevyRoutineSetDto
                {
                    Type = s.IsAmrap ? "failure" : "normal",
                    WeightKg = RoundToGymIncrement(s.WeightKg),
                    Reps = s.TargetReps
                }).ToList()
            }).ToList();

            var routineNotes = request.IsDeload
                ? $"Block {request.BlockNumber} | DELOAD WEEK | Intensity: {request.IntensityPercentage:0}%"
                : $"Block {request.BlockNumber} | Intensity: {request.IntensityPercentage:0}%";

            string? folderId = await GetOrCreateRoutineFolderAsync(request.WorkoutName, apiKey, ct);

            var createRequest = new HevyCreateRoutineRequest
            {
                Routine = new HevyRoutineDto
                {
                    Title = routineTitle,
                    FolderId = folderId != null ? int.Parse(folderId) : null,
                    Notes = routineNotes,
                    Exercises = routineExercises
                }
            };

            var routine = await CreateRoutineAsync(createRequest, apiKey, ct);
            if (routine == null)
            {
                return HevyRoutineSyncResult.Failed("Failed to create routine in Hevy");
            }

            _logger.LogInformation("Created routine: {Title} with ID {Id}", routineTitle, routine.Id);
            return HevyRoutineSyncResult.Succeeded(routine.Id, routineTitle);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error syncing routine for {WorkoutName}, week {Week}, day {Day}",
                request.WorkoutName, request.WeekNumber, request.DayNumber);
            return HevyRoutineSyncResult.Failed($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout syncing routine for {WorkoutName}, week {Week}, day {Day}",
                request.WorkoutName, request.WeekNumber, request.DayNumber);
            return HevyRoutineSyncResult.Failed("Request timed out");
        }
    }

    public async Task<HevyWorkoutSyncResult> SyncCompletedWorkoutAsync(
        HevySyncWorkoutRequest request,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var dayName = request.DayNumber >= 1 && request.DayNumber <= 6
                ? DayNames[request.DayNumber - 1]
                : $"Day {request.DayNumber}";
            var workoutTitle = $"{request.WorkoutName} - Week {request.WeekNumber} / Day {request.DayNumber} ({dayName})";

            var workoutExercises = request.Exercises
                .Where(e => e.CompletedSets.Count > 0)
                .Select(e => new HevyWorkoutExerciseDto
                {
                    ExerciseTemplateId = e.ExternalTemplateId,
                    SupersetId = null,
                    Notes = e.Notes,
                    Sets = e.CompletedSets
                        .Where(s => s.Reps > 0)
                        .Select(s => new HevyWorkoutSetDto
                        {
                            Type = s.WasAmrap ? "failure" : "normal",
                            WeightKg = RoundToGymIncrement(s.WeightKg),
                            Reps = s.Reps
                        }).ToList()
                })
                .Where(e => e.Sets.Count > 0)
                .ToList();

            if (workoutExercises.Count == 0)
            {
                return HevyWorkoutSyncResult.Failed("No completed exercises to sync");
            }

            var createRequest = new HevyCreateWorkoutRequest
            {
                Workout = new HevyWorkoutDto
                {
                    Title = workoutTitle,
                    Description = $"Block {request.BlockNumber} - Auto-synced from A2S Tracker",
                    StartTime = request.StartTime.ToUniversalTime().ToString("O"),
                    EndTime = request.EndTime.ToUniversalTime().ToString("O"),
                    IsPrivate = false,
                    Exercises = workoutExercises
                }
            };

            var createdWorkout = await CreateWorkoutAsync(createRequest, apiKey, ct);
            if (createdWorkout == null)
            {
                return HevyWorkoutSyncResult.Failed("Failed to create workout in Hevy");
            }

            _logger.LogInformation("Created workout: {Title} with ID {Id}", workoutTitle, createdWorkout.Id);
            return HevyWorkoutSyncResult.Succeeded(createdWorkout.Id, workoutTitle);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error syncing completed workout for {WorkoutName}, day {Day}",
                request.WorkoutName, request.DayNumber);
            return HevyWorkoutSyncResult.Failed($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout syncing completed workout for {WorkoutName}, day {Day}",
                request.WorkoutName, request.DayNumber);
            return HevyWorkoutSyncResult.Failed("Request timed out");
        }
    }

    public async Task<string?> GetOrCreateRoutineFolderAsync(
        string programName,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var allFolders = new List<HevyRoutineFolderResponse>();
            var page = 1;
            var hasMore = true;

            while (hasMore)
            {
                var response = await GetRoutineFoldersAsync(page, 10, apiKey, ct);
                if (response?.RoutineFolders != null)
                {
                    allFolders.AddRange(response.RoutineFolders);
                    hasMore = page < response.PageCount;
                    page++;
                }
                else
                {
                    hasMore = false;
                }
            }

            var existingFolder = allFolders.FirstOrDefault(
                f => f.Title.Equals(programName, StringComparison.OrdinalIgnoreCase));

            if (existingFolder != null)
            {
                _logger.LogInformation("Found existing folder: {Title} with ID {Id}", existingFolder.Title, existingFolder.Id);
                return existingFolder.Id.ToString();
            }

            var newFolder = await CreateRoutineFolderAsync(programName, apiKey, ct);
            if (newFolder != null)
            {
                _logger.LogInformation("Created folder: {Title} with ID {Id}", programName, newFolder.Id);
                return newFolder.Id.ToString();
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting/creating routine folder: {ProgramName}", programName);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout getting/creating routine folder: {ProgramName}", programName);
            return null;
        }
    }

    public async Task<bool> DeleteRoutineAsync(string routineId, string apiKey, CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"routines/{Uri.EscapeDataString(routineId)}", apiKey);
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Deleted routine: {RoutineId}", routineId);
                return true;
            }

            _logger.LogWarning("Failed to delete routine {RoutineId}: {StatusCode}", routineId, response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error deleting routine: {RoutineId}", routineId);
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout deleting routine: {RoutineId}", routineId);
            return false;
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "workouts/count", apiKey);
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<HevyPulledWorkoutData?> PullWorkoutDataAsync(
        HevyPullRequest request,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var dayName = request.DayNumber >= 1 && request.DayNumber <= 6
                ? DayNames[request.DayNumber - 1]
                : $"Day {request.DayNumber}";

            var possibleTitles = new[]
            {
                $"{request.WorkoutName} - Week {request.WeekNumber} / Day {request.DayNumber} ({dayName})",
                $"{request.WorkoutName} - Week {request.WeekNumber} Day {request.DayNumber}",
                $"Week {request.WeekNumber} Day {request.DayNumber}"
            };

            var allWorkouts = new List<HevyWorkoutResponse>();
            for (var page = 1; page <= 5; page++)
            {
                var response = await GetWorkoutsAsync(page, 10, apiKey, ct);
                if (response?.Workouts != null)
                {
                    allWorkouts.AddRange(response.Workouts);
                    if (page >= response.PageCount)
                    {
                        break;
                    }
                }
            }

            HevyWorkoutResponse? matchedWorkout = null;
            foreach (var title in possibleTitles)
            {
                matchedWorkout = allWorkouts.FirstOrDefault(
                    w => w.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                if (matchedWorkout != null)
                {
                    break;
                }
            }

            if (matchedWorkout == null)
            {
                var weekPattern = $"week {request.WeekNumber}";
                var dayPattern = $"day {request.DayNumber}";
                matchedWorkout = allWorkouts.FirstOrDefault(w =>
                {
                    var lowerTitle = w.Title.ToLowerInvariant();
                    return lowerTitle.Contains(weekPattern) &&
                           (lowerTitle.Contains(dayPattern) || lowerTitle.Contains(dayName.ToLowerInvariant()));
                });
            }

            if (matchedWorkout == null)
            {
                _logger.LogInformation("No workout found for Week {Week} Day {Day}", request.WeekNumber, request.DayNumber);
                return null;
            }

            var exercises = matchedWorkout.Exercises.Select(e => new HevyPulledExerciseData
            {
                HevyTemplateId = e.ExerciseTemplateId,
                ExerciseName = e.ExerciseTemplateId,
                Sets = e.Sets.Select((s, i) => new HevyPulledSetData
                {
                    SetNumber = i + 1,
                    WeightKg = s.WeightKg ?? 0,
                    Reps = s.Reps ?? 0,
                    WasFailureSet = s.Type == "failure"
                }).ToList()
            }).ToList();

            return new HevyPulledWorkoutData
            {
                WorkoutId = matchedWorkout.Id,
                WorkoutTitle = matchedWorkout.Title,
                StartTime = DateTime.Parse(matchedWorkout.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EndTime = DateTime.Parse(matchedWorkout.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Exercises = exercises
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error pulling workout data for Week {Week} Day {Day}",
                request.WeekNumber, request.DayNumber);
            return null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout pulling workout data for Week {Week} Day {Day}",
                request.WeekNumber, request.DayNumber);
            return null;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string apiKey)
    {
        var request = new HttpRequestMessage(method, $"{_options.BaseUrl}/{relativePath}");
        request.Headers.Add("api-key", apiKey);
        return request;
    }

    private async Task<HevyRoutineResponse?> FindRoutineByTitleAsync(
        string title, string apiKey, CancellationToken ct)
    {
        var allRoutines = new List<HevyRoutineResponse>();
        var page = 1;
        var hasMore = true;

        while (hasMore)
        {
            var response = await GetRoutinesAsync(page, 10, apiKey, ct);
            if (response?.Routines != null)
            {
                allRoutines.AddRange(response.Routines);
                hasMore = page < response.PageCount;
                page++;
            }
            else
            {
                hasMore = false;
            }
        }

        return allRoutines.FirstOrDefault(
            r => r.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<HevyRoutinesResponse?> GetRoutinesAsync(
        int page, int pageSize, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"routines?page={page}&pageSize={Math.Min(pageSize, 10)}", apiKey);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<HevyRoutinesResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineResponse?> CreateRoutineAsync(
        HevyCreateRoutineRequest createRequest, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, "routines", apiKey);
        request.Content = JsonContent.Create(createRequest);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Failed to create routine: {StatusCode} - {Content}", response.StatusCode, content);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<HevyRoutineWrapper>(cancellationToken: ct);
        return result?.Routine;
    }

    private async Task<HevyWorkoutResponse?> CreateWorkoutAsync(
        HevyCreateWorkoutRequest createRequest, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, "workouts", apiKey);
        request.Content = JsonContent.Create(createRequest);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Failed to create workout: {StatusCode} - {Content}", response.StatusCode, content);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<HevyWorkoutWrapper>(cancellationToken: ct);
        return result?.Workout;
    }

    private async Task<HevyWorkoutsResponse?> GetWorkoutsAsync(
        int page, int pageSize, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"workouts?page={page}&pageSize={pageSize}", apiKey);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<HevyWorkoutsResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineFoldersResponse?> GetRoutineFoldersAsync(
        int page, int pageSize, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"routine_folders?page={page}&pageSize={Math.Min(pageSize, 10)}", apiKey);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<HevyRoutineFoldersResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineFolderResponse?> CreateRoutineFolderAsync(
        string title, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, "routine_folders", apiKey);
        request.Content = JsonContent.Create(new { routine_folder = new { title } });
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Failed to create folder: {StatusCode} - {Content}", response.StatusCode, content);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<HevyRoutineFolderWrapper>(cancellationToken: ct);
        return result?.RoutineFolder;
    }

    private static decimal RoundToGymIncrement(decimal weight)
    {
        const decimal increment = 2.5m;
        return Math.Round(weight / increment) * increment;
    }
}
