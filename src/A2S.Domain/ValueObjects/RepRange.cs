using A2S.Domain.Common;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a rep range for accessory exercises (e.g., 8-12).
/// Minimum: Below this triggers a regression.
/// Maximum: Hitting this on all sets triggers progression.
/// </summary>
public sealed class RepRange : ValueObject
{
    public int Minimum { get; private init; }
    public int Maximum { get; private init; }

    // EF Core constructor for JSON deserialization
    private RepRange()
    {
    }

    private RepRange(int minimum, int maximum)
    {
        CheckRule(minimum > 0, "Minimum reps must be greater than zero");
        CheckRule(maximum >= minimum, "Maximum must be greater than or equal to minimum");
        CheckRule(maximum - minimum <= 10, "Rep range span cannot exceed 10 reps");

        Minimum = minimum;
        Maximum = maximum;
    }

    public static RepRange Create(int minimum, int maximum)
    {
        return new RepRange(minimum, maximum);
    }

    /// <summary>
    /// Common rep ranges for accessories.
    /// </summary>
    public static class Common
    {
        public static RepRange Low => new(4, 8);           // 4-8 (strength focus)
        public static RepRange MediumLow => new(6, 10);    // 6-10
        public static RepRange Medium => new(8, 12);       // 8-12 (most common)
        public static RepRange MediumHigh => new(10, 15);  // 10-15
        public static RepRange High => new(12, 20);        // 12-20 (endurance focus)
    }

    /// <summary>
    /// Checks if a rep count is below the minimum threshold.
    /// </summary>
    public bool IsBelowMinimum(int actualReps) => actualReps < Minimum;

    /// <summary>
    /// Checks if a rep count meets or exceeds the maximum.
    /// </summary>
    public bool MeetsMaximum(int actualReps) => actualReps >= Maximum;

    /// <summary>
    /// Checks if a rep count is within the acceptable range.
    /// </summary>
    public bool IsInRange(int actualReps) => actualReps >= Minimum && actualReps <= Maximum;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Minimum;
        yield return Maximum;
    }

    public override string ToString() => $"{Minimum}-{Maximum}";
}
