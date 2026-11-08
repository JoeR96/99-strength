namespace A2S.Domain.Services;

/// <summary>
/// One exercise's contribution to a session: planned sets (count both sides for unilateral
/// work), rest after each set, and an optional superset group shared with another exercise.
/// </summary>
public sealed record ExerciseTiming(string Name, int Sets, int RestSeconds, int? SupersetGroup);

/// <summary>
/// Result of <see cref="SessionTimeEstimator.Estimate"/>; every figure is in whole minutes.
/// </summary>
public sealed record SessionTimeEstimate(
    int TotalMinutes,
    int WarmUpMinutes,
    IReadOnlyList<(string Name, int Minutes)> PerExercise,
    bool OverBudget,
    int BudgetMinutes);

/// <summary>
/// Rest periods used when an exercise has no explicit rest (brief § 2).
/// </summary>
public static class RestDefaults
{
    public const int MainLiftSeconds = 180;
    public const int AccessorySeconds = 90;
    public const int SupersetSeconds = 60;
}
