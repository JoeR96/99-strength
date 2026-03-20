using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a completed day is undone.
/// </summary>
public sealed record CompletionUndone : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public DayNumber Day { get; }
    public int WeekNumber { get; }
    public DateTime OccurredOn { get; }

    public CompletionUndone(WorkoutId workoutId, DayNumber day, int weekNumber)
    {
        WorkoutId = workoutId;
        Day = day;
        WeekNumber = weekNumber;
        OccurredOn = DateTime.UtcNow;
    }
}
