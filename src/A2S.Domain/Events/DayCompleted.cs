using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a training day is completed.
/// </summary>
public sealed record DayCompleted : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public DayNumber Day { get; }
    public int WeekNumber { get; }
    public int ExerciseCount { get; }
    public DateTime OccurredOn { get; }

    public DayCompleted(WorkoutId workoutId, DayNumber day, int weekNumber, int exerciseCount)
    {
        WorkoutId = workoutId;
        Day = day;
        WeekNumber = weekNumber;
        ExerciseCount = exerciseCount;
        OccurredOn = DateTime.UtcNow;
    }
}
