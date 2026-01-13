using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a Training Max (TM) for an exercise.
/// Used to calculate working weights based on intensity percentages.
/// </summary>
public sealed class TrainingMax : ValueObject
{
    public decimal Value { get; private init; }
    public WeightUnit Unit { get; private init; }

    // EF Core constructor for JSON deserialization
    private TrainingMax()
    {
    }

    private TrainingMax(decimal value, WeightUnit unit)
    {
        CheckRule(value > 0, "Training Max must be greater than zero");
        Value = value;
        Unit = unit;
    }

    public static TrainingMax Create(decimal value, WeightUnit unit)
    {
        return new TrainingMax(value, unit);
    }

    /// <summary>
    /// Calculates the working weight based on an intensity percentage (e.g., 0.70 for 70%).
    /// Result is rounded to nearest 2.5kg/5lbs.
    /// </summary>
    public Weight CalculateWorkingWeight(decimal intensityPercentage)
    {
        CheckRule(intensityPercentage > 0 && intensityPercentage <= 1.5m,
            "Intensity percentage must be between 0 and 1.5 (0-150%)");

        var calculated = Value * intensityPercentage;
        var increment = Unit == WeightUnit.Kilograms ? 2.5m : 5m;
        var rounded = Math.Round(calculated / increment) * increment;

        return Weight.Create(rounded, Unit);
    }

    /// <summary>
    /// Applies an adjustment to the Training Max (e.g., from AMRAP performance).
    /// </summary>
    public TrainingMax ApplyAdjustment(TrainingMaxAdjustment adjustment)
    {
        var newValue = adjustment.Type switch
        {
            AdjustmentType.Percentage => Value * (1 + adjustment.Amount),
            AdjustmentType.Absolute => Value + adjustment.Amount,
            AdjustmentType.None => Value,
            _ => throw new ArgumentException($"Unknown adjustment type: {adjustment.Type}")
        };

        // Round to nearest 2.5kg/5lbs
        var increment = Unit == WeightUnit.Kilograms ? 2.5m : 5m;
        newValue = Math.Round(newValue / increment) * increment;

        CheckRule(newValue > 0, "Adjusted Training Max must be greater than zero");

        return new TrainingMax(newValue, Unit);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }

    public override string ToString() => $"{Value}{(Unit == WeightUnit.Kilograms ? "kg" : "lbs")} TM";
}

/// <summary>
/// Represents an adjustment to a Training Max.
/// </summary>
public sealed class TrainingMaxAdjustment : ValueObject
{
    public AdjustmentType Type { get; private init; }
    public decimal Amount { get; private init; }

    // EF Core constructor for JSON deserialization
    private TrainingMaxAdjustment()
    {
    }

    private TrainingMaxAdjustment(AdjustmentType type, decimal amount)
    {
        Type = type;
        Amount = amount;
    }

    public static TrainingMaxAdjustment None => new(AdjustmentType.None, 0);
    public static TrainingMaxAdjustment Percentage(decimal percentage) => new(AdjustmentType.Percentage, percentage);
    public static TrainingMaxAdjustment Absolute(decimal amount) => new(AdjustmentType.Absolute, amount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Amount;
    }

    public override string ToString()
    {
        return Type switch
        {
            AdjustmentType.None => "No adjustment",
            AdjustmentType.Percentage => $"{Amount:P1}",
            AdjustmentType.Absolute => $"{(Amount >= 0 ? "+" : "")}{Amount}",
            _ => "Unknown"
        };
    }
}

public enum AdjustmentType
{
    None,
    Percentage,
    Absolute
}
