namespace A2S.Domain.Enums;

/// <summary>
/// The tier of exercise within the A2S program, determining rep ranges and intensity tables.
/// </summary>
public enum ProgramTier
{
    /// <summary>
    /// Primary/T1 lifts (e.g., Back Squat, Bench Press).
    /// Rep range: 5 → 1 across blocks.
    /// </summary>
    Primary = 1,

    /// <summary>
    /// Auxiliary/T2 lifts (e.g., Front Squat, Incline Bench).
    /// Rep range: 7 → 2 across blocks.
    /// </summary>
    Auxiliary = 2
}
