using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a completed set with actual performance data.
/// </summary>
public sealed class CompletedSet : ValueObject
{
    public int SetNumber { get; }
    public Weight Weight { get; }
    public int ActualReps { get; }
    public bool WasAmrap { get; }

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
    public int CalculateDelta(PlannedSet plannedSet)
    {
        CheckRule(plannedSet.SetNumber == SetNumber, "Set numbers must match");
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
