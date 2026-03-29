using System.Text.Json.Serialization;

namespace A2S.Integration.Hevy.DTOs;

// --- Hevy API Request DTOs ---

internal sealed record HevyCreateRoutineRequest
{
    [JsonPropertyName("routine")]
    public HevyRoutineDto Routine { get; init; } = null!;
}

internal sealed record HevyRoutineDto
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

internal sealed record HevyRoutineExerciseDto
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

internal sealed record HevyRoutineSetDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "normal";

    [JsonPropertyName("weight_kg")]
    public decimal? WeightKg { get; init; }

    [JsonPropertyName("reps")]
    public int? Reps { get; init; }
}

internal sealed record HevyCreateWorkoutRequest
{
    [JsonPropertyName("workout")]
    public HevyWorkoutDto Workout { get; init; } = null!;
}

internal sealed record HevyWorkoutDto
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

internal sealed record HevyWorkoutExerciseDto
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

internal sealed record HevyWorkoutSetDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "normal";

    [JsonPropertyName("weight_kg")]
    public decimal? WeightKg { get; init; }

    [JsonPropertyName("reps")]
    public int? Reps { get; init; }
}
