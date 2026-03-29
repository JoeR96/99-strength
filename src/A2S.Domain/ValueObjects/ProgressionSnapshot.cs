using System.Text.Json;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Captures the state of an exercise's progression at a point in time.
/// Used to restore progression state when undoing a completed day.
/// Handles JSON serialization of typed state objects for persistence.
/// </summary>
public sealed class ProgressionSnapshot : ValueObject
{
    public ExerciseId ExerciseId { get; private init; }
    public string ExerciseName { get; private init; } = string.Empty;
    public string ProgressionType { get; private init; } = string.Empty;
    public string ProgressionStateJson { get; private init; } = string.Empty;

    // EF Core constructor
    private ProgressionSnapshot() { }

    public ProgressionSnapshot(
        ExerciseId exerciseId,
        string exerciseName,
        string progressionType,
        string progressionStateJson)
    {
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        ProgressionType = progressionType;
        ProgressionStateJson = progressionStateJson;
    }

    public static ProgressionSnapshot FromState(ExerciseId exerciseId, string exerciseName, LinearProgressionState state) =>
        new(exerciseId, exerciseName, "Linear", JsonSerializer.Serialize(state));

    public static ProgressionSnapshot FromState(ExerciseId exerciseId, string exerciseName, RepsPerSetProgressionState state) =>
        new(exerciseId, exerciseName, "RepsPerSet", JsonSerializer.Serialize(state));

    public static ProgressionSnapshot FromState(ExerciseId exerciseId, string exerciseName, MinimalSetsProgressionState state) =>
        new(exerciseId, exerciseName, "MinimalSets", JsonSerializer.Serialize(state));

    public LinearProgressionState? GetLinearState() =>
        ProgressionType == "Linear" ? JsonSerializer.Deserialize<LinearProgressionState>(ProgressionStateJson) : null;

    public RepsPerSetProgressionState? GetRepsPerSetState() =>
        ProgressionType == "RepsPerSet" ? JsonSerializer.Deserialize<RepsPerSetProgressionState>(ProgressionStateJson) : null;

    public MinimalSetsProgressionState? GetMinimalSetsState() =>
        ProgressionType == "MinimalSets" ? JsonSerializer.Deserialize<MinimalSetsProgressionState>(ProgressionStateJson) : null;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExerciseId;
        yield return ProgressionType;
        yield return ProgressionStateJson;
    }
}
