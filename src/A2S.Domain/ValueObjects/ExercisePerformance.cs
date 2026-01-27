using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents the performance for a single exercise on a given day.
/// Contains all completed sets and can be compared against planned sets.
/// </summary>
public sealed class ExercisePerformance : ValueObject
{
    public ExerciseId ExerciseId { get; private init; }

    // Use List<T> for EF Core JSON deserialization compatibility (arrays are fixed-size)
    public List<CompletedSet> CompletedSets { get; private init; } = new();
    public List<PlannedSet> PlannedSets { get; private init; } = new();
    public DateTime CompletedAt { get; private init; }

    /// <summary>
    /// When true, progression rules should be skipped for this exercise.
    /// Used for temporary substitutions where the user performed a different exercise
    /// but wants to preserve the original exercise's progression state.
    /// </summary>
    public bool SkipProgression { get; private init; }

    // EF Core constructor for JSON deserialization
    private ExercisePerformance()
    {
        ExerciseId = new ExerciseId(Guid.Empty); // Temporary placeholder for EF Core
    }

    public ExercisePerformance(
        ExerciseId exerciseId,
        IEnumerable<PlannedSet> plannedSets,
        IEnumerable<CompletedSet> completedSets,
        DateTime? completedAt = null,
        bool skipProgression = false)
    {
        var plannedSetsList = plannedSets.ToList();
        var completedSetsList = completedSets.ToList();

        CheckRule(plannedSetsList.Any(), "At least one planned set is required");
        CheckRule(completedSetsList.Any(), "At least one completed set is required");
        // Note: We no longer enforce that completed sets must equal planned sets.
        // This allows flexibility when:
        // 1. User pulls data from Hevy with different set counts
        // 2. User adds extra sets or skips sets during workout
        // 3. Frontend/backend week parameter calculations differ slightly
        // The progression logic will use whatever sets are provided.

        ExerciseId = exerciseId;
        PlannedSets = plannedSetsList;
        CompletedSets = completedSetsList;
        CompletedAt = completedAt ?? DateTime.UtcNow;
        SkipProgression = skipProgression;
    }

    /// <summary>
    /// Gets the delta for the AMRAP set (if applicable).
    /// Returns 0 if no AMRAP set was performed.
    /// </summary>
    public int GetAmrapDelta()
    {
        var amrapPlanned = PlannedSets.LastOrDefault(s => s.IsAmrap);
        if (amrapPlanned == null)
            return 0;

        var amrapCompleted = CompletedSets.LastOrDefault(s => s.WasAmrap);
        if (amrapCompleted == null)
            return 0;

        return amrapCompleted.CalculateDelta(amrapPlanned);
    }

    /// <summary>
    /// Checks if all sets hit the maximum reps in the rep range.
    /// </summary>
    public bool AllSetsHitMax(RepRange repRange)
    {
        return CompletedSets.All(s => repRange.MeetsMaximum(s.ActualReps));
    }

    /// <summary>
    /// Checks if any set fell below the minimum reps in the rep range.
    /// </summary>
    public bool AnySetsBelowMin(RepRange repRange)
    {
        return CompletedSets.Any(s => repRange.IsBelowMinimum(s.ActualReps));
    }

    /// <summary>
    /// Gets the total reps completed across all sets.
    /// Used for MinimalSets progression strategy.
    /// </summary>
    public int GetTotalRepsCompleted()
    {
        return CompletedSets.Sum(s => s.ActualReps);
    }

    /// <summary>
    /// Gets the number of sets used (completed).
    /// Used for MinimalSets progression strategy.
    /// </summary>
    public int GetSetsUsed()
    {
        return CompletedSets.Count;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExerciseId;
        foreach (var set in CompletedSets)
            yield return set;
        yield return CompletedAt;
    }
}
