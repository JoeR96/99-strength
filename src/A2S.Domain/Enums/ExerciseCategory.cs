namespace A2S.Domain.Enums;

/// <summary>
/// Category of exercise determining its progression strategy.
/// </summary>
public enum ExerciseCategory
{
    /// <summary>
    /// Main compound lifts (e.g., Squat, Bench, Deadlift, OHP).
    /// Uses linear progression with AMRAP.
    /// </summary>
    MainLift = 1,

    /// <summary>
    /// Auxiliary compound lifts (e.g., Front Squat, Incline Bench).
    /// Uses linear progression, optionally with AMRAP.
    /// </summary>
    Auxiliary = 2,

    /// <summary>
    /// Accessory exercises (e.g., Rows, Curls, Lateral Raises).
    /// Uses Reps Per Set progression.
    /// </summary>
    Accessory = 3
}
