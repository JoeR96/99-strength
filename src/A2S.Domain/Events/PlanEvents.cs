using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>Raised when a training plan is created; its start is the coming Monday.</summary>
public sealed record TrainingPlanStarted : IDomainEvent
{
    public TrainingPlanId PlanId { get; }
    public UserId UserId { get; }
    public DateOnly StartDate { get; }
    public DateTime OccurredOn { get; }

    public TrainingPlanStarted(TrainingPlanId planId, UserId userId, DateOnly startDate)
    {
        PlanId = planId;
        UserId = userId;
        StartDate = startDate;
        OccurredOn = DateTime.UtcNow;
    }
}

/// <summary>Raised when a scheduled session is recorded as done (by hand or from Hevy).</summary>
public sealed record SessionCompleted : IDomainEvent
{
    public TrainingPlanId PlanId { get; }
    public SessionCompletionId CompletionId { get; }
    public DateOnly Date { get; }
    public SessionKind Kind { get; }
    public CompletionSource Source { get; }
    public DateTime OccurredOn { get; }

    public SessionCompleted(TrainingPlanId planId, SessionCompletionId completionId, DateOnly date, SessionKind kind, CompletionSource source, DateTimeOffset recordedAt)
    {
        PlanId = planId;
        CompletionId = completionId;
        Date = date;
        Kind = kind;
        Source = source;
        OccurredOn = recordedAt.UtcDateTime;
    }
}

/// <summary>Raised when a dated phase (cut, maintenance, pause, deload) is added to a plan.</summary>
public sealed record PhaseAdded : IDomainEvent
{
    public TrainingPlanId PlanId { get; }
    public PhaseType Type { get; }
    public DateOnly From { get; }
    public DateOnly To { get; }
    public DateTime OccurredOn { get; }

    public PhaseAdded(TrainingPlanId planId, PhaseType type, DateOnly from, DateOnly to)
    {
        PlanId = planId;
        Type = type;
        From = from;
        To = to;
        OccurredOn = DateTime.UtcNow;
    }
}
