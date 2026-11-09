namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for the HabitDefinition aggregate root.</summary>
public readonly record struct HabitId(Guid Value);

/// <summary>Strongly-typed ID for a HabitLog (one habit, one local date).</summary>
public readonly record struct HabitLogId(Guid Value);

/// <summary>Strongly-typed ID for a BodyWeightEntry (one user, one local date).</summary>
public readonly record struct BodyWeightEntryId(Guid Value);
