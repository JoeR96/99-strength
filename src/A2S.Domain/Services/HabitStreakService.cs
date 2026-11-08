using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;

namespace A2S.Domain.Services;

/// <summary>
/// Streaks with the "never miss twice" rule. A day counts when <b>every</b> active habit has a log that
/// reaches at least its minimum (a 30% day still counts). Walking back from today, one non-counting day is
/// forgiven, two in a row end the streak, and today not yet logged is an open day rather than a miss.
/// </summary>
public sealed class HabitStreakService
{
    public HabitStreak Compute(
        IReadOnlyList<HabitDefinition> activeHabits,
        IReadOnlyList<HabitLog> logs,
        DateOnly today,
        DateOnly? firstDay = null)
    {
        var habits = activeHabits.Where(h => !h.Archived).ToList();
        if (habits.Count == 0 || logs.Count == 0)
        {
            return HabitStreak.None;
        }

        var start = firstDay ?? logs.Min(l => l.Date);
        if (start > today)
        {
            return HabitStreak.None;
        }

        var countingDays = CountingDays(habits, logs, start, today);
        var lastCounted = countingDays.Count == 0 ? (DateOnly?)null : countingDays.Max();
        var todayCounted = countingDays.Contains(today);

        var (current, graceUsed) = WalkBack(countingDays, start, today, todayCounted);
        var best = Math.Max(current, LongestRun(countingDays, start, today, todayCounted));

        return new HabitStreak(current, best, lastCounted, todayCounted, graceUsed);
    }

    private static HashSet<DateOnly> CountingDays(
        IReadOnlyList<HabitDefinition> habits,
        IReadOnlyList<HabitLog> logs,
        DateOnly start,
        DateOnly today)
    {
        var byDay = logs
            .Where(l => l.Date >= start && l.Date <= today)
            .GroupBy(l => l.Date)
            .ToDictionary(g => g.Key, g => g.ToDictionary(l => l.HabitId, l => l));

        var counting = new HashSet<DateOnly>();
        foreach (var (date, dayLogs) in byDay)
        {
            var allMet = habits.All(h => HabitRules.MetMinimum(h, dayLogs.GetValueOrDefault(h.Id)));
            if (allMet)
            {
                counting.Add(date);
            }
        }

        return counting;
    }

    private static (int Current, bool GraceUsed) WalkBack(
        HashSet<DateOnly> countingDays,
        DateOnly start,
        DateOnly today,
        bool todayCounted)
    {
        var current = todayCounted ? 1 : 0;
        var graceUsed = false;
        var consecutiveMisses = 0;

        for (var day = today.AddDays(-1); day >= start; day = day.AddDays(-1))
        {
            if (countingDays.Contains(day))
            {
                current++;
                if (consecutiveMisses == 1)
                {
                    graceUsed = true;
                }

                consecutiveMisses = 0;
                continue;
            }

            consecutiveMisses++;
            if (consecutiveMisses >= 2)
            {
                break;
            }
        }

        return (current, graceUsed);
    }

    private static int LongestRun(HashSet<DateOnly> countingDays, DateOnly start, DateOnly today, bool todayCounted)
    {
        var best = 0;
        var run = 0;
        var consecutiveMisses = 0;
        var lastClosedDay = todayCounted ? today : today.AddDays(-1);

        for (var day = start; day <= lastClosedDay; day = day.AddDays(1))
        {
            if (countingDays.Contains(day))
            {
                run++;
                consecutiveMisses = 0;
                best = Math.Max(best, run);
                continue;
            }

            consecutiveMisses++;
            if (consecutiveMisses >= 2)
            {
                run = 0;
            }
        }

        return best;
    }
}
