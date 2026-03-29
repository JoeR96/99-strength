namespace A2S.Application.Interfaces;

// --- Request DTOs (Application → Hevy service) ---

/// <summary>
/// Request to sync a routine for a specific training day.
/// </summary>
public sealed record HevySyncRoutineRequest
{
    public required string WorkoutName { get; init; }
    public required int WeekNumber { get; init; }
    public required int DayNumber { get; init; }
    public required int BlockNumber { get; init; }
    public required bool IsDeload { get; init; }
    public required decimal IntensityPercentage { get; init; }
    public required IReadOnlyList<HevySyncExerciseInfo> Exercises { get; init; }
}

/// <summary>
/// Exercise info for routine sync — contains everything the Hevy service needs.
/// </summary>
public sealed record HevySyncExerciseInfo
{
    public required string ExternalTemplateId { get; init; }
    public required string ProgressionType { get; init; }
    public required string Notes { get; init; }
    public required IReadOnlyList<HevySyncSetInfo> PlannedSets { get; init; }
}

/// <summary>
/// Planned set info for routine sync.
/// </summary>
public sealed record HevySyncSetInfo
{
    public required decimal WeightKg { get; init; }
    public required int TargetReps { get; init; }
    public required bool IsAmrap { get; init; }
}

/// <summary>
/// Request to sync a completed workout to Hevy.
/// </summary>
public sealed record HevySyncWorkoutRequest
{
    public required string WorkoutName { get; init; }
    public required int WeekNumber { get; init; }
    public required int DayNumber { get; init; }
    public required int BlockNumber { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required IReadOnlyList<HevySyncCompletedExercise> Exercises { get; init; }
}

/// <summary>
/// Completed exercise data for workout sync.
/// </summary>
public sealed record HevySyncCompletedExercise
{
    public required string ExternalTemplateId { get; init; }
    public required string? Notes { get; init; }
    public required IReadOnlyList<HevySyncCompletedSet> CompletedSets { get; init; }
}

/// <summary>
/// Completed set data for workout sync.
/// </summary>
public sealed record HevySyncCompletedSet
{
    public required decimal WeightKg { get; init; }
    public required int Reps { get; init; }
    public required bool WasAmrap { get; init; }
}

/// <summary>
/// Request to pull workout data from Hevy.
/// </summary>
public sealed record HevyPullRequest
{
    public required string WorkoutName { get; init; }
    public required int WeekNumber { get; init; }
    public required int DayNumber { get; init; }
}

// --- Response DTOs (Hevy service → Application) ---

/// <summary>
/// Result of syncing a routine to Hevy.
/// </summary>
public sealed record HevyRoutineSyncResult
{
    public bool Success { get; init; }
    public string? RoutineId { get; init; }
    public string? RoutineTitle { get; init; }
    public string? ErrorMessage { get; init; }
    public bool AlreadyExists { get; init; }

    public static HevyRoutineSyncResult Succeeded(string routineId, string routineTitle, bool alreadyExists = false)
        => new() { Success = true, RoutineId = routineId, RoutineTitle = routineTitle, AlreadyExists = alreadyExists };

    public static HevyRoutineSyncResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of syncing a completed workout to Hevy.
/// </summary>
public sealed record HevyWorkoutSyncResult
{
    public bool Success { get; init; }
    public string? WorkoutId { get; init; }
    public string? WorkoutTitle { get; init; }
    public string? ErrorMessage { get; init; }

    public static HevyWorkoutSyncResult Succeeded(string workoutId, string workoutTitle)
        => new() { Success = true, WorkoutId = workoutId, WorkoutTitle = workoutTitle };

    public static HevyWorkoutSyncResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Data pulled from a completed Hevy workout.
/// </summary>
public sealed record HevyPulledWorkoutData
{
    public string WorkoutId { get; init; } = string.Empty;
    public string WorkoutTitle { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public IReadOnlyList<HevyPulledExerciseData> Exercises { get; init; } = [];
}

/// <summary>
/// Exercise data pulled from Hevy.
/// </summary>
public sealed record HevyPulledExerciseData
{
    public string HevyTemplateId { get; init; } = string.Empty;
    public string ExerciseName { get; init; } = string.Empty;
    public IReadOnlyList<HevyPulledSetData> Sets { get; init; } = [];
}

/// <summary>
/// Set data pulled from Hevy.
/// </summary>
public sealed record HevyPulledSetData
{
    public int SetNumber { get; init; }
    public decimal WeightKg { get; init; }
    public int Reps { get; init; }
    public bool WasFailureSet { get; init; }
}
