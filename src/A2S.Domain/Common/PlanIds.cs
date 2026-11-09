namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for the TrainingPlan aggregate root.</summary>
public readonly record struct TrainingPlanId(Guid Value);

/// <summary>Strongly-typed ID for the CardioProtocol aggregate root.</summary>
public readonly record struct CardioProtocolId(Guid Value);

/// <summary>Strongly-typed ID for the MobilityRoutine aggregate root.</summary>
public readonly record struct MobilityRoutineId(Guid Value);

/// <summary>Strongly-typed ID for a SessionCompletion entity owned by a TrainingPlan.</summary>
public readonly record struct SessionCompletionId(Guid Value);
