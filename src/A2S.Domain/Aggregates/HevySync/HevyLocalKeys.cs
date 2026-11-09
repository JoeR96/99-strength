using System.Globalization;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Aggregates.HevySync;

/// <summary>
/// The local-key formats stored in <see cref="HevyLink.LocalKey"/>. Lift routines are keyed per date
/// (one Hevy routine per week, titled with the A2S2 week); cardio and mobility routines are keyed per
/// slot (their content barely changes week to week, so the same routine is updated in place).
/// </summary>
public static class HevyLocalKeys
{
    private const string DateFormat = "yyyyMMdd";

    public static string Folder(TrainingPlanId planId) => $"plan:{planId.Value:D}:folder";

    public static string LiftRoutine(TrainingPlanId planId, DateOnly date, DayNumber liftDay) =>
        $"plan:{planId.Value:D}:{date.ToString(DateFormat, CultureInfo.InvariantCulture)}:Lift:{liftDay}";

    /// <summary>Cardio and mobility routines: one per scheduled slot, identified by its label.</summary>
    public static string SlotRoutine(TrainingPlanId planId, SessionKind kind, string label) =>
        $"plan:{planId.Value:D}:{kind}:{Slug(label)}";

    public static string Routine(TrainingPlanId planId, DateOnly date, ScheduledSession session) =>
        session.Kind == SessionKind.Lift
            ? LiftRoutine(planId, date, session.LiftDay!.Value)
            : SlotRoutine(planId, session.Kind, session.Label);

    public static string DrillTemplate(string drillName) => $"drill:{Slug(drillName)}";

    public static string CardioTemplate(CardioProtocolKind kind) => $"cardio:{kind}";

    /// <summary>Reads a routine key back into the plan, kind and either lift day + date or slot label slug.</summary>
    public static bool TryParseRoutine(string localKey, out ParsedRoutineKey parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(localKey))
        {
            return false;
        }

        var parts = localKey.Split(':');
        if (parts.Length < 4 || parts[0] != "plan" || !Guid.TryParse(parts[1], out var planGuid))
        {
            return false;
        }

        var planId = new TrainingPlanId(planGuid);

        if (parts.Length == 5
            && DateOnly.TryParseExact(parts[2], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && parts[3] == nameof(SessionKind.Lift)
            && Enum.TryParse<DayNumber>(parts[4], out var liftDay))
        {
            parsed = new ParsedRoutineKey(planId, SessionKind.Lift, date, liftDay, null);
            return true;
        }

        if (parts.Length == 4
            && Enum.TryParse<SessionKind>(parts[2], out var kind)
            && kind is SessionKind.Cardio or SessionKind.Mobility)
        {
            parsed = new ParsedRoutineKey(planId, kind, null, null, parts[3]);
            return true;
        }

        return false;
    }

    /// <summary>Lower-case, trimmed, with runs of whitespace or colons collapsed to one dash.</summary>
    public static string Slug(string text)
    {
        var chars = (text ?? string.Empty).Trim().ToLowerInvariant().ToCharArray();
        var result = new System.Text.StringBuilder(chars.Length);
        var lastDash = false;
        foreach (var c in chars)
        {
            if (char.IsWhiteSpace(c) || c == ':')
            {
                if (!lastDash)
                {
                    result.Append('-');
                    lastDash = true;
                }
                continue;
            }

            result.Append(c);
            lastDash = false;
        }

        return result.ToString().Trim('-');
    }
}

/// <summary>A routine key taken apart. Lift keys carry a date and a lift day; slot keys carry the label slug.</summary>
public readonly record struct ParsedRoutineKey(TrainingPlanId PlanId, SessionKind Kind, DateOnly? Date, DayNumber? LiftDay, string? LabelSlug)
{
    /// <summary>Whether a scheduled session is the one this key was made for.</summary>
    public bool Describes(ScheduledSession session) =>
        session.Kind == Kind
        && (Kind == SessionKind.Lift
            ? session.LiftDay == LiftDay
            : string.Equals(HevyLocalKeys.Slug(session.Label), LabelSlug, StringComparison.Ordinal));
}
