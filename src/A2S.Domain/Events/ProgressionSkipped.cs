using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when an exercise's progression is skipped for a week.
/// </summary>
public sealed record ProgressionSkipped : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public Guid ExerciseId { get; }
    public string ExerciseName { get; }
    public int WeekNumber { get; }
    public string Reason { get; }
    public DateTime OccurredOn { get; }

    public ProgressionSkipped(
        WorkoutId workoutId,
        Guid exerciseId,
        string exerciseName,
        int weekNumber,
        string reason)
    {
        WorkoutId = workoutId;
        ExerciseId = exerciseId;
        ExerciseName = exerciseName;
        WeekNumber = weekNumber;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }
}
