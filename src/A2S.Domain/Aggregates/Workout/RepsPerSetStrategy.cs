using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
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
    public override bool IsWeightPending => CurrentWeight == null;
    public EquipmentType Equipment { get; private set; }

    /// <summary>
    /// Indicates if this is a unilateral exercise (performed one side at a time).
    /// Unilateral exercises have a lower max set target (3 per side = 6 total).
    /// </summary>
    private bool _isUnilateral;
    public override bool IsUnilateral => _isUnilateral;

    /// <summary>
    /// Whether this exercise has a pending weight confirmation after Cable/Machine progression.
    /// </summary>
    private bool _pendingWeightConfirmation;
    public override bool PendingWeightConfirmation => _pendingWeightConfirmation;

    /// <summary>
    /// The system-calculated suggested weight when PendingWeightConfirmation is true.
    /// </summary>
    private Weight? _suggestedWeight;
    public override Weight? SuggestedWeight => _suggestedWeight;

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
        _isUnilateral = isUnilateral;
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
    public override void ConfirmStartingWeight(Weight weight)
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
            sets.Add(new PlannedSet(i, weight, RepRange.Maximum, isAmrap: false));
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
            var newWeight = CurrentWeight!.Add(GetWeightIncrement());
            CurrentWeight = newWeight;
            CurrentSetCount = StartingSets;

            // Cable/Machine exercises require user confirmation of new working weight
            // because weight stack increments vary between gyms
            if (Equipment is EquipmentType.Cable or EquipmentType.Machine)
            {
                _pendingWeightConfirmation = true;
                _suggestedWeight = newWeight;
            }
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
    /// Delegates to shared base class implementation.
    /// </summary>
    public override Weight GetWeightIncrement()
    {
        var weight = CurrentWeight ?? Weight.Create(0, WeightUnit.Kilograms);
        return GetStandardWeightIncrement(Equipment, weight);
    }

    /// <summary>
    /// Manually updates the current weight.
    /// Used for adjustments or corrections by the user.
    /// </summary>
    public override void UpdateWeight(Weight newWeight)
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
    public override void UpdateRepRange(RepRange newRepRange)
    {
        RepRange = newRepRange;
    }

    /// <summary>
    /// Sets the unilateral flag for this exercise.
    /// Unilateral exercises have a lower max set target (3 per side).
    /// When switching to unilateral, set count is capped at 3 if currently higher.
    /// </summary>
    public override void SetUnilateral(bool isUnilateral)
    {
        _isUnilateral = isUnilateral;

        // If switching to unilateral and current sets exceed the new max, cap it
        if (isUnilateral && CurrentSetCount > MaxSets)
        {
            CurrentSetCount = MaxSets;
        }
    }

    /// <summary>
    /// Confirms the new working weight after Cable/Machine progression.
    /// Clears the PendingWeightConfirmation flag and applies the user-confirmed weight.
    /// </summary>
    public override void ConfirmWorkingWeight(Weight confirmedWeight)
    {
        CheckRule(_pendingWeightConfirmation,
            "No pending weight confirmation for this exercise");

        if (CurrentWeight != null)
        {
            CheckRule(confirmedWeight.Unit == CurrentWeight.Unit,
                "Confirmed weight must use the same unit as current weight");
        }

        CurrentWeight = confirmedWeight;
        _pendingWeightConfirmation = false;
        _suggestedWeight = null;
    }

    public override Weight? GetCurrentWeight() => CurrentWeight;

    public override TrainingMax? GetTrainingMax() => null;

    public override bool SupportsUnilateral => true;

    public override string GetProgressionChangeDescription(ExercisePerformance performance)
    {
        if (IsWeightPending)
        {
            return "Weight pending confirmation";
        }

        if (performance.AllSetsHitMax(RepRange))
        {
            return CurrentSetCount < Math.Min(TargetSets, MaxSets)
                ? "Added 1 set"
                : "Weight increased, sets reset";
        }

        if (performance.AnySetsBelowMin(RepRange))
        {
            return CurrentSetCount > 1
                ? "Removed 1 set"
                : "Weight decreased";
        }

        return "No change";
    }

    internal override ProgressionSnapshot CaptureSnapshot(ExerciseId exerciseId, string exerciseName) =>
        ProgressionSnapshot.FromState(exerciseId, exerciseName, CaptureState());

    internal override void RestoreFromSnapshot(ProgressionSnapshot snapshot)
    {
        var state = snapshot.GetRepsPerSetState()
            ?? throw new InvalidOperationException("Failed to deserialize RepsPerSet snapshot state");
        RestoreFromState(state);
    }

    public override ProgressionData GetProgressionData() => new()
    {
        RepRangeMinimum = RepRange.Minimum,
        RepRangeMaximum = RepRange.Maximum,
        StartingSets = StartingSets,
        CurrentSetCount = CurrentSetCount,
        TargetSets = TargetSets
    };

    internal void RestoreState(decimal? currentWeight, int currentSetCount, bool isUnilateral, WeightUnit? weightUnit = null)
    {
        CurrentWeight = currentWeight.HasValue
            ? Weight.Create(currentWeight.Value, weightUnit ?? CurrentWeight?.Unit ?? WeightUnit.Kilograms)
            : null;
        CurrentSetCount = currentSetCount;
        _isUnilateral = isUnilateral;
    }

    internal RepsPerSetProgressionState CaptureState() => new(
        CurrentWeight?.Value, CurrentWeight != null ? (int?)CurrentWeight.Unit : null,
        CurrentSetCount, TargetSets,
        RepRange.Minimum, RepRange.Maximum,
        IsUnilateral,
        _pendingWeightConfirmation,
        _suggestedWeight?.Value, _suggestedWeight != null ? (int?)_suggestedWeight.Unit : null);

    internal void RestoreFromState(RepsPerSetProgressionState state)
    {
        RestoreState(state.CurrentWeight, state.CurrentSetCount, state.IsUnilateral,
            state.WeightUnit.HasValue ? (WeightUnit)state.WeightUnit.Value : null);
        _pendingWeightConfirmation = state.PendingWeightConfirmation;
        if (state.SuggestedWeight.HasValue)
        {
            if (!state.SuggestedWeightUnit.HasValue)
            {
                throw new InvalidOperationException(
                    "SuggestedWeightUnit must be specified when SuggestedWeight has a value.");
            }

            _suggestedWeight = Weight.Create(state.SuggestedWeight.Value,
                (WeightUnit)state.SuggestedWeightUnit.Value);
        }
        else
        {
            _suggestedWeight = null;
        }
    }
}

/// <summary>
/// Typed memento holding RepsPerSet progression state for snapshot capture/restore.
/// </summary>
public sealed record RepsPerSetProgressionState(
    decimal? CurrentWeight, int? WeightUnit, int CurrentSetCount, int TargetSets,
    int RepRangeMinimum, int RepRangeMaximum, bool IsUnilateral,
    bool PendingWeightConfirmation = false,
    decimal? SuggestedWeight = null, int? SuggestedWeightUnit = null);
