using System.Globalization;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Services;

namespace A2S.Application.Push;

/// <summary>A session the plan has for today, with where it stands.</summary>
public sealed record PlannedSession(string Label, SessionKind Kind, SessionStatus Status);

/// <summary>A habit's value today against its minimum.</summary>
public sealed record HabitStanding(string Name, decimal Value, decimal Minimum)
{
    public bool UnderMinimum => Value < Minimum;
}

/// <summary>A notification to send: kind for the daily log, the rest for the screen.</summary>
public sealed record PushNudge(PushNudgeKind Kind, string Title, string Body, string Url)
{
    public PushMessage ToMessage() => new(Title, Body, Url, Kind.ToString());
}

/// <summary>
/// The pure part of the nudge service: given the local time and what today looks like, which nudge
/// (if any) is due. Timing uses a one-hour window from the scheduled time so a tick that ran late
/// still fires, while the once-per-day log stops it firing twice.
/// </summary>
public static class PushNudgePlanner
{
    public static readonly TimeOnly HabitsReminderTime = new(20, 0);
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public const string TodayUrl = "/today";

    /// <summary>True when <paramref name="nowLocal"/> is within the hour after <paramref name="scheduled"/>.</summary>
    public static bool IsDue(TimeOnly nowLocal, TimeOnly scheduled)
    {
        var elapsed = nowLocal.ToTimeSpan() - scheduled.ToTimeSpan();
        return elapsed >= TimeSpan.Zero && elapsed < Window;
    }

    /// <summary>"Time to train — Upper A" when a planned, not-yet-done session is on today; null otherwise.</summary>
    public static PushNudge? SessionNudge(TimeOnly nowLocal, TimeOnly sessionStart, IReadOnlyList<PlannedSession> sessions)
    {
        if (!IsDue(nowLocal, sessionStart))
        {
            return null;
        }

        var waiting = sessions
            .Where(s => s.Kind != SessionKind.Rest && s.Status == SessionStatus.Planned)
            .Select(s => s.Label)
            .ToList();
        if (waiting.Count == 0)
        {
            return null;
        }

        var start = sessionStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        return new PushNudge(
            PushNudgeKind.SessionStart,
            $"Time to train — {string.Join(" + ", waiting)}",
            $"Planned for {start}. Open 99 Strength to start.",
            TodayUrl);
    }

    /// <summary>"Still under minimum: Water 2/4, Walks 0/1" at 20:00 when any habit is short; null otherwise.</summary>
    public static PushNudge? HabitsNudge(TimeOnly nowLocal, IReadOnlyList<HabitStanding> habits)
    {
        if (!IsDue(nowLocal, HabitsReminderTime))
        {
            return null;
        }

        var under = habits.Where(h => h.UnderMinimum).ToList();
        if (under.Count == 0)
        {
            return null;
        }

        var summary = string.Join(", ", under.Select(h => $"{h.Name} {Format(h.Value)}/{Format(h.Minimum)}"));
        return new PushNudge(
            PushNudgeKind.HabitsUnderMinimum,
            "Still under minimum",
            summary,
            TodayUrl);
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
