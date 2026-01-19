namespace A2S.Domain.Enums;

/// <summary>
/// The variant of the A2S program being run.
/// Represents the number of training days per week.
/// </summary>
public enum ProgramVariant
{
    /// <summary>
    /// 4-day per week program variant.
    /// </summary>
    FourDay = 4,

    /// <summary>
    /// 5-day per week program variant.
    /// Traditional bodybuilding split with mixed progression strategies.
    /// </summary>
    FiveDay = 5,

    /// <summary>
    /// 6-day per week program variant (high frequency).
    /// </summary>
    SixDay = 6
}
