using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// One slot in the weekly schedule template. A weekday may hold more than one session
/// (Wednesday = cardio + mobility). Immutable value object.
/// </summary>
public sealed record ScheduledSession
{
    public DayOfWeek Day { get; }
    public SessionKind Kind { get; }
    public DayNumber? LiftDay { get; }
    public CardioProtocolId? CardioProtocolId { get; }
    public MobilityRoutineId? MobilityRoutineId { get; }
    public string Label { get; }

    public ScheduledSession(
        DayOfWeek day,
        SessionKind kind,
        DayNumber? liftDay,
        CardioProtocolId? cardioProtocolId,
        MobilityRoutineId? mobilityRoutineId,
        string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new BusinessRuleViolationException("Session label is required");
        }

        if (kind == SessionKind.Lift && liftDay is null)
        {
            throw new BusinessRuleViolationException("A lift session needs the A2S2 day it trains");
        }

        if (kind != SessionKind.Lift && liftDay is not null)
        {
            throw new BusinessRuleViolationException("Only lift sessions carry a lift day");
        }

        if (kind != SessionKind.Cardio && cardioProtocolId is not null)
        {
            throw new BusinessRuleViolationException("Only cardio sessions carry a cardio protocol");
        }

        if (kind != SessionKind.Mobility && mobilityRoutineId is not null)
        {
            throw new BusinessRuleViolationException("Only mobility sessions carry a mobility routine");
        }

        Day = day;
        Kind = kind;
        LiftDay = liftDay;
        CardioProtocolId = cardioProtocolId;
        MobilityRoutineId = mobilityRoutineId;
        Label = label.Trim();
    }

    public static ScheduledSession Lift(DayOfWeek day, DayNumber liftDay, string label) =>
        new(day, SessionKind.Lift, liftDay, null, null, label);

    /// <summary>A cardio slot; the protocol may be left open ("C25K run or LISS").</summary>
    public static ScheduledSession Cardio(DayOfWeek day, CardioProtocolId? protocol, string label) =>
        new(day, SessionKind.Cardio, null, protocol, null, label);

    public static ScheduledSession Mobility(DayOfWeek day, MobilityRoutineId? routine, string label) =>
        new(day, SessionKind.Mobility, null, null, routine, label);

    public static ScheduledSession Rest(DayOfWeek day, string label = "Rest") =>
        new(day, SessionKind.Rest, null, null, null, label);

    /// <summary>Monday = 0 … Sunday = 6, for ordering the training week.</summary>
    public int DayOrder => ((int)Day + 6) % 7;
}
