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

/// <summary>
/// Strongly-typed ID for User aggregate root.
/// </summary>
public readonly record struct UserId(string Value);

/// <summary>
/// Strongly-typed ID for ExerciseDefinition entity (reference data).
/// </summary>
public readonly record struct ExerciseDefinitionId(Guid Value);
