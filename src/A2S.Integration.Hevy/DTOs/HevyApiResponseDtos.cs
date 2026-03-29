using System.Text.Json.Serialization;

namespace A2S.Integration.Hevy.DTOs;

// --- Hevy API Response DTOs ---

internal sealed record HevyRoutinesResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; init; }

    [JsonPropertyName("routines")]
    public List<HevyRoutineResponse> Routines { get; init; } = new();
}

internal sealed record HevyRoutineResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}

internal sealed record HevyRoutineWrapper
{
    [JsonPropertyName("routine")]
    public HevyRoutineResponse Routine { get; init; } = null!;
}

internal sealed record HevyWorkoutsResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; init; }

    [JsonPropertyName("workouts")]
    public List<HevyWorkoutResponse> Workouts { get; init; } = new();
}

internal sealed record HevyWorkoutResponse
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

internal sealed record HevyWorkoutExerciseResponse
{
    [JsonPropertyName("exercise_template_id")]
    public string ExerciseTemplateId { get; init; } = string.Empty;

    [JsonPropertyName("sets")]
    public List<HevyWorkoutSetResponse> Sets { get; init; } = new();
}

internal sealed record HevyWorkoutSetResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "normal";

    [JsonPropertyName("weight_kg")]
    public decimal? WeightKg { get; init; }

    [JsonPropertyName("reps")]
    public int? Reps { get; init; }
}

internal sealed record HevyWorkoutWrapper
{
    [JsonPropertyName("workout")]
    public HevyWorkoutResponse Workout { get; init; } = null!;
}

internal sealed record HevyRoutineFoldersResponse
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_count")]
    public int PageCount { get; init; }

    [JsonPropertyName("routine_folders")]
    public List<HevyRoutineFolderResponse> RoutineFolders { get; init; } = new();
}

internal sealed record HevyRoutineFolderResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}

internal sealed record HevyRoutineFolderWrapper
{
    [JsonPropertyName("routine_folder")]
    public HevyRoutineFolderResponse RoutineFolder { get; init; } = null!;
}
