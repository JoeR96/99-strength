using System.Globalization;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Services;

/// <summary>One logged set with the local (Europe/London) date it was done on. Weight in kg.</summary>
public sealed record DatedSet(DateOnly Date, decimal WeightKg, int Reps, bool IsWarmup = false);

/// <summary>What a suggestion seeds: an A2S2 training max, or a reps-per-set starting weight.</summary>
public enum StartingNumberKind
{
    TrainingMax,
    StartingWeight
}

/// <summary>The set a suggestion rests on, its Epley e1RM and the window searched.</summary>
public sealed record StrengthEvidence(DatedSet Set, decimal EstimatedOneRepMaxKg, DateOnly WindowStart, DateOnly WindowEnd);

/// <summary>A suggested starting number with the evidence behind it and a one-line reason.</summary>
public sealed record StartingNumberSuggestion(
    StartingNumberKind Kind,
    decimal ValueKg,
    StrengthEvidence Evidence,
    bool Stale,
    int StalePenaltyPercent,
    string Reason);

/// <summary>
/// Suggests starting numbers from logged history. Best Epley e1RM (via
/// <see cref="TrainingMaxEstimator.EstimateOneRepMax"/>; working sets of 1–12 reps only) within the last
/// <see cref="WindowDays"/> days. With nothing that recent, the best set of the most recent session is
/// used less <see cref="StalePenaltyPerPeriodPercent"/> % per 3 months since (capped), flagged stale.
/// A TM is <see cref="TrainingMaxEstimator.TrainingMaxFraction"/> × e1RM rounded down to 2.5 kg; a
/// reps-per-set weight is the load for the middle of the rep range, rounded down to a weight the
/// equipment can actually be set to. Suggestions only; the person always has the last word.
/// </summary>
public static class TrainingMaxSuggester
{
    public const int WindowDays = 90;
    public const int StalePeriodDays = 90;
    public const int StalePenaltyPerPeriodPercent = 5;
    public const int MaxStalePenaltyPercent = 20;
    public const decimal BarbellIncrementKg = 2.5m;

    public static StartingNumberSuggestion? ForTrainingMax(IEnumerable<DatedSet> sets, DateOnly today) =>
        Suggest(sets, today, StartingNumberKind.TrainingMax, e1Rm =>
            RoundDown(e1Rm * TrainingMaxEstimator.TrainingMaxFraction, BarbellIncrementKg));

    public static StartingNumberSuggestion? ForStartingWeight(
        IEnumerable<DatedSet> sets, DateOnly today, EquipmentType equipment, RepRange repRange)
    {
        ArgumentNullException.ThrowIfNull(repRange);
        if (equipment == EquipmentType.Bodyweight)
        {
            return null;
        }

        var midReps = (repRange.Minimum + repRange.Maximum) / 2m;
        return Suggest(sets, today, StartingNumberKind.StartingWeight, e1Rm =>
            RoundDownForEquipment(e1Rm / (1 + midReps / 30m), equipment));
    }

    /// <summary>
    /// Rounds down to a settable weight, matching the progression increments: dumbbells in 1 kg steps
    /// below 10 kg and even sizes from 10 kg; everything else in 2.5 kg.
    /// </summary>
    public static decimal RoundDownForEquipment(decimal weightKg, EquipmentType equipment)
    {
        if (equipment == EquipmentType.Dumbbell)
        {
            return weightKg >= 10m ? Math.Floor(weightKg / 2m) * 2m : Math.Max(1m, Math.Floor(weightKg));
        }

        return RoundDown(weightKg, BarbellIncrementKg);
    }

    private static StartingNumberSuggestion? Suggest(
        IEnumerable<DatedSet> sets, DateOnly today, StartingNumberKind kind, Func<decimal, decimal> fromE1Rm)
    {
        ArgumentNullException.ThrowIfNull(sets);
        var working = sets
            .Where(s => !s.IsWarmup && s.WeightKg > 0 && s.Reps is >= 1 and <= TrainingMaxEstimator.MaximumReps && s.Date <= today)
            .ToList();
        if (working.Count == 0)
        {
            return null;
        }

        var windowStart = today.AddDays(-WindowDays);
        var recent = working.Where(s => s.Date >= windowStart).ToList();
        var stale = recent.Count == 0;
        var candidates = stale ? working.Where(s => s.Date == working.Max(w => w.Date)).ToList() : recent;

        var best = candidates
            .OrderByDescending(s => TrainingMaxEstimator.EstimateOneRepMax(s.WeightKg, s.Reps))
            .ThenByDescending(s => s.Date)
            .First();
        var e1Rm = Math.Round(TrainingMaxEstimator.EstimateOneRepMax(best.WeightKg, best.Reps), 1);

        var penalty = 0;
        if (stale)
        {
            var daysSince = today.DayNumber - best.Date.DayNumber;
            penalty = Math.Min(MaxStalePenaltyPercent, Math.Max(1, daysSince / StalePeriodDays) * StalePenaltyPerPeriodPercent);
        }

        var value = fromE1Rm(e1Rm * (100 - penalty) / 100m);
        if (value <= 0)
        {
            return null;
        }

        var evidence = new StrengthEvidence(best, e1Rm, windowStart, today);
        var from = $"{Kg(best.WeightKg)} kg × {best.Reps} on {best.Date.ToString("d MMM", CultureInfo.InvariantCulture)}";
        var reason = stale
            ? $"Older history — estimated from {from} (est. 1RM {Kg(e1Rm)} kg, less {penalty} %)"
            : $"From {from} (est. 1RM {Kg(e1Rm)} kg)";

        return new StartingNumberSuggestion(kind, value, evidence, stale, penalty, reason);
    }

    private static decimal RoundDown(decimal value, decimal increment) =>
        Math.Max(increment, Math.Floor(value / increment) * increment);

    private static string Kg(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
