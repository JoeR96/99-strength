using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a weight value with unit of measurement.
/// Immutable value object.
/// </summary>
public sealed class Weight : ValueObject
{
    public decimal Value { get; }
    public WeightUnit Unit { get; }

    private Weight(decimal value, WeightUnit unit)
    {
        CheckRule(value >= 0, "Weight cannot be negative");
        Value = value;
        Unit = unit;
    }

    public static Weight Kilograms(decimal value) => new(value, WeightUnit.Kilograms);
    public static Weight Pounds(decimal value) => new(value, WeightUnit.Pounds);

    public static Weight Create(decimal value, WeightUnit unit) => new(value, unit);

    /// <summary>
    /// Adds weight to this instance.
    /// </summary>
    public Weight Add(Weight other)
    {
        CheckRule(Unit == other.Unit, "Cannot add weights with different units");
        return new Weight(Value + other.Value, Unit);
    }

    /// <summary>
    /// Subtracts weight from this instance.
    /// </summary>
    public Weight Subtract(Weight other)
    {
        CheckRule(Unit == other.Unit, "Cannot subtract weights with different units");
        var newValue = Value - other.Value;
        CheckRule(newValue >= 0, "Resulting weight cannot be negative");
        return new Weight(newValue, Unit);
    }

    /// <summary>
    /// Rounds weight to the nearest increment (e.g., 2.5kg for barbell exercises).
    /// </summary>
    public Weight RoundToIncrement(decimal increment)
    {
        var rounded = Math.Round(Value / increment) * increment;
        return new Weight(rounded, Unit);
    }

    /// <summary>
    /// Converts this weight to the specified unit.
    /// </summary>
    public Weight ConvertTo(WeightUnit targetUnit)
    {
        if (Unit == targetUnit)
            return this;

        var converted = targetUnit == WeightUnit.Kilograms
            ? Value / 2.20462m  // pounds to kg
            : Value * 2.20462m; // kg to pounds

        return new Weight(converted, targetUnit);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }

    public override string ToString() => $"{Value}{(Unit == WeightUnit.Kilograms ? "kg" : "lbs")}";
}
