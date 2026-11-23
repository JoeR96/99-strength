using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Services;

/// <summary>
/// Estimates a starting training max from a recent set: Epley e1RM (weight × (1 + reps / 30), the
/// formula the exercise history chart uses) × 90 %, the app's "TM ≈ 90 % of 1RM" convention, rounded
/// to the nearest 2.5 kg / 5 lb. Only used to seed a pending TM; it never adjusts one.
/// </summary>
public static class TrainingMaxEstimator
{
    public const decimal TrainingMaxFraction = 0.90m;
    public const int MaximumReps = 12;

    /// <summary>Epley e1RM: weight � (1 + reps / 30); a single is taken at face value.</summary>
    public static decimal EstimateOneRepMax(decimal weight, int reps) =>
        reps == 1 ? weight : weight * (1 + reps / 30m);

    public static TrainingMax FromRecentSet(Weight weight, int reps)
    {
        ArgumentNullException.ThrowIfNull(weight);
        if (weight.Value <= 0)
        {
            throw new BusinessRuleViolationException("The recent set needs a weight above zero");
        }

        if (reps is < 1 or > MaximumReps)
        {
            throw new BusinessRuleViolationException($"Use a recent set of 1 to {MaximumReps} reps to estimate a training max");
        }

        var oneRepMax = EstimateOneRepMax(weight.Value, reps);
        var increment = weight.Unit == WeightUnit.Kilograms ? 2.5m : 5m;
        var rounded = Math.Round(oneRepMax * TrainingMaxFraction / increment, MidpointRounding.AwayFromZero) * increment;
        return TrainingMax.Create(Math.Max(increment, rounded), weight.Unit);
    }
}
