using System.ComponentModel.DataAnnotations;
using A2S.Api.Contracts.Responses;
using A2S.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2S.Api.Controllers;

/// <summary>
/// Aggregated Hevy workout data endpoints for charting and analysis.
/// Fetches raw data via the Hevy API proxy and transforms it into chart-ready DTOs.
/// </summary>
[ApiController]
[Route("api/v1/hevy/data")]
[Authorize]
public class HevyDataController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HevyDataController> _logger;
    private readonly ICurrentUserService _currentUser;
    private const string HevyApiBaseUrl = "https://api.hevyapp.com/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public HevyDataController(
        IHttpClientFactory httpClientFactory,
        ILogger<HevyDataController> logger,
        ICurrentUserService currentUser)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get all Hevy workouts with exercise summaries, suitable for listing.
    /// </summary>
    [HttpGet("workouts")]
    [ProducesResponseType(typeof(HevyWorkoutListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkouts(
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        [FromQuery] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        apiKey ??= _currentUser.HevyApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        var client = _httpClientFactory.CreateClient("HevyApi");
        var allWorkouts = new List<HevyRawWorkout>();
        var currentPage = page;
        var totalPages = 1;

        // Fetch requested page(s) of workouts
        var url = $"{HevyApiBaseUrl}/workouts?page={currentPage}&page_size={pageSize}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("api-key", apiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, new ProblemDetails
            {
                Status = (int)response.StatusCode,
                Title = "Hevy API Error",
                Detail = errorContent
            });
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<HevyWorkoutsRawResponse>(content, JsonOptions);

        if (parsed?.Workouts != null)
        {
            allWorkouts.AddRange(parsed.Workouts);
            totalPages = parsed.PageCount;
        }

        var workoutSummaries = allWorkouts.Select(w => new HevyWorkoutSummary
        {
            Id = w.Id,
            Title = w.Title,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            ExerciseCount = w.Exercises?.Count ?? 0,
            Exercises = w.Exercises?.Select(e => new HevyExerciseSummary
            {
                ExerciseTemplateId = e.ExerciseTemplateId,
                Title = e.Title ?? e.ExerciseTemplateId,
                SetCount = e.Sets?.Count ?? 0,
                BestWeight = e.Sets?.Max(s => s.WeightKg) ?? 0,
                BestReps = e.Sets?.Max(s => s.Reps) ?? 0,
                TotalVolume = e.Sets?.Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0)) ?? 0
            }).ToList() ?? []
        }).ToList();

        return Ok(new HevyWorkoutListResponse
        {
            Workouts = workoutSummaries,
            Page = currentPage,
            PageCount = totalPages
        });
    }

    /// <summary>
    /// Get time-series history for a specific exercise across all Hevy workouts.
    /// Aggregates weight, reps, sets, and volume data for charting.
    /// </summary>
    [HttpGet("exercises/{exerciseTemplateId}/history")]
    [ProducesResponseType(typeof(HevyExerciseHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExerciseHistory(
        [FromRoute] string exerciseTemplateId,
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        [FromQuery][Range(1, 20)] int maxPages = 10,
        CancellationToken cancellationToken = default)
    {
        apiKey ??= _currentUser.HevyApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        if (string.IsNullOrWhiteSpace(exerciseTemplateId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "Exercise template ID is required."
            });
        }

        var client = _httpClientFactory.CreateClient("HevyApi");
        var sessions = new List<ExerciseSessionPoint>();
        var currentPage = 1;
        var hasMore = true;

        // Paginate through all workouts to find exercise occurrences
        while (hasMore && currentPage <= maxPages)
        {
            var url = $"{HevyApiBaseUrl}/workouts?page={currentPage}&page_size=10";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("api-key", apiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<HevyWorkoutsRawResponse>(content, JsonOptions);

            if (parsed?.Workouts == null || parsed.Workouts.Count == 0)
            {
                break;
            }

            foreach (var workout in parsed.Workouts)
            {
                var matchingExercises = workout.Exercises?
                    .Where(e => e.ExerciseTemplateId == exerciseTemplateId)
                    .ToList() ?? [];

                foreach (var exercise in matchingExercises)
                {
                    var workingSets = exercise.Sets?
                        .Where(s => s.Type is "normal" or "failure")
                        .ToList() ?? [];

                    if (workingSets.Count == 0)
                    {
                        continue;
                    }

                    sessions.Add(new ExerciseSessionPoint
                    {
                        Date = workout.StartTime,
                        WorkoutTitle = workout.Title,
                        Sets = workingSets.Count,
                        MaxWeight = workingSets.Max(s => s.WeightKg ?? 0),
                        MaxReps = workingSets.Max(s => s.Reps ?? 0),
                        TotalVolume = workingSets.Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0)),
                        AvgWeight = workingSets.Average(s => (double)(s.WeightKg ?? 0)),
                        AvgReps = workingSets.Average(s => (double)(s.Reps ?? 0)),
                        SetDetails = workingSets.Select(s => new SetDetail
                        {
                            WeightKg = s.WeightKg ?? 0,
                            Reps = s.Reps ?? 0,
                            Type = s.Type
                        }).ToList()
                    });
                }
            }

            hasMore = currentPage < parsed.PageCount;
            currentPage++;
        }

        sessions = sessions.OrderBy(s => s.Date).ToList();

        return Ok(new HevyExerciseHistoryResponse
        {
            ExerciseTemplateId = exerciseTemplateId,
            Sessions = sessions,
            TotalSessions = sessions.Count
        });
    }

    // --- Raw Hevy API DTOs for deserialization ---

    private sealed record HevyWorkoutsRawResponse
    {
        public int Page { get; init; }
        public int PageCount { get; init; }
        public List<HevyRawWorkout> Workouts { get; init; } = [];
    }

    private sealed record HevyRawWorkout
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string StartTime { get; init; } = string.Empty;
        public string EndTime { get; init; } = string.Empty;
        public List<HevyRawExercise> Exercises { get; init; } = [];
    }

    private sealed record HevyRawExercise
    {
        public string ExerciseTemplateId { get; init; } = string.Empty;
        public string? Title { get; init; }
        public List<HevyRawSet> Sets { get; init; } = [];
    }

    private sealed record HevyRawSet
    {
        public string Type { get; init; } = "normal";
        public decimal? WeightKg { get; init; }
        public int? Reps { get; init; }
    }

    private static IActionResult RequireApiKey() =>
        new BadRequestObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = "Hevy API key is required."
        });
}
