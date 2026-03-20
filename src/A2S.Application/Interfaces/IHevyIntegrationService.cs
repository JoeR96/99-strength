using A2S.Domain.Aggregates.Workout;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Interfaces;

/// <summary>
/// Application service interface for Hevy integration.
/// Following the Anti-Corruption Layer pattern — defines the contract in domain terms, not Hevy API terms.
/// </summary>
public interface IHevyIntegrationService
{
    /// <summary>
    /// Creates a routine in Hevy for a specific training day.
    /// </summary>
    Task<HevyRoutineSyncResult> SyncRoutineForDayAsync(
        Workout workout,
        int weekNumber,
        int dayNumber,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a completed workout entry in Hevy.
    /// </summary>
    Task<HevyWorkoutSyncResult> SyncCompletedWorkoutAsync(
        Workout workout,
        int dayNumber,
        IReadOnlyList<ExercisePerformance> performances,
        DateTime startTime,
        DateTime endTime,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Gets or creates a routine folder for the workout program.
    /// </summary>
    Task<string?> GetOrCreateRoutineFolderAsync(
        string programName,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a routine from Hevy.
    /// </summary>
    Task<bool> DeleteRoutineAsync(
        string routineId,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the Hevy API key.
    /// </summary>
    Task<bool> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Pulls workout data from Hevy to detect what was actually completed.
    /// </summary>
    Task<HevyPulledWorkoutData?> PullWorkoutDataAsync(
        Workout workout,
        int weekNumber,
        int dayNumber,
        string apiKey,
        CancellationToken ct = default);
}

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
