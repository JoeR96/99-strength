using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Captures the state of an exercise's progression at a point in time.
/// Used to restore progression state when undoing a completed day.
/// </summary>
public sealed class ProgressionSnapshot : ValueObject
{
    public Guid ExerciseId { get; private init; }
    public string ExerciseName { get; private init; } = string.Empty;
    public string ProgressionType { get; private init; } = string.Empty;
    public string ProgressionStateJson { get; private init; } = string.Empty;

    // EF Core constructor
    private ProgressionSnapshot() { }

    public ProgressionSnapshot(
        Guid exerciseId,
        string exerciseName,
        string progressionType,
        string progressionStateJson)
    {
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        ProgressionType = progressionType;
        ProgressionStateJson = progressionStateJson;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExerciseId;
        yield return ProgressionType;
        yield return ProgressionStateJson;
    }
}
