namespace A2S.Domain.Enums;

/// <summary>
/// Current status of a workout program.
/// </summary>
public enum WorkoutStatus
{
    /// <summary>
    /// Workout has been created but not yet started.
    /// </summary>
    NotStarted = 1,

    /// <summary>
    /// Workout is actively being performed.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Workout has been temporarily paused.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Workout has been completed (all 21 weeks finished).
    /// </summary>
    Completed = 4
}
