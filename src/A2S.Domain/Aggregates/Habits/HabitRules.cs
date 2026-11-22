namespace A2S.Domain.Aggregates.Habits;

/// <summary>A numeric habit's week so far: the average per logged day against the target.</summary>
public sealed record HabitWeeklyAverage(DateOnly WeekStart, DateOnly Through, decimal Average, int DaysLogged, decimal Target)
{
    public bool OnTarget => DaysLogged > 0 && Average >= Target;
}

/// <summary>Whether a day's log satisfies a habit's minimum or full target.</summary>
public static class HabitRules
{
    /// <summary>True when something was logged and it reached at least the bad-day minimum.</summary>
    public static bool MetMinimum(HabitDefinition habit, HabitLog? log) =>
        log is not null && log.Value >= habit.Minimum;

    /// <summary>True when something was logged and it reached the full target.</summary>
    public static bool MetTarget(HabitDefinition habit, HabitLog? log) =>
        log is not null && log.Value >= habit.Target;

    /// <summary>Monday of the (Monday–Sunday) week that holds <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>
    /// The average of the days logged from Monday through <paramref name="date"/> (days with no log are left out,
    /// so a morning with nothing logged yet doesn't drag the week down). Rounded to whole units.
    /// </summary>
    public static HabitWeeklyAverage WeeklyAverage(HabitDefinition habit, IEnumerable<HabitLog> logs, DateOnly date)
    {
        var monday = WeekStart(date);
        var week = logs
            .Where(l => l.HabitId == habit.Id && l.Date >= monday && l.Date <= date)
            .ToList();
        var average = week.Count == 0 ? 0m : Math.Round(week.Average(l => l.Value), 0, MidpointRounding.AwayFromZero);
        return new HabitWeeklyAverage(monday, date, average, week.Count, habit.Target);
    }
}
