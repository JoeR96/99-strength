namespace A2S.Domain.Aggregates.Habits;

/// <summary>Whether a day's log satisfies a habit's minimum or full target.</summary>
public static class HabitRules
{
    /// <summary>True when something was logged and it reached at least the bad-day minimum.</summary>
    public static bool MetMinimum(HabitDefinition habit, HabitLog? log) =>
        log is not null && log.Value >= habit.Minimum;

    /// <summary>True when something was logged and it reached the full target.</summary>
    public static bool MetTarget(HabitDefinition habit, HabitLog? log) =>
        log is not null && log.Value >= habit.Target;
}
