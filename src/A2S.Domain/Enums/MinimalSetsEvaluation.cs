namespace A2S.Domain.Enums;

/// <summary>
/// Result of evaluating performance for MinimalSets progression.
/// </summary>
public enum MinimalSetsEvaluation
{
    /// <summary>
    /// Completed target reps in fewer sets than expected - progress.
    /// </summary>
    Success,

    /// <summary>
    /// Completed target reps in expected number of sets - no change.
    /// </summary>
    Maintained,

    /// <summary>
    /// Could not complete target reps - need more sets.
    /// </summary>
    Failed
}
