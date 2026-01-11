using A2S.Domain.Common;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Abstract base class for exercise progression strategies.
/// Implements the Strategy pattern for different progression methodologies.
/// Persisted using TPH (Table Per Hierarchy) in EF Core.
/// </summary>
public abstract class ExerciseProgression : Entity<ExerciseProgressionId>
{
    /// <summary>
    /// Discriminator column for TPH inheritance mapping.
    /// </summary>
    public string ProgressionType { get; protected set; } = string.Empty;

    protected ExerciseProgression()
    {
    }

    protected ExerciseProgression(ExerciseProgressionId id, string progressionType)
        : base(id)
    {
        ProgressionType = progressionType;
    }

    /// <summary>
    /// Calculates the planned sets for a given week and block.
    /// </summary>
    public abstract IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber);

    /// <summary>
    /// Applies performance results and updates progression state (e.g., adjust TM, change sets).
    /// </summary>
    public abstract void ApplyPerformanceResult(ExercisePerformance performance);

    /// <summary>
    /// Gets a summary of the current progression state for display purposes.
    /// </summary>
    public abstract ProgressionSummary GetSummary();
}

/// <summary>
/// Summary of progression state for UI display.
/// </summary>
public sealed record ProgressionSummary
{
    public string Type { get; init; } = string.Empty;
    public Dictionary<string, string> Details { get; init; } = new();
}
