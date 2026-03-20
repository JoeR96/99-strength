using A2S.Domain.Aggregates.Workout;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Services;

/// <summary>
/// Domain service interface for Hevy integration.
/// Following the Anti-Corruption Layer pattern from DDD.
/// This interface defines the contract in domain terms, not Hevy API terms.
/// </summary>
public interface IHevyIntegrationService
{
    /// <summary>
    /// Creates a routine in Hevy for a specific training day.
    /// </summary>
    /// <param name="workout">The workout aggregate</param>
    /// <param name="weekNumber">The week number (1-21)</param>
    /// <param name="dayNumber">The day number (1-6)</param>
    /// <param name="apiKey">The user's Hevy API key</param>
    /// <returns>Result containing the created routine info or error</returns>
    Task<HevyRoutineSyncResult> SyncRoutineForDayAsync(
        Workout workout,
        int weekNumber,
        int dayNumber,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a completed workout entry in Hevy.
    /// </summary>
    /// <param name="workout">The workout aggregate</param>
    /// <param name="dayNumber">The day number that was completed</param>
    /// <param name="performances">The exercise performances from the completed day</param>
    /// <param name="startTime">When the workout started</param>
    /// <param name="endTime">When the workout ended</param>
    /// <param name="apiKey">The user's Hevy API key</param>
    /// <returns>Result containing the created workout info or error</returns>
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
    /// <param name="programName">The name of the program</param>
    /// <param name="apiKey">The user's Hevy API key</param>
    /// <returns>The folder ID or null if failed</returns>
    Task<string?> GetOrCreateRoutineFolderAsync(
        string programName,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a routine from Hevy.
    /// </summary>
    /// <param name="routineId">The Hevy routine ID</param>
    /// <param name="apiKey">The user's Hevy API key</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteRoutineAsync(
        string routineId,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the Hevy API key.
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Pulls workout data from Hevy to detect what was actually completed.
    /// Used for reconciliation when completing a day.
    /// </summary>
    /// <param name="workout">The workout aggregate</param>
    /// <param name="weekNumber">The week to search for</param>
    /// <param name="dayNumber">The day to search for</param>
    /// <param name="apiKey">The user's Hevy API key</param>
    /// <returns>Pulled workout data or null if not found</returns>
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
/// Used for reconciliation.
/// </summary>
public sealed record HevyPulledWorkoutData
{
    public string WorkoutId { get; init; } = string.Empty;
    public string WorkoutTitle { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public IReadOnlyList<HevyPulledExerciseData> Exercises { get; init; } = Array.Empty<HevyPulledExerciseData>();
}

/// <summary>
/// Exercise data pulled from Hevy.
/// </summary>
public sealed record HevyPulledExerciseData
{
    public string HevyTemplateId { get; init; } = string.Empty;
    public string ExerciseName { get; init; } = string.Empty;
    public IReadOnlyList<HevyPulledSetData> Sets { get; init; } = Array.Empty<HevyPulledSetData>();
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
