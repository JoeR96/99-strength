using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a workout transitions from NotStarted to Active.
/// </summary>
public sealed record WorkoutStarted : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public DateTime OccurredOn { get; }

    public WorkoutStarted(WorkoutId workoutId)
    {
        WorkoutId = workoutId;
        OccurredOn = DateTime.UtcNow;
    }
}
