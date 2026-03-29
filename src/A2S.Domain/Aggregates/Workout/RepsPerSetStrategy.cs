using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Reps Per Set progression strategy for accessory exercises.
/// Progressively adds sets until reaching target, then increases weight.
/// Does not use Training Max - uses direct weight progression.
/// </summary>
/// <remarks>
/// Reference: research/business-rules.md Section 3.3 "Reps Per Set Progression" (lines 172-227)
///
/// Progression Logic:
/// - SUCCESS: All sets hit maximumReps → Add set (or increase weight if at target sets)
/// - MAINTAINED: All sets hit at least minimumReps → No change
/// - FAILED: Any set below minimumReps → Remove set (or decrease weight if at minimum sets)
/// </remarks>
public sealed class RepsPerSetStrategy : ExerciseProgression
{
    public RepRange RepRange { get; private set; }
    public int CurrentSetCount { get; private set; }
    public int StartingSets { get; private set; }
    public int TargetSets { get; private set; }
    public Weight? CurrentWeight { get; private set; }

    /// <summary>
    /// Indicates whether the starting weight has not yet been confirmed.
    /// Weight is deferred until after the first session.
    /// </summary>
    public bool IsWeightPending => CurrentWeight == null;
    public EquipmentType Equipment { get; private set; }

    /// <summary>
    /// Indicates if this is a unilateral exercise (performed one side at a time).
    /// Unilateral exercises have a lower max set target (3 per side = 6 total).
    /// </summary>
    public bool IsUnilateral { get; private set; }

    /// <summary>
    /// Gets the maximum set count before weight increases.
    /// Unilateral exercises max at 3 sets (per side), bilateral max at 5.
    /// </summary>
    public int MaxSets => IsUnilateral ? 3 : 5;

    // EF Core constructor
    private RepsPerSetStrategy()
    {
        RepRange = null!;
        CurrentWeight = null;
    }

    private RepsPerSetStrategy(
        ExerciseProgressionId id,
        RepRange repRange,
        int startingSets,
        int targetSets,
        Weight? currentWeight,
        EquipmentType equipment,
        bool isUnilateral)
        : base(id, "RepsPerSet")
    {
        CheckRule(startingSets >= 1 && startingSets <= 10,
            "Starting sets must be between 1 and 10");
        CheckRule(targetSets >= startingSets && targetSets <= 10,
            "Target sets must be between starting sets and 10");

        RepRange = repRange;
        CurrentSetCount = startingSets;
        StartingSets = startingSets;
        TargetSets = targetSets;
        CurrentWeight = currentWeight;
        Equipment = equipment;
        IsUnilateral = isUnilateral;
    }

    public static RepsPerSetStrategy Create(
        RepRange repRange,
        EquipmentType equipment,
        int startingSets = 2,
        int targetSets = 4,
        bool isUnilateral = false,
        Weight? startingWeight = null)
    {
        return new RepsPerSetStrategy(
            new ExerciseProgressionId(Guid.NewGuid()),
            repRange,
            startingSets,
            targetSets,
            startingWeight,
            equipment,
            isUnilateral);
    }

    /// <summary>
    /// Confirms the starting weight after the first session.
    /// Can only be called when weight is pending (null).
    /// </summary>
    public void ConfirmStartingWeight(Weight weight)
    {
        CheckRule(CurrentWeight == null, "Starting weight has already been confirmed");
        CurrentWeight = weight;
    }

