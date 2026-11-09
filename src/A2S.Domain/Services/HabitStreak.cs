namespace A2S.Domain.Services;

/// <summary>
/// The state of a person's habit streak.
/// </summary>
/// <param name="Current">Counted days in the run that is still alive today.</param>
/// <param name="Best">The longest run under the same rule across all history.</param>
/// <param name="LastCountedDay">The most recent day on which every active habit reached its minimum.</param>
/// <param name="TodayCounted">Whether today already counts. False never breaks the streak: the day is still open.</param>
/// <param name="GraceUsed">Whether the current run already contains a forgiven miss; another miss straight after one would reset it.</param>
public sealed record HabitStreak(int Current, int Best, DateOnly? LastCountedDay, bool TodayCounted, bool GraceUsed)
{
    public static readonly HabitStreak None = new(0, 0, null, false, false);
}
