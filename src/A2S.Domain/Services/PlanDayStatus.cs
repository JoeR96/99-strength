using A2S.Domain.Aggregates.Plan;

namespace A2S.Domain.Services;

/// <summary>Where a scheduled session stands on a given local date.</summary>
public enum SessionStatus
{
    Planned,
    Done,
    Missed,
    Paused,
    Rest
}

/// <summary>A scheduled session on a date with its status and, when done by hand or Hevy, its completion.</summary>
public sealed record PlanDaySession(ScheduledSession Session, SessionStatus Status, SessionCompletion? Completion);

/// <summary>One calendar day of the plan: the phase it falls in and every session scheduled for it.</summary>
public sealed record PlanDay(DateOnly Date, PhaseType? Phase, IReadOnlyList<PlanDaySession> Sessions);
