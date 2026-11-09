namespace A2S.Domain.Aggregates.Plan;

/// <summary>
/// The kind of a dated plan phase. A <see cref="Pause"/> (holiday, away) means scheduled sessions
/// are not counted as missed and the A2S2 week does not advance.
/// </summary>
public enum PhaseType
{
    Cut,
    Maintenance,
    Pause,
    Deload
}
