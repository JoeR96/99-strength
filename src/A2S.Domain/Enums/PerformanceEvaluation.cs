namespace A2S.Domain.Enums;

/// <summary>
/// Result of evaluating performance against rep range criteria.
/// </summary>
public enum PerformanceEvaluation
{
    /// <summary>
    /// All sets hit maximum reps - progress to next level.
    /// </summary>
    Success,

    /// <summary>
    /// All sets hit at least minimum reps - maintain current level.
    /// </summary>
    Maintained,

    /// <summary>
    /// Any set fell below minimum reps - regress to previous level.
    /// </summary>
    Failed
}
