using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a completed set with actual performance data.
/// </summary>
public sealed class CompletedSet : ValueObject
{
    public int SetNumber { get; private init; }
    public Weight Weight { get; private init; } = null!;
    public int ActualReps { get; private init; }
    public bool WasAmrap { get; private init; }

    // EF Core constructor for JSON deserialization
    private CompletedSet()
    {
    }

    public CompletedSet(int setNumber, Weight weight, int actualReps, bool wasAmrap = false)
    {
        CheckRule(setNumber > 0, "Set number must be greater than zero");
        CheckRule(actualReps >= 0, "Actual reps cannot be negative");

        SetNumber = setNumber;
        Weight = weight;
        ActualReps = actualReps;
        WasAmrap = wasAmrap;
    }

    /// <summary>
    /// Calculates the rep delta compared to a planned set.
    /// Positive means exceeded target, negative means fell short.
    /// </summary>
    /// <remarks>
    /// Note: Set numbers do NOT need to match. This is intentional because:
    /// 1. When pulling from Hevy, the user may have done fewer/more sets than planned
    /// 2. The AMRAP comparison is about reps vs target, not set position
    /// 3. Example: User completes 4 sets (AMRAP on set 4), but week plan calls for 5 sets
    ///    We still want to compare the AMRAP reps to the target reps for progression.
    /// </remarks>
    public int CalculateDelta(PlannedSet plannedSet)
    {
        return ActualReps - plannedSet.TargetReps;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SetNumber;
        yield return Weight;
        yield return ActualReps;
        yield return WasAmrap;
    }

    public override string ToString()
    {
        var amrapIndicator = WasAmrap ? " (AMRAP)" : "";
        return $"Set {SetNumber}: {Weight} × {ActualReps}{amrapIndicator}";
    }
}
