namespace A2S.Domain.Enums;

/// <summary>
/// One bonus set on one side of a unilateral exercise (e.g. split squat "extra set left"). Bonus volume
/// only: it is logged and sent to Hevy but never takes part in progression.
/// </summary>
public enum ExtraSetSide
{
    None = 0,
    Left = 1,
    Right = 2
}
