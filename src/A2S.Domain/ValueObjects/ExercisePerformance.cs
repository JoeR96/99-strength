using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents the performance for a single exercise on a given day.
/// Contains all completed sets and can be compared against planned sets.
/// </summary>
public sealed class ExercisePerformance : ValueObject
{
    public ExerciseId ExerciseId { get; private init; }
    public IReadOnlyList<CompletedSet> CompletedSets { get; private init; } = Array.Empty<CompletedSet>();
    public IReadOnlyList<PlannedSet> PlannedSets { get; private init; } = Array.Empty<PlannedSet>();
    public DateTime CompletedAt { get; private init; }

    // EF Core constructor for JSON deserialization
    private ExercisePerformance()
    {
        ExerciseId = new ExerciseId(Guid.Empty); // Temporary placeholder for EF Core
    }

    public ExercisePerformance(
        ExerciseId exerciseId,
        IEnumerable<PlannedSet> plannedSets,
        IEnumerable<CompletedSet> completedSets,
        DateTime? completedAt = null)
    {
        var plannedSetsList = plannedSets.ToList();
        var completedSetsList = completedSets.ToList();

        CheckRule(plannedSetsList.Any(), "At least one planned set is required");
        CheckRule(completedSetsList.Any(), "At least one completed set is required");
        CheckRule(
            completedSetsList.Count <= plannedSetsList.Count,
            "Cannot have more completed sets than planned sets"
        );

        ExerciseId = exerciseId;
        PlannedSets = plannedSetsList.AsReadOnly();
        CompletedSets = completedSetsList.AsReadOnly();
        CompletedAt = completedAt ?? DateTime.UtcNow;
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

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ExerciseId;
        foreach (var set in CompletedSets)
            yield return set;
        yield return CompletedAt;
    }
}
