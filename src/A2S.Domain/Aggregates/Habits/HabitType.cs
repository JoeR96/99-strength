namespace A2S.Domain.Aggregates.Habits;

/// <summary>How a habit is logged each day.</summary>
public enum HabitType
{
    /// <summary>Tap +1 / -1 towards a target (water bottles 0/6, walks 0/N).</summary>
    Counter,

    /// <summary>Enter a value (e.g. morning weight).</summary>
    Numeric,

    /// <summary>Done / not done. Target and minimum are always 1.</summary>
    Checkbox
}
