using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Single source of truth for the AMRAP delta → Training Max adjustment mapping.
/// Reference: A2S2 business rules — RTF (Reps To Failure) progression table.
/// </summary>
public static class AmrapDeltaTable
{
    /// <summary>
    /// Returns the Training Max adjustment for a given AMRAP delta
    /// (actual reps minus rep-out target).
    /// </summary>
    /// <remarks>
    /// Delta →  Adjustment
    /// ≥ +5  →  +3.0%
    ///   +4  →  +2.0%
    ///   +3  →  +1.5%
    ///   +2  →  +1.0%
    ///   +1  →  +0.5%
    ///    0  →   0%
    ///   -1  →  -2.0%
    /// ≤ -2  →  -5.0%
    /// </remarks>
    public static TrainingMaxAdjustment GetAdjustment(int delta)
    {
        return delta switch
        {
            >= 5 => TrainingMaxAdjustment.Percentage(0.03m),
            4 => TrainingMaxAdjustment.Percentage(0.02m),
            3 => TrainingMaxAdjustment.Percentage(0.015m),
            2 => TrainingMaxAdjustment.Percentage(0.01m),
            1 => TrainingMaxAdjustment.Percentage(0.005m),
            0 => TrainingMaxAdjustment.None,
            -1 => TrainingMaxAdjustment.Percentage(-0.02m),
            _ => TrainingMaxAdjustment.Percentage(-0.05m)
        };
    }
}
