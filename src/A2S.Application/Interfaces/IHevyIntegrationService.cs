namespace A2S.Application.Interfaces;

/// <summary>
/// Application service interface for Hevy integration.
/// Following the Anti-Corruption Layer pattern — uses DTOs, not domain entities.
/// </summary>
public interface IHevyIntegrationService
{
    /// <summary>
    /// Creates a routine in Hevy for a specific training day.
    /// </summary>
    Task<HevyRoutineSyncResult> SyncRoutineForDayAsync(
        HevySyncRoutineRequest request,
        string apiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a completed workout entry in Hevy.
    /// </summary>
    Task<HevyWorkoutSyncResult> SyncCompletedWorkoutAsync(
        HevySyncWorkoutRequest request,
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
        HevyPullRequest request,
        string apiKey,
        CancellationToken ct = default);
}
