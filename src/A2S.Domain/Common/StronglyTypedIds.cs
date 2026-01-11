namespace A2S.Domain.Common;

/// <summary>
/// Strongly-typed ID for Workout aggregate root.
/// </summary>
public readonly record struct WorkoutId(Guid Value);

/// <summary>
/// Strongly-typed ID for Exercise entity.
/// </summary>
public readonly record struct ExerciseId(Guid Value);

/// <summary>
/// Strongly-typed ID for ExerciseProgression entity.
/// </summary>
public readonly record struct ExerciseProgressionId(Guid Value);
