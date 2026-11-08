namespace A2S.Domain.Common;

/// <summary>Strongly-typed ID for a HevyLink (local key ↔ Hevy id).</summary>
public readonly record struct HevyLinkId(Guid Value);

/// <summary>Hevy's own id for a workout; the primary key of a HevyWorkoutSnapshot and the dedupe key for pulls.</summary>
public readonly record struct HevyWorkoutId(string Value);
