using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.ExternalServices;

/// <summary>
/// Infrastructure implementation of the Hevy integration service.
/// This is the Anti-Corruption Layer that translates between domain concepts and Hevy API.
/// </summary>
public sealed class HevyIntegrationService : IHevyIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HevyIntegrationService> _logger;
    private readonly IA2SProgramProvider _programProvider;
    private const string HevyApiBaseUrl = "https://api.hevyapp.com/v1";

    // Day names for workout titles
    private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public HevyIntegrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<HevyIntegrationService> logger,
        IA2SProgramProvider programProvider)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _programProvider = programProvider ?? throw new ArgumentNullException(nameof(programProvider));
    }

    public async Task<HevyRoutineSyncResult> SyncRoutineForDayAsync(
        Workout workout,
        int weekNumber,
        int dayNumber,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var routineTitle = $"{workout.Name} - Week {weekNumber} Day {dayNumber}";

            // Check if routine already exists
            var existingRoutine = await FindRoutineByTitleAsync(routineTitle, apiKey, ct);
            if (existingRoutine != null)
            {
                _logger.LogInformation("Routine already exists: {Title}", routineTitle);
                return HevyRoutineSyncResult.Succeeded(existingRoutine.Id, routineTitle, alreadyExists: true);
            }

            // Get exercises for this day
            var dayExercises = workout.Exercises
                .Where(e => (int)e.AssignedDay == dayNumber)
                .OrderBy(e => e.OrderInDay)
                .ToList();

            if (dayExercises.Count == 0)
            {
                return HevyRoutineSyncResult.Failed($"No exercises found for Day {dayNumber}");
            }

            // Get week parameters from program provider
            var weekParams = _programProvider.GetWeekParameters(weekNumber);
            var blockNumber = weekParams.BlockNumber;

            // Build routine exercises
            var routineExercises = new List<HevyRoutineExerciseDto>();
            foreach (var exercise in dayExercises)
            {
                var plannedSets = exercise.CalculatePlannedSets(weekNumber, blockNumber).ToList();
                var routineExercise = ConvertToRoutineExercise(exercise, plannedSets, weekParams);
                routineExercises.Add(routineExercise);
            }

            // Build routine notes
            var routineNotes = weekParams.IsDeload
                ? $"Block {blockNumber} | DELOAD WEEK | Intensity: {weekParams.IntensityPercentage:0}%"
                : $"Block {blockNumber} | Intensity: {weekParams.IntensityPercentage:0}%";

            // Get or create folder
            string? folderId = workout.HevyRoutineFolderId;
            if (string.IsNullOrEmpty(folderId))
            {
                folderId = await GetOrCreateRoutineFolderAsync(workout.Name, apiKey, ct);
            }

            // Create routine request
            var request = new HevyCreateRoutineRequest
            {
                Routine = new HevyRoutineDto
                {
                    Title = routineTitle,
                    FolderId = folderId != null ? int.Parse(folderId) : null,
                    Notes = routineNotes,
                    Exercises = routineExercises
                }
            };

            // Create routine
            var routine = await CreateRoutineAsync(request, apiKey, ct);
            if (routine == null)
            {
                return HevyRoutineSyncResult.Failed("Failed to create routine in Hevy");
            }

            _logger.LogInformation("Created routine: {Title} with ID {Id}", routineTitle, routine.Id);
            return HevyRoutineSyncResult.Succeeded(routine.Id, routineTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync routine for workout {WorkoutId}, week {Week}, day {Day}",
                workout.Id.Value, weekNumber, dayNumber);
            return HevyRoutineSyncResult.Failed(ex.Message);
        }
    }

    public async Task<HevyWorkoutSyncResult> SyncCompletedWorkoutAsync(
        Workout workout,
        int dayNumber,
        IReadOnlyList<ExercisePerformance> performances,
        DateTime startTime,
        DateTime endTime,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var dayName = dayNumber >= 1 && dayNumber <= 6 ? DayNames[dayNumber - 1] : $"Day {dayNumber}";
            var workoutTitle = $"{workout.Name} - Week {workout.CurrentWeek} / Day {dayNumber} ({dayName})";

            // Build workout exercises from performances
            var workoutExercises = new List<HevyWorkoutExerciseDto>();
            foreach (var performance in performances)
            {
                var exercise = workout.Exercises.FirstOrDefault(e => e.Id == performance.ExerciseId);
                if (exercise == null) continue;

                var workoutExercise = ConvertToWorkoutExercise(exercise, performance, workout.CurrentWeek);
                if (workoutExercise.Sets.Count > 0)
                {
                    workoutExercises.Add(workoutExercise);
                }
            }

            if (workoutExercises.Count == 0)
            {
                return HevyWorkoutSyncResult.Failed("No completed exercises to sync");
            }

            // Create workout request
            var request = new HevyCreateWorkoutRequest
            {
                Workout = new HevyWorkoutDto
                {
                    Title = workoutTitle,
                    Description = $"Block {workout.CurrentBlock} - Auto-synced from A2S Tracker",
                    StartTime = startTime.ToUniversalTime().ToString("O"),
                    EndTime = endTime.ToUniversalTime().ToString("O"),
                    IsPrivate = false,
                    Exercises = workoutExercises
                }
            };

            // Create workout
            var createdWorkout = await CreateWorkoutAsync(request, apiKey, ct);
            if (createdWorkout == null)
            {
                return HevyWorkoutSyncResult.Failed("Failed to create workout in Hevy");
            }

            _logger.LogInformation("Created workout: {Title} with ID {Id}", workoutTitle, createdWorkout.Id);
            return HevyWorkoutSyncResult.Succeeded(createdWorkout.Id, workoutTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync completed workout for {WorkoutId}, day {Day}",
                workout.Id.Value, dayNumber);
            return HevyWorkoutSyncResult.Failed(ex.Message);
        }
    }

    public async Task<string?> GetOrCreateRoutineFolderAsync(
        string programName,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            // Get all folders
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

            // Find existing folder
            var existingFolder = allFolders.FirstOrDefault(
                f => f.Title.Equals(programName, StringComparison.OrdinalIgnoreCase));

            if (existingFolder != null)
            {
                _logger.LogInformation("Found existing folder: {Title} with ID {Id}", existingFolder.Title, existingFolder.Id);
                return existingFolder.Id.ToString();
            }

            // Create new folder
            var newFolder = await CreateRoutineFolderAsync(programName, apiKey, ct);
            if (newFolder != null)
            {
                _logger.LogInformation("Created folder: {Title} with ID {Id}", programName, newFolder.Id);
                return newFolder.Id.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create routine folder: {ProgramName}", programName);
            return null;
        }
    }

    public async Task<bool> DeleteRoutineAsync(string routineId, string apiKey, CancellationToken ct = default)
    {
        try
        {
            var client = CreateClient(apiKey);
            var response = await client.DeleteAsync($"{HevyApiBaseUrl}/routines/{Uri.EscapeDataString(routineId)}", ct);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Deleted routine: {RoutineId}", routineId);
                return true;
            }

            _logger.LogWarning("Failed to delete routine {RoutineId}: {StatusCode}", routineId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete routine: {RoutineId}", routineId);
            return false;
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            var client = CreateClient(apiKey);
            var response = await client.GetAsync($"{HevyApiBaseUrl}/workouts/count", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Hevy API key");
            return false;
        }
    }

    public async Task<HevyPulledWorkoutData?> PullWorkoutDataAsync(
        Workout workout,
        int weekNumber,
        int dayNumber,
        string apiKey,
        CancellationToken ct = default)
    {
        try
        {
            var dayName = dayNumber >= 1 && dayNumber <= 6 ? DayNames[dayNumber - 1] : $"Day {dayNumber}";

            // Possible title patterns
            var possibleTitles = new[]
            {
                $"{workout.Name} - Week {weekNumber} / Day {dayNumber} ({dayName})",
                $"{workout.Name} - Week {weekNumber} Day {dayNumber}",
                $"Week {weekNumber} Day {dayNumber}"
            };

            // Search recent workouts
            var allWorkouts = new List<HevyWorkoutResponse>();
            for (var page = 1; page <= 5; page++)
            {
                var response = await GetWorkoutsAsync(page, 10, apiKey, ct);
                if (response?.Workouts != null)
                {
                    allWorkouts.AddRange(response.Workouts);
                    if (page >= response.PageCount) break;
                }
            }

            // Find matching workout
            HevyWorkoutResponse? matchedWorkout = null;
            foreach (var title in possibleTitles)
            {
                matchedWorkout = allWorkouts.FirstOrDefault(
                    w => w.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                if (matchedWorkout != null) break;
            }

            // Fallback: partial match
            if (matchedWorkout == null)
            {
                var weekPattern = $"week {weekNumber}";
                var dayPattern = $"day {dayNumber}";
                matchedWorkout = allWorkouts.FirstOrDefault(w =>
                {
                    var lowerTitle = w.Title.ToLowerInvariant();
                    return lowerTitle.Contains(weekPattern) &&
                           (lowerTitle.Contains(dayPattern) || lowerTitle.Contains(dayName.ToLowerInvariant()));
                });
            }

            if (matchedWorkout == null)
            {
                _logger.LogInformation("No workout found for Week {Week} Day {Day}", weekNumber, dayNumber);
                return null;
            }

            // Convert to domain model
            var exercises = matchedWorkout.Exercises.Select(e => new HevyPulledExerciseData
            {
                HevyTemplateId = e.ExerciseTemplateId,
                ExerciseName = e.ExerciseTemplateId, // Would need template lookup for actual name
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
                StartTime = DateTime.Parse(matchedWorkout.StartTime),
                EndTime = DateTime.Parse(matchedWorkout.EndTime),
                Exercises = exercises
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull workout data for Week {Week} Day {Day}", weekNumber, dayNumber);
            return null;
        }
    }

    #region Private Helper Methods

    private HttpClient CreateClient(string apiKey)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("api-key", apiKey);
        return client;
    }

    private static HevyRoutineExerciseDto ConvertToRoutineExercise(
        Exercise exercise,
        IReadOnlyList<PlannedSet> plannedSets,
        WeekParameters weekParams)
    {
        var sets = new List<HevyRoutineSetDto>();
        var notes = "";

        if (exercise.Progression is LinearProgressionStrategy linear)
        {
            foreach (var plannedSet in plannedSets)
            {
                sets.Add(new HevyRoutineSetDto
                {
                    Type = plannedSet.IsAmrap ? "failure" : "normal",
                    WeightKg = RoundToGymIncrement(plannedSet.Weight.ConvertTo(WeightUnit.Kilograms).Value, "kg"),
                    Reps = plannedSet.TargetReps
                });
            }

            var blockPhase = weekParams.BlockNumber switch
            {
                1 => "Volume phase",
                2 => "Intensity phase",
                3 => "Peaking phase",
                _ => ""
            };

            var noteParts = new List<string>();
            noteParts.Add($"TM: {linear.TrainingMax}");
            if (weekParams.IsDeload)
            {
                noteParts.Add("DELOAD");
            }
            else
            {
                noteParts.Add($"Block {weekParams.BlockNumber} - {blockPhase}");
            }
            noteParts.Add($"{weekParams.IntensityPercentage:0}% × {weekParams.Sets} sets × {weekParams.TargetReps} reps");
            if (linear.UseAmrap)
            {
                noteParts.Add($"AMRAP last set (target: {plannedSets.LastOrDefault()?.TargetReps ?? 0}+)");
            }
            notes = string.Join(" | ", noteParts);
        }
        else if (exercise.Progression is RepsPerSetStrategy reps)
        {
            foreach (var plannedSet in plannedSets)
            {
                sets.Add(new HevyRoutineSetDto
                {
                    Type = "normal",
                    WeightKg = RoundToGymIncrement(plannedSet.Weight.ConvertTo(WeightUnit.Kilograms).Value, "kg"),
                    Reps = plannedSet.TargetReps
                });
            }

            var effectiveMaxSets = Math.Min(reps.TargetSets, reps.MaxSets);
            var incrementDesc = reps.Equipment switch
            {
                EquipmentType.Dumbbell => reps.CurrentWeight.Value < 10 ? "+1kg" : "+2kg",
                EquipmentType.Bodyweight => "bodyweight",
                _ => reps.CurrentWeight.Unit == WeightUnit.Kilograms ? "+2.5kg" : "+5lbs"
            };

            var noteParts = new List<string>();
            noteParts.Add($"Sets: {reps.CurrentSetCount}/{effectiveMaxSets}");
            noteParts.Add($"Rep range: {reps.RepRange.Minimum}-{reps.RepRange.Maximum} (hit {reps.RepRange.Maximum} to progress)");
            if (reps.Equipment != EquipmentType.Bodyweight)
            {
                noteParts.Add($"Equipment: {reps.Equipment} ({incrementDesc})");
            }
            else
            {
                noteParts.Add("Bodyweight");
            }
            if (reps.IsUnilateral)
            {
                noteParts.Add("Unilateral");
            }
            notes = string.Join(" | ", noteParts);
        }
        else if (exercise.Progression is MinimalSetsStrategy minimal)
        {
            foreach (var plannedSet in plannedSets)
            {
                sets.Add(new HevyRoutineSetDto
                {
                    Type = "normal",
                    WeightKg = RoundToGymIncrement(plannedSet.Weight.ConvertTo(WeightUnit.Kilograms).Value, "kg"),
                    Reps = plannedSet.TargetReps
                });
            }

            notes = $"Target: {minimal.TargetTotalReps} reps in {minimal.CurrentSetCount} sets (range: {minimal.MinimumSets}-{minimal.MaximumSets}) | Complete in fewer sets to progress";
        }

        return new HevyRoutineExerciseDto
        {
            ExerciseTemplateId = exercise.HevyExerciseTemplateId,
            SupersetId = null,
            RestSeconds = 120,
            Notes = notes,
            Sets = sets
        };
    }

    private HevyWorkoutExerciseDto ConvertToWorkoutExercise(
        Exercise exercise,
        ExercisePerformance performance,
        int weekNumber)
    {
        var sets = new List<HevyWorkoutSetDto>();

        foreach (var completedSet in performance.CompletedSets)
        {
            if (completedSet.ActualReps <= 0) continue;

            sets.Add(new HevyWorkoutSetDto
            {
                Type = completedSet.WasAmrap ? "failure" : "normal",
                WeightKg = RoundToGymIncrement(completedSet.Weight.ConvertTo(WeightUnit.Kilograms).Value, "kg"),
                Reps = completedSet.ActualReps
            });
        }

        // Build notes
        string? notes = null;
        if (exercise.Progression is LinearProgressionStrategy linear && linear.UseAmrap)
        {
            var targetReps = performance.PlannedSets.LastOrDefault()?.TargetReps ?? 0;
            notes = $"AMRAP target: {targetReps} reps";
        }

        return new HevyWorkoutExerciseDto
        {
            ExerciseTemplateId = exercise.HevyExerciseTemplateId,
            SupersetId = null,
            Notes = notes,
            Sets = sets
        };
    }

    private static decimal RoundToGymIncrement(decimal weight, string unit)
    {
        var increment = unit == "lbs" ? 5m : 2.5m;
        return Math.Round(weight / increment) * increment;
    }

    private async Task<HevyRoutineResponse?> FindRoutineByTitleAsync(
        string title,
        string apiKey,
        CancellationToken ct)
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
        int page,
        int pageSize,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var response = await client.GetAsync(
            $"{HevyApiBaseUrl}/routines?page={page}&pageSize={Math.Min(pageSize, 10)}",
            ct);

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HevyRoutinesResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineResponse?> CreateRoutineAsync(
        HevyCreateRoutineRequest request,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var response = await client.PostAsJsonAsync($"{HevyApiBaseUrl}/routines", request, ct);

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
        HevyCreateWorkoutRequest request,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var response = await client.PostAsJsonAsync($"{HevyApiBaseUrl}/workouts", request, ct);

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
        int page,
        int pageSize,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var response = await client.GetAsync(
            $"{HevyApiBaseUrl}/workouts?page={page}&pageSize={pageSize}",
            ct);

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HevyWorkoutsResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineFoldersResponse?> GetRoutineFoldersAsync(
        int page,
        int pageSize,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var response = await client.GetAsync(
            $"{HevyApiBaseUrl}/routine_folders?page={page}&pageSize={Math.Min(pageSize, 10)}",
            ct);

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HevyRoutineFoldersResponse>(cancellationToken: ct);
    }

    private async Task<HevyRoutineFolderResponse?> CreateRoutineFolderAsync(
        string title,
        string apiKey,
        CancellationToken ct)
    {
        var client = CreateClient(apiKey);
        var request = new { routine_folder = new { title } };
        var response = await client.PostAsJsonAsync($"{HevyApiBaseUrl}/routine_folders", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Failed to create folder: {StatusCode} - {Content}", response.StatusCode, content);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<HevyRoutineFolderWrapper>(cancellationToken: ct);
        return result?.RoutineFolder;
    }

    #endregion

    #region Hevy API DTOs (Internal)

    private sealed record HevyCreateRoutineRequest
    {
        [JsonPropertyName("routine")]
        public HevyRoutineDto Routine { get; init; } = null!;
    }

    private sealed record HevyRoutineDto
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("folder_id")]
        public int? FolderId { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("exercises")]
        public List<HevyRoutineExerciseDto> Exercises { get; init; } = new();
    }

    private sealed record HevyRoutineExerciseDto
    {
        [JsonPropertyName("exercise_template_id")]
        public string ExerciseTemplateId { get; init; } = string.Empty;

        [JsonPropertyName("superset_id")]
        public int? SupersetId { get; init; }

        [JsonPropertyName("rest_seconds")]
        public int? RestSeconds { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("sets")]
        public List<HevyRoutineSetDto> Sets { get; init; } = new();
    }

    private sealed record HevyRoutineSetDto
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "normal";

        [JsonPropertyName("weight_kg")]
        public decimal? WeightKg { get; init; }

        [JsonPropertyName("reps")]
        public int? Reps { get; init; }
    }

    private sealed record HevyCreateWorkoutRequest
    {
        [JsonPropertyName("workout")]
        public HevyWorkoutDto Workout { get; init; } = null!;
    }

    private sealed record HevyWorkoutDto
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; init; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string EndTime { get; init; } = string.Empty;

        [JsonPropertyName("is_private")]
        public bool IsPrivate { get; init; }

        [JsonPropertyName("exercises")]
        public List<HevyWorkoutExerciseDto> Exercises { get; init; } = new();
    }

    private sealed record HevyWorkoutExerciseDto
    {
        [JsonPropertyName("exercise_template_id")]
        public string ExerciseTemplateId { get; init; } = string.Empty;

        [JsonPropertyName("superset_id")]
        public int? SupersetId { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }

        [JsonPropertyName("sets")]
        public List<HevyWorkoutSetDto> Sets { get; init; } = new();
    }

    private sealed record HevyWorkoutSetDto
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "normal";

        [JsonPropertyName("weight_kg")]
        public decimal? WeightKg { get; init; }

        [JsonPropertyName("reps")]
        public int? Reps { get; init; }
    }

    private sealed record HevyRoutinesResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("page_count")]
        public int PageCount { get; init; }

        [JsonPropertyName("routines")]
        public List<HevyRoutineResponse> Routines { get; init; } = new();
    }

    private sealed record HevyRoutineResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;
    }

    private sealed record HevyRoutineWrapper
    {
        [JsonPropertyName("routine")]
        public HevyRoutineResponse Routine { get; init; } = null!;
    }

    private sealed record HevyWorkoutsResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("page_count")]
        public int PageCount { get; init; }

        [JsonPropertyName("workouts")]
        public List<HevyWorkoutResponse> Workouts { get; init; } = new();
    }

    private sealed record HevyWorkoutResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("start_time")]
        public string StartTime { get; init; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string EndTime { get; init; } = string.Empty;

        [JsonPropertyName("exercises")]
        public List<HevyWorkoutExerciseResponse> Exercises { get; init; } = new();
    }

    private sealed record HevyWorkoutExerciseResponse
    {
        [JsonPropertyName("exercise_template_id")]
        public string ExerciseTemplateId { get; init; } = string.Empty;

        [JsonPropertyName("sets")]
        public List<HevyWorkoutSetResponse> Sets { get; init; } = new();
    }

    private sealed record HevyWorkoutSetResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "normal";

        [JsonPropertyName("weight_kg")]
        public decimal? WeightKg { get; init; }

        [JsonPropertyName("reps")]
        public int? Reps { get; init; }
    }

    private sealed record HevyWorkoutWrapper
    {
        [JsonPropertyName("workout")]
        public HevyWorkoutResponse Workout { get; init; } = null!;
    }

    private sealed record HevyRoutineFoldersResponse
    {
        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("page_count")]
        public int PageCount { get; init; }

        [JsonPropertyName("routine_folders")]
        public List<HevyRoutineFolderResponse> RoutineFolders { get; init; } = new();
    }

    private sealed record HevyRoutineFolderResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;
    }

    private sealed record HevyRoutineFolderWrapper
    {
        [JsonPropertyName("routine_folder")]
        public HevyRoutineFolderResponse RoutineFolder { get; init; } = null!;
    }

    #endregion
}
