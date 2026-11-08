namespace A2S.Application.Interfaces.Hevy;

/// <summary>
/// Typed, resilient access to the Hevy public API. Every method takes the caller's plaintext key
/// (from <see cref="IHevyKeyResolver"/>) and returns a <see cref="HevyResult{T}"/>; nothing throws
/// for an HTTP outcome. GETs are retried, writes are not; a 429 is surfaced with its
/// <c>Retry-After</c>; the circuit is per key.
/// </summary>
public interface IHevyClient
{
    /// <summary><c>GET /v1/workouts/events?since=</c>: newest first, <paramref name="pageSize"/> at most 10.</summary>
    Task<HevyResult<HevyPage<HevyWorkoutEvent>>> GetWorkoutEventsAsync(string apiKey, DateTimeOffset since, int page, int pageSize = 10, CancellationToken ct = default);

    Task<HevyResult<HevyPage<HevyRoutine>>> GetRoutinesAsync(string apiKey, int page, CancellationToken ct = default);

    /// <summary><c>POST /v1/routines</c> (201). Not retried: a timeout may still have created it.</summary>
    Task<HevyResult<HevyRoutine>> CreateRoutineAsync(string apiKey, HevyRoutineDraft routine, CancellationToken ct = default);

    /// <summary><c>PUT /v1/routines/{id}</c>. Not retried.</summary>
    Task<HevyResult<HevyRoutine>> UpdateRoutineAsync(string apiKey, string routineId, HevyRoutineDraft routine, CancellationToken ct = default);

    Task<HevyResult<HevyPage<HevyRoutineFolder>>> GetRoutineFoldersAsync(string apiKey, int page, CancellationToken ct = default);

    Task<HevyResult<HevyRoutineFolder>> CreateRoutineFolderAsync(string apiKey, string title, CancellationToken ct = default);

    Task<HevyResult<HevyPage<HevyExerciseTemplate>>> GetExerciseTemplatesAsync(string apiKey, int page, CancellationToken ct = default);

    Task<HevyResult<HevyExerciseTemplate>> CreateExerciseTemplateAsync(string apiKey, HevyExerciseTemplateDraft template, CancellationToken ct = default);

    /// <summary><c>POST /v1/body_measurements</c>; on 409 falls back to <c>PUT</c> for that date.</summary>
    Task<HevyResult<HevyBodyMeasurement>> UpsertBodyMeasurementAsync(string apiKey, DateOnly date, decimal weightKg, CancellationToken ct = default);

    /// <summary>True when the key is accepted (a cheap authenticated GET succeeds).</summary>
    Task<HevyResult<bool>> ValidateKeyAsync(string apiKey, CancellationToken ct = default);
}
