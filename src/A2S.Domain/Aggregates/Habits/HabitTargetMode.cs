namespace A2S.Domain.Aggregates.Habits;

/// <summary>How a habit's target is judged.</summary>
public enum HabitTargetMode
{
    /// <summary>Each day against its minimum and target (the streak rule).</summary>
    DailyMinimum = 0,

    /// <summary>The week's (Monday–Sunday) average per logged day against the target, e.g. 8 000 steps a day on average.</summary>
    WeeklyAverage = 1
}
