namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for a PushSubscription (one browser/device of one user).</summary>
public readonly record struct PushSubscriptionId(Guid Value);

/// <summary>Strongly-typed ID for a PushNudgeLog row (one nudge kind, one user, one local date).</summary>
public readonly record struct PushNudgeLogId(Guid Value);

/// <summary>Strongly-typed ID for a FinisherSchedule (the phase nudges of one guided finisher).</summary>
public readonly record struct FinisherScheduleId(Guid Value);
