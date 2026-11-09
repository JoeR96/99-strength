using System.Text.Json.Serialization;

namespace A2S.Application.Interfaces.Hevy;

// Hevy wire shapes (api.hevyapp.com/docs, read 8 Nov 2026). These records double as the
// Application-facing contract and the JSON format: snake_case names are pinned with attributes,
// nulls are left out on write. Anything the app does not use is ignored on read.

/// <summary>Hevy set types. Routine and workout sets carry one of these.</summary>
public static class HevySetTypes
{
    public const string Warmup = "warmup";
    public const string Normal = "normal";
    public const string Failure = "failure";
    public const string Dropset = "dropset";

    /// <summary>Sets that count as work: normal and failure (AMRAP).</summary>
    public static bool IsWorking(string? type) => type is Normal or Failure;
}

/// <summary>Hevy custom exercise template types.</summary>
public static class HevyExerciseTypes
{
    public const string WeightReps = "weight_reps";
    public const string RepsOnly = "reps_only";
    public const string BodyweightReps = "bodyweight_reps";
    public const string BodyweightAssistedReps = "bodyweight_assisted_reps";
    public const string Duration = "duration";
    public const string WeightDuration = "weight_duration";
    public const string DistanceDuration = "distance_duration";
    public const string ShortDistanceWeight = "short_distance_weight";
}

// --- Routines ---

public sealed record HevySetDraft
{
    [JsonPropertyName("type")] public string Type { get; init; } = HevySetTypes.Normal;
    [JsonPropertyName("weight_kg")] public decimal? WeightKg { get; init; }
    [JsonPropertyName("reps")] public int? Reps { get; init; }
    [JsonPropertyName("distance_meters")] public int? DistanceMeters { get; init; }
    [JsonPropertyName("duration_seconds")] public int? DurationSeconds { get; init; }
    [JsonPropertyName("custom_metric")] public decimal? CustomMetric { get; init; }
}

public sealed record HevyRoutineExerciseDraft
{
    [JsonPropertyName("exercise_template_id")] public required string ExerciseTemplateId { get; init; }
    [JsonPropertyName("superset_id")] public int? SupersetId { get; init; }
    [JsonPropertyName("rest_seconds")] public int? RestSeconds { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("sets")] public IReadOnlyList<HevySetDraft> Sets { get; init; } = [];
}

/// <summary>What the app sends to create or update a routine.</summary>
public sealed record HevyRoutineDraft
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("folder_id")] public int? FolderId { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("exercises")] public IReadOnlyList<HevyRoutineExerciseDraft> Exercises { get; init; } = [];
}

/// <summary>A routine as Hevy returns it (only the fields the app reads).</summary>
public sealed record HevyRoutine
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("folder_id")] public int? FolderId { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("exercises")] public IReadOnlyList<HevyWorkoutExercise> Exercises { get; init; } = [];
}

/// <summary>One page of a Hevy list; the items key differs per resource so the client fills <see cref="Items"/>.</summary>
public sealed record HevyPage<T>
{
    public int Page { get; init; }
    public int PageCount { get; init; }
    public IReadOnlyList<T> Items { get; init; } = [];

    public bool HasMore => Page < PageCount;
}

// --- Folders ---

public sealed record HevyRoutineFolder
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
}

// --- Exercise templates ---

public sealed record HevyExerciseTemplateDraft
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("exercise_type")] public required string ExerciseType { get; init; }
    [JsonPropertyName("primary_muscle_group")] public required string PrimaryMuscleGroup { get; init; }
    [JsonPropertyName("secondary_muscle_groups")] public IReadOnlyList<string> SecondaryMuscleGroups { get; init; } = [];
}

public sealed record HevyExerciseTemplate
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("exercise_type")] public string? ExerciseType { get; init; }
    [JsonPropertyName("primary_muscle_group")] public string? PrimaryMuscleGroup { get; init; }
    [JsonPropertyName("is_custom")] public bool IsCustom { get; init; }
}

// --- Workouts and events ---

public sealed record HevyWorkoutSet
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = HevySetTypes.Normal;
    [JsonPropertyName("weight_kg")] public decimal? WeightKg { get; init; }
    [JsonPropertyName("reps")] public int? Reps { get; init; }
    [JsonPropertyName("distance_meters")] public int? DistanceMeters { get; init; }
    [JsonPropertyName("duration_seconds")] public int? DurationSeconds { get; init; }
    [JsonPropertyName("rpe")] public decimal? Rpe { get; init; }
    [JsonPropertyName("custom_metric")] public decimal? CustomMetric { get; init; }
}

public sealed record HevyWorkoutExercise
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("exercise_template_id")] public string ExerciseTemplateId { get; init; } = string.Empty;
    [JsonPropertyName("superset_id")] public int? SupersetId { get; init; }
    [JsonPropertyName("rest_seconds")] public int? RestSeconds { get; init; }
    [JsonPropertyName("sets")] public IReadOnlyList<HevyWorkoutSet> Sets { get; init; } = [];
}

/// <summary>A completed workout as Hevy returns it.</summary>
public sealed record HevyWorkout
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("routine_id")] public string? RoutineId { get; init; }
    [JsonPropertyName("start_time")] public DateTimeOffset StartTime { get; init; }
    [JsonPropertyName("end_time")] public DateTimeOffset EndTime { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("exercises")] public IReadOnlyList<HevyWorkoutExercise> Exercises { get; init; } = [];

    /// <summary>Total of the sets' durations, or the wall-clock length when no set carries one.</summary>
    public int TotalDurationSeconds()
    {
        var fromSets = Exercises.SelectMany(e => e.Sets).Sum(s => s.DurationSeconds ?? 0);
        if (fromSets > 0)
        {
            return fromSets;
        }

        var wall = (int)Math.Round((EndTime - StartTime).TotalSeconds);
        return Math.Max(wall, 0);
    }

    public int? TotalDistanceMeters()
    {
        var sum = Exercises.SelectMany(e => e.Sets).Sum(s => s.DistanceMeters ?? 0);
        return sum > 0 ? sum : null;
    }
}

/// <summary>One entry from <c>GET /v1/workouts/events</c>: an updated workout or a deletion.</summary>
public sealed record HevyWorkoutEvent
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("workout")] public HevyWorkout? Workout { get; init; }
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("deleted_at")] public DateTimeOffset? DeletedAt { get; init; }

    public bool IsUpdated => string.Equals(Type, "updated", StringComparison.OrdinalIgnoreCase) && Workout is not null;
    public bool IsDeleted => string.Equals(Type, "deleted", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Id);

    /// <summary>When this event happened, for advancing the pull cursor.</summary>
    public DateTimeOffset? OccurredAt => IsDeleted ? DeletedAt : Workout?.UpdatedAt ?? Workout?.EndTime;
}

// --- Body measurements ---

public sealed record HevyBodyMeasurement
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("date")] public string Date { get; init; } = string.Empty;
    [JsonPropertyName("weight_kg")] public decimal? WeightKg { get; init; }
}
