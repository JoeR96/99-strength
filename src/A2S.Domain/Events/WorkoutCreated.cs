using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a new workout is created.
/// </summary>
public sealed record WorkoutCreated : IDomainEvent
{
    public WorkoutId WorkoutId { get; }
    public string Name { get; }
    public ProgramVariant Variant { get; }
    public int ExerciseCount { get; }
    public DateTime OccurredOn { get; }

    public WorkoutCreated(WorkoutId workoutId, string name, ProgramVariant variant, int exerciseCount)
    {
        WorkoutId = workoutId;
        Name = name;
        Variant = variant;
        ExerciseCount = exerciseCount;
        OccurredOn = DateTime.UtcNow;
    }
}