    /// <summary>
    /// Calculates planned sets for the current state.
    /// All sets use the same weight and target reps from RepRange.
    /// When weight is pending, returns sets with zero weight.
    /// </summary>
    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        var weight = CurrentWeight ?? Weight.Create(0, WeightUnit.Kilograms);
        var sets = new List<PlannedSet>();
        for (int i = 1; i <= CurrentSetCount; i++)
        {
            sets.Add(new PlannedSet(i, weight, RepRange.Target, isAmrap: false));
        }
        return sets;
    }

    /// <summary>
    /// Applies performance results and adjusts sets or weight based on performance.
    /// Implements the Reps Per Set progression algorithm from business-rules.md lines 187-210.
    /// </summary>
    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        // Skip progression when weight hasn't been confirmed yet (first session)
        if (IsWeightPending)
            return;

        var evaluation = EvaluatePerformance(performance);

        switch (evaluation)
        {
            case PerformanceEvaluation.Success:
                HandleSuccess();
                break;

            case PerformanceEvaluation.Failed:
                HandleFailure();
                break;

            case PerformanceEvaluation.Maintained:
                // No change - keep building proficiency at current level
                break;
        }
    }

    /// <summary>
    /// Gets a summary of current progression state for UI display.
    /// </summary>
    public override ProgressionSummary GetSummary()
    {
        var effectiveMaxSets = Math.Min(TargetSets, MaxSets);
        var details = new Dictionary<string, string>
        {
            ["Rep Range"] = RepRange.ToString(),
            ["Current Sets"] = $"{CurrentSetCount}/{effectiveMaxSets}",
            ["Current Weight"] = CurrentWeight?.ToString() ?? "Pending",
            ["Equipment"] = Equipment.ToString()
        };

        if (IsUnilateral)
        {
            details["Type"] = "Unilateral (per side)";
        }

        return new ProgressionSummary
        {
            Type = "Reps Per Set",
            Details = details
        };
    }

    /// <summary>
    /// Evaluates performance based on rep range thresholds.
    /// Reference: business-rules.md lines 189-192.
    /// </summary>
    private PerformanceEvaluation EvaluatePerformance(ExercisePerformance performance)
    {
        // SUCCESS: All sets hit maximum reps
        if (performance.AllSetsHitMax(RepRange))
        {
            return PerformanceEvaluation.Success;
        }

        // FAILED: Any set falls below minimum reps
        if (performance.AnySetsBelowMin(RepRange))
        {
            return PerformanceEvaluation.Failed;
        }

        // MAINTAINED: All sets hit at least minimum, but not all hit maximum
        return PerformanceEvaluation.Maintained;
    }

    /// <summary>
    /// Handles successful performance (all sets hit max reps).
    /// Reference: business-rules.md lines 195-200.
    /// Uses MaxSets property which accounts for unilateral exercises (3 max) vs bilateral (5 max).
    /// </summary>
    private void HandleSuccess()
    {
        // Use the lower of TargetSets or MaxSets (unilateral cap)
        var effectiveMaxSets = Math.Min(TargetSets, MaxSets);

        if (CurrentSetCount < effectiveMaxSets)
        {
            // Add one set
            CurrentSetCount++;
        }
        else
        {
            // At max sets, increase weight and reset to starting sets
            CurrentWeight = CurrentWeight!.Add(GetWeightIncrement());
            CurrentSetCount = StartingSets;
        }
    }

    /// <summary>
    /// Handles failed performance (any set below min reps).
    /// Reference: business-rules.md lines 202-207.
    /// </summary>
    private void HandleFailure()
    {
        if (CurrentSetCount > 1)
        {
            // Remove one set
            CurrentSetCount--;
        }
        else
        {
            // At minimum sets, reduce weight (if possible)
            var decrement = GetWeightIncrement();

            // Only decrease if we won't go below zero
            if (CurrentWeight!.Value >= decrement.Value)
            {
                CurrentWeight = CurrentWeight.Subtract(decrement);
            }
            // If weight is already at minimum, stay at 1 set with current weight
            // User should consider form check or exercise substitution
        }
    }

    /// <summary>
    /// Calculates weight increment based on equipment type.
    /// Reference: business-rules.md lines 212-227.
    /// </summary>
    /// <remarks>
    /// Equipment-Based Weight Increments:
    /// - Dumbbell: 1kg if weight &lt; 10kg, else 2kg
    /// - Barbell/Smith Machine/Cable/Machine: 2.5kg
    /// - Bodyweight: 0kg (progression via sets/reps only)
    /// </remarks>
    private Weight GetWeightIncrement()
    {
        // CurrentWeight is guaranteed non-null here because ApplyPerformanceResult guards against it
        var weight = CurrentWeight!;

        if (Equipment == EquipmentType.Bodyweight)
        {
            return Weight.Create(0, weight.Unit);
        }

        if (Equipment == EquipmentType.Dumbbell)
        {
            var incrementValue = weight.Value < 10 ? 1m : 2m;
            return Weight.Create(incrementValue, weight.Unit);
        }

        var standardIncrement = weight.Unit == WeightUnit.Kilograms ? 2.5m : 5m;
        return Weight.Create(standardIncrement, weight.Unit);
    }

    /// <summary>
    /// Manually updates the current weight.
    /// Used for adjustments or corrections by the user.
    /// </summary>
    public void UpdateWeight(Weight newWeight)
    {
        if (CurrentWeight != null)
        {
            CheckRule(newWeight.Unit == CurrentWeight.Unit,
                "New weight must use the same unit as current weight");
        }

        CurrentWeight = newWeight;
    }

    /// <summary>
    /// Manually updates the rep range.
    /// Used when user wants to change the target rep range for an accessory.
    /// </summary>
    public void UpdateRepRange(RepRange newRepRange)
    {
        RepRange = newRepRange;
    }

    /// <summary>
    /// Sets the unilateral flag for this exercise.
    /// Unilateral exercises have a lower max set target (3 per side).
    /// When switching to unilateral, set count is capped at 3 if currently higher.
    /// </summary>
    public void SetUnilateral(bool isUnilateral)
    {
        IsUnilateral = isUnilateral;

        // If switching to unilateral and current sets exceed the new max, cap it
        if (isUnilateral && CurrentSetCount > MaxSets)
        {
            CurrentSetCount = MaxSets;
        }
    }

    internal void RestoreState(decimal? currentWeight, int currentSetCount, bool isUnilateral, WeightUnit? weightUnit = null)
    {
        CurrentWeight = currentWeight.HasValue
            ? Weight.Create(currentWeight.Value, weightUnit ?? CurrentWeight?.Unit ?? WeightUnit.Kilograms)
            : null;
        CurrentSetCount = currentSetCount;
        IsUnilateral = isUnilateral;
    }
}

/// <summary>
/// Result of evaluating performance against rep range criteria.
/// </summary>
public enum PerformanceEvaluation
{
    /// <summary>
    /// All sets hit maximum reps - progress to next level.
    /// </summary>
    Success,

    /// <summary>
    /// All sets hit at least minimum reps - maintain current level.
    /// </summary>
    Maintained,

    /// <summary>
    /// Any set fell below minimum reps - regress to previous level.
    /// </summary>
    Failed
}
