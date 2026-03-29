namespace A2S.Domain.Enums;

/// <summary>
/// Type of audit entry recorded during workout operations.
/// </summary>
public enum AuditEntryType
{
    TemporarySubstitution,
    PermanentSubstitution,
    ProgressionSkipped,
    UndoCompletion,
    ManualAdjustment
}
