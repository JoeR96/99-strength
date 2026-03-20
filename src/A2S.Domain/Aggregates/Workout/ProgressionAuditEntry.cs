using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Records a progression-related event for audit trail purposes.
/// </summary>
public sealed class ProgressionAuditEntry : ValueObject
{
    public DateTime OccurredAt { get; private init; }
    public AuditEntryType Type { get; private init; }
    public Guid ExerciseId { get; private init; }
    public string ExerciseName { get; private init; } = string.Empty;
    public int WeekNumber { get; private init; }
    public int DayNumber { get; private init; }
    public string? OldValue { get; private init; }  // JSON of previous state
    public string? NewValue { get; private init; }  // JSON of new state
    public string? Reason { get; private init; }

    // EF Core constructor
    private ProgressionAuditEntry() { }

    private ProgressionAuditEntry(
        AuditEntryType type,
        Guid exerciseId,
        string exerciseName,
        int weekNumber,
        int dayNumber,
        string? oldValue,
        string? newValue,
        string? reason)
    {
        OccurredAt = DateTime.UtcNow;
        Type = type;
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        WeekNumber = weekNumber;
        DayNumber = dayNumber;
        OldValue = oldValue;
        NewValue = newValue;
        Reason = reason;
    }

    public static ProgressionAuditEntry TemporarySubstitution(
        Guid exerciseId, string exerciseName, int weekNumber, int dayNumber, string? reason = null)
        => new(AuditEntryType.TemporarySubstitution, exerciseId, exerciseName, weekNumber, dayNumber, null, null, reason ?? "Temporary substitution - progression skipped");

    public static ProgressionAuditEntry PermanentSubstitution(
        Guid exerciseId, string originalName, string newName, int weekNumber, int dayNumber,
        string? oldProgressionJson, string? newProgressionJson, string? reason = null)
        => new(AuditEntryType.PermanentSubstitution, exerciseId, originalName, weekNumber, dayNumber,
            oldProgressionJson, newProgressionJson, reason ?? $"Permanently replaced with {newName}");

    public static ProgressionAuditEntry ProgressionSkipped(
        Guid exerciseId, string exerciseName, int weekNumber, int dayNumber, string reason)
        => new(AuditEntryType.ProgressionSkipped, exerciseId, exerciseName, weekNumber, dayNumber, null, null, reason);

    public static ProgressionAuditEntry UndoCompletion(int weekNumber, int dayNumber, string? reason = null)
        => new(AuditEntryType.UndoCompletion, Guid.Empty, "N/A", weekNumber, dayNumber, null, null, reason ?? "User initiated undo");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OccurredAt;
        yield return Type;
        yield return ExerciseId;
        yield return WeekNumber;
        yield return DayNumber;
    }
}

public enum AuditEntryType
{
    TemporarySubstitution,
    PermanentSubstitution,
    ProgressionSkipped,
    UndoCompletion,
    ManualAdjustment
}
