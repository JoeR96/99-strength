using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;

namespace A2S.Domain.Services;

/// <summary>A soft rule the weekly schedule breaks. Warnings inform; they never block a change.</summary>
public sealed record ScheduleWarning(string Code, string Message);

/// <summary>
/// Soft rules for the weekly schedule template (brief § 5: "no 4×4 the day before Lower A or B").
/// Pure: it reads the schedule and, when given, a way to tell which cardio protocol a slot points at.
/// </summary>
public static class PlanScheduleRules
{
    public const string FourByFourBeforeLowerDay = "FourByFourBeforeLowerDay";

    /// <summary>
    /// One warning for every Norwegian 4×4 slot scheduled the day before a lower-body lift.
    /// A slot counts as 4×4 when <paramref name="protocolKind"/> says so for its protocol, or when its
    /// label says "4×4" / "4x4". A lift counts as lower-body when its label contains "lower".
    /// Sunday is the day before Monday.
    /// </summary>
    public static IReadOnlyList<ScheduleWarning> Warnings(
        IEnumerable<ScheduledSession> schedule,
        Func<CardioProtocolId, CardioProtocolKind?>? protocolKind = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var sessions = schedule.ToList();
        var warnings = new List<ScheduleWarning>();

        foreach (var cardio in sessions.Where(s => s.Kind == SessionKind.Cardio && IsFourByFour(s, protocolKind)))
        {
            var nextDay = (DayOfWeek)(((int)cardio.Day + 1) % 7);
            foreach (var lift in sessions.Where(s => s.Kind == SessionKind.Lift && s.Day == nextDay && IsLowerBody(s)))
            {
                warnings.Add(new ScheduleWarning(
                    FourByFourBeforeLowerDay,
                    $"Norwegian 4×4 on {cardio.Day} is the day before {lift.Label} ({nextDay}). Move it or swap the lifting days."));
            }
        }

        return warnings;
    }

    private static bool IsFourByFour(ScheduledSession session, Func<CardioProtocolId, CardioProtocolKind?>? protocolKind)
    {
        if (session.CardioProtocolId is { } id && protocolKind?.Invoke(id) is { } kind)
        {
            return kind == CardioProtocolKind.NorwegianFourByFour;
        }

        return session.Label.Contains("4×4", StringComparison.OrdinalIgnoreCase)
            || session.Label.Contains("4x4", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowerBody(ScheduledSession session) =>
        session.Label.Contains("lower", StringComparison.OrdinalIgnoreCase);
}
