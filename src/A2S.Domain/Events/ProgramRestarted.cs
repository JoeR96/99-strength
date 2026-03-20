using A2S.Domain.Common;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a completed program is restarted via UpdateBlockSequence.
/// Historical activities are archived rather than destroyed.
/// </summary>
public sealed record ProgramRestarted : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public int ArchivedActivitiesCount { get; }
    public DateTime OccurredOn { get; }

    public ProgramRestarted(WorkoutId workoutId, int archivedActivitiesCount)
    {
        WorkoutId = workoutId;
        ArchivedActivitiesCount = archivedActivitiesCount;
        OccurredOn = DateTime.UtcNow;
    }
}
