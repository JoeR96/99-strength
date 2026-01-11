using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a workout program is completed (all 21 weeks finished).
/// </summary>
public sealed record WorkoutCompleted : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public DateTime CompletedAt { get; }
    public DateTime OccurredOn { get; }

    public WorkoutCompleted(WorkoutId workoutId, DateTime completedAt)
    {
        WorkoutId = workoutId;
        CompletedAt = completedAt;
        OccurredOn = DateTime.UtcNow;
    }
}
