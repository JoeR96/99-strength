using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when progressing to a new week.
/// </summary>
public sealed record WeekProgressed : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public int PreviousWeek { get; }
    public int NewWeek { get; }
    public int NewBlock { get; }
    public bool IsDeloadWeek { get; }
    public DateTime OccurredOn { get; }

    public WeekProgressed(
        WorkoutId workoutId,
        int previousWeek,
        int newWeek,
        int newBlock,
        bool isDeloadWeek)
    {
        WorkoutId = workoutId;
        PreviousWeek = previousWeek;
        NewWeek = newWeek;
        NewBlock = newBlock;
        IsDeloadWeek = isDeloadWeek;
        OccurredOn = DateTime.UtcNow;
    }
}
