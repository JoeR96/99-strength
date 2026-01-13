using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a planned set for an exercise in a workout.
/// Contains the prescribed weight and target reps.
/// </summary>
public sealed class PlannedSet : ValueObject
{
    public int SetNumber { get; private init; }
    public Weight Weight { get; private init; } = null!;
    public int TargetReps { get; private init; }
    public bool IsAmrap { get; private init; }

    // EF Core constructor for JSON deserialization
    private PlannedSet()
    {
    }

    public PlannedSet(int setNumber, Weight weight, int targetReps, bool isAmrap = false)
    {
        CheckRule(setNumber > 0, "Set number must be greater than zero");
        CheckRule(targetReps > 0, "Target reps must be greater than zero");

        SetNumber = setNumber;
        Weight = weight;
        TargetReps = targetReps;
        IsAmrap = isAmrap;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SetNumber;
        yield return Weight;
        yield return TargetReps;
        yield return IsAmrap;
    }

    public override string ToString()
    {
        var amrapIndicator = IsAmrap ? "+" : "";
        return $"Set {SetNumber}: {Weight} × {TargetReps}{amrapIndicator}";
    }
}
