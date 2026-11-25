using System.Text.Json.Serialization;

namespace A2S.Application.Interfaces.Hevy;

// Hevy wire shapes (api.hevyapp.com/docs, read 8 Nov 2026). These records double as the
// Application-facing contract and the JSON format: snake_case names are pinned with attributes,
// nulls are left out on write. Anything the app does not use is ignored on read.

/// <summary>Hevy set types. Routine and workout sets carry one of these.</summary>
/// <summary>
/// Hevy only accepts RPE 6, 7, 7.5, 8, 8.5, 9, 9.5 or 10 on a set; anything else makes <c>POST /v1/workouts</c>
/// answer 400. The app's own scale goes lower (Easy = RPE 4), so below 6 is sent as no RPE (the effort is
/// already in the exercise notes), 6.5 goes up to 7 and anything else rounds to the nearest half and caps at 10.
/// </summary>
public static class HevyRpe
{
    public static decimal? ForHevy(decimal? rpe)
    {
        if (rpe is not { } value || value < 6m)
        {
            return null;
        }

        var half = Math.Min(10m, Math.Round(value * 2m, MidpointRounding.AwayFromZero) / 2m);
        return half == 6.5m ? 7m : half;
    }

    /// <summary>
    /// A cardio exercise's effort as a Hevy RPE: Easy 6 (Hevy's lowest), Solid 7, Brutal 9. Only cardio exercises use
    /// this, so a session logged or edited in Hevy can come back as Easy (<see cref="CardioExerciseEffort"/>).
    /// </summary>
    public static decimal CardioExerciseRpe(A2S.Domain.Aggregates.Plan.Effort effort) => effort switch
    {
        A2S.Domain.Aggregates.Plan.Effort.Easy => 6m,
        A2S.Domain.Aggregates.Plan.Effort.Solid => 7m,
        _ => 9m
    };

    /// <summary>A Hevy RPE on a cardio exercise as an effort: 6 or less Easy, 7 to 8 Solid, 9 or more Brutal.</summary>
    public static A2S.Domain.Aggregates.Plan.Effort CardioExerciseEffort(decimal rpe) => rpe switch
    {
        <= 6m => A2S.Domain.Aggregates.Plan.Effort.Easy,
        < 9m => A2S.Domain.Aggregates.Plan.Effort.Solid,
        _ => A2S.Domain.Aggregates.Plan.Effort.Brutal
    };
}

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

/// <summary>
/// Body of <c>POST /v1/exercise_templates</c>, sent wrapped as <c>{"exercise": {...}}</c>. The create endpoint uses
/// different field names from the read model (<c>muscle_group</c> / <c>other_muscles</c>, not
/// <c>primary_muscle_group</c> / <c>secondary_muscle_groups</c>) and requires <c>equipment_category</c>; anything
/// else is a 400.
/// </summary>
public sealed record HevyExerciseTemplateDraft
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("exercise_type")] public required string ExerciseType { get; init; }
    [JsonPropertyName("equipment_category")] public string EquipmentCategory { get; init; } = "none";
    [JsonPropertyName("muscle_group")] public required string PrimaryMuscleGroup { get; init; }
    [JsonPropertyName("other_muscles")] public IReadOnlyList<string> SecondaryMuscleGroups { get; init; } = [];
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

/// <summary>One set of a workout the app logs in Hevy. <c>custom_metric</c> carries floors (or steps) for stair machines.</summary>
public sealed record HevyWorkoutSetDraft
{
    [JsonPropertyName("type")] public string Type { get; init; } = HevySetTypes.Normal;
    [JsonPropertyName("weight_kg")] public decimal? WeightKg { get; init; }
    [JsonPropertyName("reps")] public int? Reps { get; init; }
    [JsonPropertyName("distance_meters")] public int? DistanceMeters { get; init; }
    [JsonPropertyName("duration_seconds")] public int? DurationSeconds { get; init; }
    [JsonPropertyName("custom_metric")] public decimal? CustomMetric { get; init; }
    [JsonPropertyName("rpe")] public decimal? Rpe { get; init; }
}

public sealed record HevyWorkoutExerciseDraft
{
    [JsonPropertyName("exercise_template_id")] public required string ExerciseTemplateId { get; init; }
    [JsonPropertyName("superset_id")] public int? SupersetId { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("sets")] public IReadOnlyList<HevyWorkoutSetDraft> Sets { get; init; } = [];
}

/// <summary>What the app sends to log a completed workout (<c>POST /v1/workouts</c>).</summary>
public sealed record HevyWorkoutDraft
{
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("start_time")] public required DateTimeOffset StartTime { get; init; }
    [JsonPropertyName("end_time")] public required DateTimeOffset EndTime { get; init; }
    [JsonPropertyName("is_private")] public bool IsPrivate { get; init; }
    [JsonPropertyName("exercises")] public IReadOnlyList<HevyWorkoutExerciseDraft> Exercises { get; init; } = [];
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
