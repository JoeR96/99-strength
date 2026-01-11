using A2S.Domain.Common;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Events;

/// <summary>
/// Domain event raised when a Training Max is adjusted (either automatically or manually).
/// </summary>
public sealed record TrainingMaxAdjusted : IDomainEvent
{
    public ExerciseProgressionId ProgressionId { get; }
    public TrainingMax NewTrainingMax { get; }
    public TrainingMaxAdjustment Adjustment { get; }
    public int? AmrapDelta { get; }
    public string? Reason { get; }
    public DateTime OccurredOn { get; }

    public TrainingMaxAdjusted(
        ExerciseProgressionId progressionId,
        TrainingMax newTrainingMax,
        TrainingMaxAdjustment adjustment,
        int? amrapDelta = null,
        string? reason = null)
    {
        ProgressionId = progressionId;
        NewTrainingMax = newTrainingMax;
        Adjustment = adjustment;
        AmrapDelta = amrapDelta;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }
}
