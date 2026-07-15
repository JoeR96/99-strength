using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Abstract base class for exercise progression strategies.
/// Implements the Strategy pattern for different progression methodologies.
/// Persisted using TPH (Table Per Hierarchy) in EF Core.
/// All operations are polymorphic — callers never type-check concrete strategies.
/// </summary>
public abstract class ExerciseProgression : Entity<ExerciseProgressionId>
{
    /// <summary>
    /// Discriminator column for TPH inheritance mapping.
    /// </summary>
    public string ProgressionType { get; protected set; } = string.Empty;

    protected ExerciseProgression()
    {
    }

    protected ExerciseProgression(ExerciseProgressionId id, string progressionType)
        : base(id)
    {
        ProgressionType = progressionType;
    }

    // --- Existing abstract methods ---

    /// <summary>
    /// Calculates the planned sets for a given week and block.
    /// </summary>
    public abstract IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber);

    /// <summary>
    /// Applies performance results and updates progression state (e.g., adjust TM, change sets).
    /// </summary>
    public abstract void ApplyPerformanceResult(ExercisePerformance performance);

    /// <summary>
    /// Gets a summary of the current progression state for display purposes.
    /// </summary>
    public abstract ProgressionSummary GetSummary();

    // --- Weight & Training Max accessors ---

    /// <summary>
    /// Gets the current working weight. Returns null for strategies that don't use direct weight (e.g., Linear uses TM).
    /// </summary>
    public abstract Weight? GetCurrentWeight();

    /// <summary>
    /// Gets the Training Max if applicable. Returns null for weight-based strategies.
    /// </summary>
    public abstract TrainingMax? GetTrainingMax();

    /// <summary>
    /// Gets the weight increment used when progressing. Returns zero-weight if not applicable.
    /// </summary>
    public abstract Weight GetWeightIncrement();

    // --- Unilateral support ---

    /// <summary>
    /// Whether this strategy supports unilateral exercise configuration.
    /// </summary>
    public abstract bool SupportsUnilateral { get; }

    /// <summary>
    /// Whether the exercise is currently configured as unilateral.
    /// </summary>
    public abstract bool IsUnilateral { get; }

    /// <summary>
    /// Whether the weight has not yet been confirmed (first session pending).
    /// </summary>
    public abstract bool IsWeightPending { get; }

    /// <summary>
    /// Whether this exercise has a pending weight confirmation after progression.
    /// Cable/Machine exercises require user confirmation of the new working weight
    /// because weight stack increments vary between gyms.
    /// </summary>
    public virtual bool PendingWeightConfirmation => false;

    /// <summary>
    /// The system-calculated suggested weight after progression.
    /// Only populated when PendingWeightConfirmation is true.
    /// </summary>
    public virtual Weight? SuggestedWeight => null;

    // --- Description for DTO mapping ---

    /// <summary>
    /// Generates a human-readable description of the progression change after performance is applied.
    /// Called before ApplyPerformanceResult to describe what will happen.
    /// </summary>
    public abstract string GetProgressionChangeDescription(ExercisePerformance performance);

    // --- Snapshot capture/restore ---

    /// <summary>
    /// Captures the current progression state as a snapshot for undo capability.
    /// </summary>
    internal abstract ProgressionSnapshot CaptureSnapshot(ExerciseId exerciseId, string exerciseName);

    /// <summary>
    /// Restores progression state from a snapshot.
    /// </summary>
    internal abstract void RestoreFromSnapshot(ProgressionSnapshot snapshot);

    // --- Mutators with default throws (override in supporting strategies) ---

    /// <summary>
    /// Updates the current weight. Supported by RepsPerSet and MinimalSets.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call on strategies where direct weight updates are meaningful.
    /// Linear strategies use Training Max instead. Callers should verify the strategy type supports this operation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support direct weight updates.</exception>
    public virtual void UpdateWeight(Weight newWeight)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support direct weight updates.");
    }

    /// <summary>
    /// Confirms the starting weight after first session. Supported by RepsPerSet.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call on strategies that track a pending starting weight.
    /// Check <see cref="IsWeightPending"/> before calling.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support weight confirmation.</exception>
    public virtual void ConfirmStartingWeight(Weight weight)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support weight confirmation.");
    }

    /// <summary>
    /// Updates the Training Max and returns a domain event. Supported by Linear.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call on strategies that use a Training Max.
    /// Check <see cref="GetTrainingMax"/> returns non-null before calling.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support training max updates.</exception>
    public virtual TrainingMaxAdjusted? UpdateTrainingMaxValue(TrainingMax newTm, string? reason = null)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support training max updates. Use UpdateWeight instead.");
    }

    /// <summary>
    /// Sets whether this exercise is unilateral. Supported by RepsPerSet.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call on strategies where <see cref="SupportsUnilateral"/> returns true.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support unilateral settings.</exception>
    public virtual void SetUnilateral(bool isUnilateral)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support unilateral settings.");
    }

    /// <summary>
    /// Confirms the new working weight after Cable/Machine progression.
    /// Clears the PendingWeightConfirmation flag. Supported by RepsPerSet.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call when <see cref="PendingWeightConfirmation"/> is true.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support working weight confirmation.</exception>
    public virtual void ConfirmWorkingWeight(Weight confirmedWeight)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support working weight confirmation.");
    }

    /// <summary>
    /// Updates the rep range. Supported by RepsPerSet.
    /// </summary>
    /// <remarks>
    /// Check-before-call contract: only call on strategies that use rep ranges.
    /// Check <see cref="GetProgressionData"/> for RepRangeMinimum/Maximum presence before calling.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the strategy does not support rep range updates.</exception>
    public virtual void UpdateRepRange(RepRange repRange)
    {
        throw new InvalidOperationException(
            $"{ProgressionType} progression does not support rep range updates.");
    }

    /// <summary>
    /// Returns all strategy-specific data as a flat record for DTO mapping.
    /// Enables Application layer to construct typed DTOs without type-checking strategies.
    /// </summary>
    public abstract ProgressionData GetProgressionData();

    /// <summary>
    /// Calculates the standard weight increment based on equipment type and current weight.
    /// Shared by all weight-based progression strategies.
    /// </summary>
    /// <remarks>
    /// Equipment-Based Weight Increments:
    /// - Bodyweight: 0 (progression via sets/reps only)
    /// - Dumbbell (kg): 1kg steps below 10kg; at 10kg and above, dumbbells only come in
    ///   even sizes (12, 14, 16...), so the increment lands on the next even number
    /// - Dumbbell (lbs): 1lb below 10lb, else 2lb
    /// - All others (Barbell, SmithMachine, PlateLoadedMachine, Cable, Machine): 2.5kg / 5lb
    /// </remarks>
    protected static Weight GetStandardWeightIncrement(EquipmentType equipmentType, Weight currentWeight)
    {
        if (equipmentType == EquipmentType.Bodyweight)
        {
            return Weight.Create(0, currentWeight.Unit);
        }

        if (equipmentType == EquipmentType.Dumbbell)
        {
            if (currentWeight.Unit == WeightUnit.Kilograms && currentWeight.Value >= 10)
            {
                var nextEven = Math.Floor(currentWeight.Value / 2) * 2 + 2;
                return Weight.Create(nextEven - currentWeight.Value, currentWeight.Unit);
            }

            var incrementValue = currentWeight.Value < 10 ? 1m : 2m;
            return Weight.Create(incrementValue, currentWeight.Unit);
        }

        // Barbell, SmithMachine, PlateLoadedMachine, Cable, Machine all use standard increment
        var standardIncrement = currentWeight.Unit == WeightUnit.Kilograms ? 2.5m : 5m;
        return Weight.Create(standardIncrement, currentWeight.Unit);
    }

    /// <summary>
    /// Creates a progression strategy from a configuration record.
    /// Dispatches to the correct concrete strategy based on progressionType.
    /// </summary>
    public static ExerciseProgression CreateFromConfig(ProgressionConfig config)
    {
        return config.ProgressionType.ToLowerInvariant() switch
        {
            "linear" => CreateLinear(
                config.TrainingMax ?? throw new ArgumentException("Linear progression requires a training max.", nameof(config)),
                config.UseAmrap ?? true,
                config.BaseSetsPerExercise ?? 4),

            "repsset" or "repsperset" => CreateRepsPerSet(
                config.RepRange ?? throw new ArgumentException("RepsPerSet progression requires a rep range.", nameof(config)),
                config.EquipmentType,
                config.StartingSets ?? 2,
                config.TargetSets ?? 4,
                config.IsUnilateral ?? false,
                config.StartingWeight),

            "minimalsets" => CreateMinimalSets(
                config.StartingWeight ?? throw new ArgumentException("MinimalSets progression requires a starting weight.", nameof(config)),
                config.TargetTotalReps ?? throw new ArgumentException("MinimalSets progression requires target total reps.", nameof(config)),
                config.StartingSets ?? Math.Max(config.MinimumSets ?? 2, Math.Min(config.MaximumSets ?? 10, ((config.MinimumSets ?? 2) + (config.MaximumSets ?? 10)) / 2)),
                config.EquipmentType,
                config.MinimumSets ?? 2,
                config.MaximumSets ?? 10),

            _ => throw new ArgumentException(
                $"Unknown progression type: {config.ProgressionType}. Valid types are: Linear, RepsPerSet, MinimalSets",
                nameof(config))
        };
    }

    // --- Factory methods ---

    /// <summary>
    /// Creates a Linear progression strategy with the given training max.
    /// </summary>
    public static ExerciseProgression CreateLinear(TrainingMax trainingMax, bool useAmrap = true, int baseSets = 4)
    {
        return LinearProgressionStrategy.Create(trainingMax, useAmrap, baseSets);
    }

    /// <summary>
    /// Creates a RepsPerSet progression strategy.
    /// </summary>
    public static ExerciseProgression CreateRepsPerSet(
        RepRange repRange,
        EquipmentType equipment,
        int startingSets = 2,
        int targetSets = 4,
        bool isUnilateral = false,
        Weight? startingWeight = null)
    {
        return RepsPerSetStrategy.Create(
            repRange, equipment, startingSets, targetSets, isUnilateral, startingWeight);
    }

    /// <summary>
    /// Creates a MinimalSets progression strategy.
    /// </summary>
    public static ExerciseProgression CreateMinimalSets(
        Weight startingWeight,
        int targetTotalReps,
        int startingSets,
        EquipmentType equipment,
        int minimumSets = 2,
        int maximumSets = 10)
    {
        return MinimalSetsStrategy.Create(
            startingWeight, targetTotalReps, startingSets, equipment, minimumSets, maximumSets);
    }
}

/// <summary>
/// Summary of progression state for UI display.
/// </summary>
public sealed record ProgressionSummary
{
    public string Type { get; init; } = string.Empty;
    public Dictionary<string, string> Details { get; init; } = new();
}

/// <summary>
/// Flat record carrying all strategy-specific data for DTO mapping.
/// Each strategy populates only its relevant properties.
/// </summary>
public sealed record ProgressionData
{
    public bool? UseAmrap { get; init; }
    public int? BaseSetsPerExercise { get; init; }

    public int? RepRangeMinimum { get; init; }
    public int? RepRangeMaximum { get; init; }
    public int? StartingSets { get; init; }
    public int? CurrentSetCount { get; init; }
    public int? TargetSets { get; init; }

    public int? TargetTotalReps { get; init; }
    public int? MinimumSets { get; init; }
    public int? MaximumSets { get; init; }
}
