namespace A2S.Domain.Enums;

/// <summary>
/// The variant of the A2S program being run.
/// </summary>
public enum ProgramVariant
{
    /// <summary>
    /// Hypertrophy-focused variant with higher volume (21 weeks total).
    /// </summary>
    Hypertrophy = 1,

    /// <summary>
    /// Strength-focused variant with lower reps (21 weeks total).
    /// </summary>
    Strength = 2
}
