namespace A2S.Domain.Enums;

/// <summary>
/// Type of equipment used for an exercise.
/// Affects weight increments in Reps Per Set progression.
/// </summary>
public enum EquipmentType
{
    /// <summary>
    /// Barbell exercises (increment: 2.5kg).
    /// </summary>
    Barbell = 1,

    /// <summary>
    /// Dumbbell exercises (increment: 1kg for light weights, 2kg for heavier).
    /// </summary>
    Dumbbell = 2,

    /// <summary>
    /// Cable machine exercises (increment: 2.5kg).
    /// </summary>
    Cable = 3,

    /// <summary>
    /// Plate-loaded or selectorized machines (increment: 2.5kg).
    /// </summary>
    Machine = 4,

    /// <summary>
    /// Bodyweight exercises (no weight increments).
    /// </summary>
    Bodyweight = 5
}
