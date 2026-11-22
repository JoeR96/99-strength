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

    /// <summary>The phase heads-up goes out from 08:00 to 21:00 local on any day of the heads-up window (once per phase).</summary>
    public static readonly TimeOnly PhaseChangeFrom = new(8, 0);
    public static readonly TimeOnly PhaseChangeUntil = new(21, 0);

    /// <summary>"Build starts Monday 4 Jan" with the first changes, when <paramref name="phase"/> is in its heads-up window; null otherwise.</summary>
    public static PushNudge? PhaseChangeNudge(TimeOnly nowLocal, DateOnly today, PlanPhase? phase)
    {
        if (phase is null || !phase.HasChanges || !phase.InHeadsUpWindow(today) || nowLocal < PhaseChangeFrom || nowLocal >= PhaseChangeUntil)
        {
            return null;
        }

        var name = phase.Label ?? phase.Type.ToString();
        var when = phase.From == today ? "starts today" : $"starts {phase.From.ToString("dddd d MMM", CultureInfo.InvariantCulture)}";
        var lines = phase.Changes.Concat(phase.Swaps.Select(s => $"{s.ToExercise} replaces {s.FromExercise}")).Take(3);
        return new PushNudge(PushNudgeKind.PhaseChange, $"{name} {when}", string.Join("; ", lines), TodayUrl);
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
