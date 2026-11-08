using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Mobility;

/// <summary>What a set of a mobility drill is measured in.</summary>
public enum DoseUnit
{
    Reps,
    Seconds,
    Breaths
}

/// <summary>How much one set of a drill is: 5 breaths, 30 seconds, 10 reps. Immutable value object.</summary>
public sealed record DrillDose
{
    public DoseUnit Unit { get; }
    public int Amount { get; }

    public DrillDose(DoseUnit unit, int amount)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleViolationException("Dose amount must be positive");
        }

        Unit = unit;
        Amount = amount;
    }

    public static DrillDose Reps(int amount) => new(DoseUnit.Reps, amount);
    public static DrillDose Seconds(int amount) => new(DoseUnit.Seconds, amount);
    public static DrillDose Breaths(int amount) => new(DoseUnit.Breaths, amount);
}
