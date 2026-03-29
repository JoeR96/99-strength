using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Represents an exercise within a workout program.
/// Contains configuration and polymorphic progression strategy.
/// Entity within the Workout aggregate.
/// </summary>
public sealed class Exercise : Entity<ExerciseId>
{
    public string Name { get; private set; }
    public ExerciseCategory Category { get; private set; }
    public EquipmentType Equipment { get; private set; }
    public DayNumber AssignedDay { get; private set; }
    public int OrderInDay { get; private set; }

    /// <summary>
    /// The Hevy exercise template ID for syncing to Hevy.
    /// This is the canonical identifier from Hevy's exercise library.
    /// </summary>
    public string HevyExerciseTemplateId { get; private set; }

    /// <summary>
    /// Polymorphic progression strategy (owned entity).
    /// Can be LinearProgressionStrategy or RepsPerSetStrategy.
    /// </summary>
    public ExerciseProgression Progression { get; private set; }

    // EF Core constructor
    private Exercise()
    {
        Name = string.Empty;
        HevyExerciseTemplateId = string.Empty;
        Progression = null!;
    }

    private Exercise(
        ExerciseId id,
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string hevyExerciseTemplateId,
        ExerciseProgression progression)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Exercise name cannot be empty");
        CheckRule(!string.IsNullOrWhiteSpace(hevyExerciseTemplateId), "Hevy exercise template ID cannot be empty");
        CheckRule(orderInDay >= 1, "Order in day must be at least 1");

        Name = name;
        Category = category;
        Equipment = equipment;
        AssignedDay = assignedDay;
        OrderInDay = orderInDay;
        HevyExerciseTemplateId = hevyExerciseTemplateId;
        Progression = progression;
    }

    /// <summary>
    /// Creates an exercise with linear progression.
    /// Can be used for any category - progression strategy is independent of category.
    /// </summary>
    /// <param name="name">Exercise name</param>
    /// <param name="category">Category determines if it's a primary or auxiliary lift</param>
    /// <param name="equipment">Equipment type used</param>
    /// <param name="assignedDay">Training day assigned to</param>
    /// <param name="orderInDay">Order within the day</param>
    /// <param name="hevyExerciseTemplateId">Hevy exercise template ID for syncing</param>
    /// <param name="trainingMax">Training max for calculating working weights</param>
    /// <param name="useAmrap">Whether to use AMRAP on final set</param>
    /// <param name="baseSetsPerExercise">Number of sets per session</param>
    public static Exercise CreateWithLinearProgression(
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string hevyExerciseTemplateId,
        TrainingMax trainingMax,
        bool useAmrap = true,
        int baseSetsPerExercise = 4)
    {
        var progression = LinearProgressionStrategy.Create(
            trainingMax,
            useAmrap,
            baseSetsPerExercise);

        return new Exercise(
            new ExerciseId(Guid.NewGuid()),
            name,
            category,
            equipment,
            assignedDay,
            orderInDay,
            hevyExerciseTemplateId,
            progression);
    }

    /// <summary>
    /// Creates an exercise with reps-per-set (hypertrophy) progression.
    /// Can be used for any category - progression strategy is independent of category.
    /// A hypertrophy exercise can be a main lift, auxiliary, or accessory.
    /// </summary>
    /// <param name="name">Exercise name</param>
    /// <param name="category">Category determines if it's a primary, auxiliary, or accessory lift</param>
    /// <param name="equipment">Equipment type used</param>
    /// <param name="assignedDay">Training day assigned to</param>
    /// <param name="orderInDay">Order within the day</param>
    /// <param name="hevyExerciseTemplateId">Hevy exercise template ID for syncing</param>
    /// <param name="repRange">Target rep range for progression</param>
    /// <param name="startingWeight">Starting weight</param>
    /// <param name="startingSets">Starting number of sets</param>
    /// <param name="targetSets">Target sets before weight increases</param>
    /// <param name="isUnilateral">True if exercise is performed one side at a time (max 3 sets per side)</param>
    public static Exercise CreateWithRepsPerSetProgression(
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string hevyExerciseTemplateId,
        RepRange repRange,
        int startingSets = 2,
        int targetSets = 4,
        bool isUnilateral = false,
        Weight? startingWeight = null)
    {
        var progression = RepsPerSetStrategy.Create(
            repRange,
            equipment,
            startingSets,
            targetSets,
            isUnilateral,
            startingWeight);

        return new Exercise(
            new ExerciseId(Guid.NewGuid()),
            name,
            category,
            equipment,
            assignedDay,
            orderInDay,
            hevyExerciseTemplateId,
            progression);
    }

    /// <summary>
    /// Creates an exercise with minimal-sets progression.
    /// Used for exercises like Assisted Dips/Pullups where the goal is to complete
    /// a target total number of reps in as few sets as possible.
    /// </summary>
    /// <param name="name">Exercise name</param>
    /// <param name="category">Category determines if it's a primary, auxiliary, or accessory lift</param>
    /// <param name="equipment">Equipment type used</param>
    /// <param name="assignedDay">Training day assigned to</param>
    /// <param name="orderInDay">Order within the day</param>
    /// <param name="hevyExerciseTemplateId">Hevy exercise template ID for syncing</param>
    /// <param name="startingWeight">Starting weight (or assistance weight)</param>
    /// <param name="targetTotalReps">Total reps to complete across all sets (e.g., 40)</param>
    /// <param name="startingSets">Initial number of sets</param>
    /// <param name="minimumSets">Minimum sets allowed (floor)</param>
    /// <param name="maximumSets">Maximum sets allowed (ceiling)</param>
    public static Exercise CreateWithMinimalSetsProgression(
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string hevyExerciseTemplateId,
        Weight startingWeight,
        int targetTotalReps,
        int startingSets,
        int minimumSets = 2,
        int maximumSets = 10)
    {
        var progression = MinimalSetsStrategy.Create(
            startingWeight,
            targetTotalReps,
            startingSets,
            equipment,
            minimumSets,
            maximumSets);

        return new Exercise(
            new ExerciseId(Guid.NewGuid()),
            name,
            category,
            equipment,
            assignedDay,
            orderInDay,
            hevyExerciseTemplateId,
            progression);
    }

    /// <summary>
    /// Calculates planned sets for a given week and block.
    /// Delegates to the progression strategy.
    /// </summary>
    public IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        return Progression.CalculatePlannedSets(weekNumber, blockNumber);
    }

    /// <summary>
    /// Applies performance results to update progression state.
    /// For linear progression, adjusts Training Max.
    /// For reps-per-set, adjusts sets or weight.
    /// Returns TrainingMaxAdjusted event if TM was adjusted, null otherwise.
    /// </summary>
    public TrainingMaxAdjusted? ApplyProgression(ExercisePerformance performance)
    {
        CheckRule(performance.ExerciseId == Id,
            "Performance data must be for this exercise");

        // Capture previous TM if applicable
        TrainingMax? previousTm = null;
        if (Progression is LinearProgressionStrategy linearStrategy)
        {
            previousTm = linearStrategy.TrainingMax;
        }

        Progression.ApplyPerformanceResult(performance);

        // Check if TM changed and create event
        if (Progression is LinearProgressionStrategy linearStrategyAfter && previousTm != null)
        {
            if (!linearStrategyAfter.TrainingMax.Equals(previousTm))
            {
                var delta = performance.GetAmrapDelta();
                var adjustment = AmrapDeltaTable.GetAdjustment(delta);
                return new TrainingMaxAdjusted(
                    Progression.Id,
                    linearStrategyAfter.TrainingMax,
                    adjustment,
                    delta);
            }
        }

        return null;
    }

    /// <summary>
    /// Updates the starting weight for the exercise.
    /// Only applicable for RepsPerSetStrategy.
    /// </summary>
    public void UpdateStartingWeight(Weight weight)
    {
        if (Progression is RepsPerSetStrategy repsStrategy)
        {
            repsStrategy.UpdateWeight(weight);
        }
        else if (Progression is LinearProgressionStrategy)
        {
            throw new InvalidOperationException(
                "Cannot update starting weight for exercises using linear progression. " +
                "Use UpdateTrainingMax instead.");
        }
    }

    /// <summary>
    /// Confirms the starting weight for a RepsPerSet exercise after the first session.
    /// </summary>
    public void ConfirmStartingWeight(Weight weight)
    {
        if (Progression is RepsPerSetStrategy repsStrategy)
        {
            repsStrategy.ConfirmStartingWeight(weight);
        }
        else
        {
            throw new InvalidOperationException(
                "Can only confirm starting weight for exercises using RepsPerSet progression.");
        }
    }

    /// <summary>
    /// Updates the Training Max for the exercise.
    /// Only applicable for LinearProgressionStrategy.
    /// Returns event to be raised by aggregate root.
    /// </summary>
    public TrainingMaxAdjusted? UpdateTrainingMax(TrainingMax trainingMax, string? reason = null)
    {
        if (Progression is LinearProgressionStrategy linearStrategy)
        {
            linearStrategy.UpdateTrainingMax(trainingMax, reason);

            return new TrainingMaxAdjusted(
                Progression.Id,
                trainingMax,
                TrainingMaxAdjustment.None,
                amrapDelta: null,
                reason ?? "Manual adjustment");
        }
        else if (Progression is RepsPerSetStrategy)
        {
            throw new InvalidOperationException(
                "Cannot update Training Max for accessory exercises. " +
                "Use UpdateStartingWeight instead.");
        }

        return null;
    }

    /// <summary>
    /// Updates the rep range for accessory exercises.
    /// Only applicable for RepsPerSetStrategy.
    /// </summary>
    public void UpdateRepRange(RepRange repRange)
    {
        if (Progression is RepsPerSetStrategy repsStrategy)
        {
            repsStrategy.UpdateRepRange(repRange);
        }
        else
        {
            throw new InvalidOperationException(
                "Rep range can only be updated for accessory exercises");
        }
    }

    /// <summary>
    /// Changes the assigned training day for this exercise.
    /// </summary>
    public void ChangeAssignedDay(DayNumber newDay, int newOrderInDay)
    {
        CheckRule(newOrderInDay >= 1, "Order in day must be at least 1");

        AssignedDay = newDay;
        OrderInDay = newOrderInDay;
    }

    /// <summary>
    /// Substitutes this exercise with a different exercise.
    /// Preserves all progression data, only changes the name and optionally the Hevy template ID.
    /// </summary>
    /// <param name="newName">New exercise name</param>
    /// <param name="newHevyExerciseTemplateId">Optional new Hevy template ID. If not provided, keeps the existing one.</param>
    /// <returns>The original name for audit purposes</returns>
    public string Substitute(string newName, string? newHevyExerciseTemplateId = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(newName), "New exercise name cannot be empty");

        var originalName = Name;
        Name = newName;

        if (!string.IsNullOrWhiteSpace(newHevyExerciseTemplateId))
        {
            HevyExerciseTemplateId = newHevyExerciseTemplateId;
        }

        return originalName;
    }

    /// <summary>
    /// Gets a summary of the current progression state.
    /// </summary>
    public ProgressionSummary GetProgressionSummary()
    {
        return Progression.GetSummary();
    }

    /// <summary>
    /// Replaces the current progression strategy with a new one.
    /// Used when substituting an exercise and changing its progression type.
    /// </summary>
    /// <param name="newProgression">The new progression strategy to use</param>
    public void ReplaceProgression(ExerciseProgression newProgression)
    {
        CheckRule(newProgression != null, "New progression cannot be null");
        Progression = newProgression;
    }

    /// <summary>
    /// Gets the Training Max if this exercise uses linear progression.
    /// Returns null for accessory exercises.
    /// </summary>
    public TrainingMax? GetTrainingMax()
    {
        return Progression is LinearProgressionStrategy linearStrategy
            ? linearStrategy.TrainingMax
            : null;
    }

    /// <summary>
    /// Gets the current weight if this exercise uses reps-per-set or minimal-sets progression.
    /// Returns null for linear progression exercises.
    /// </summary>
    public Weight? GetCurrentWeight()
    {
        return Progression switch
        {
            RepsPerSetStrategy repsStrategy => repsStrategy.CurrentWeight,
            MinimalSetsStrategy minimalSetsStrategy => minimalSetsStrategy.CurrentWeight,
            _ => null
        };
    }

    /// <summary>
    /// Updates the starting weight for exercises using weight-based progression.
    /// Applicable for RepsPerSetStrategy and MinimalSetsStrategy.
    /// </summary>
    public void UpdateWeight(Weight weight)
    {
        if (Progression is RepsPerSetStrategy repsStrategy)
        {
            repsStrategy.UpdateWeight(weight);
        }
        else if (Progression is MinimalSetsStrategy minimalSetsStrategy)
        {
            minimalSetsStrategy.UpdateWeight(weight);
        }
        else if (Progression is LinearProgressionStrategy)
        {
            throw new InvalidOperationException(
                "Cannot update weight for exercises using linear progression. " +
                "Use UpdateTrainingMax instead.");
        }
    }

    /// <summary>
    /// Sets whether this exercise is unilateral (performed one side at a time).
    /// Only applicable for RepsPerSetStrategy.
    /// Unilateral exercises have sets performed once per side (so 3 sets = 6 total).
    /// </summary>
    public void SetUnilateral(bool isUnilateral)
    {
        if (Progression is RepsPerSetStrategy repsStrategy)
        {
            repsStrategy.SetUnilateral(isUnilateral);
        }
        else
        {
            throw new InvalidOperationException(
                "Unilateral setting only applies to RepsPerSet progression exercises.");
        }
    }

    /// <summary>
    /// Gets whether this exercise is unilateral.
    /// Returns false for non-RepsPerSet exercises.
    /// </summary>
    public bool IsUnilateral()
    {
        return Progression is RepsPerSetStrategy repsStrategy && repsStrategy.IsUnilateral;
    }

    /// <summary>
    /// Captures the current progression state as a snapshot for undo capability.
    /// </summary>
    public ProgressionSnapshot CaptureProgressionSnapshot()
    {
        var progressionType = Progression switch
        {
            LinearProgressionStrategy => "Linear",
            RepsPerSetStrategy => "RepsPerSet",
            MinimalSetsStrategy => "MinimalSets",
            _ => "Unknown"
        };

        var stateJson = Progression switch
        {
            LinearProgressionStrategy linear => System.Text.Json.JsonSerializer.Serialize(new
            {
                TrainingMaxValue = linear.TrainingMax.Value,
                TrainingMaxUnit = (int)linear.TrainingMax.Unit,
                UseAmrap = linear.UseAmrap,
                BaseSetsPerExercise = linear.BaseSetsPerExercise
            }),
            RepsPerSetStrategy reps => System.Text.Json.JsonSerializer.Serialize(new
            {
                CurrentWeight = reps.CurrentWeight?.Value,
                WeightUnit = reps.CurrentWeight != null ? (int?)reps.CurrentWeight.Unit : null,
                CurrentSetCount = reps.CurrentSetCount,
                TargetSets = reps.TargetSets,
                RepRangeMinimum = reps.RepRange.Minimum,
                RepRangeTarget = reps.RepRange.Target,
                RepRangeMaximum = reps.RepRange.Maximum,
                IsUnilateral = reps.IsUnilateral
            }),
            MinimalSetsStrategy minimal => System.Text.Json.JsonSerializer.Serialize(new
            {
                CurrentWeight = minimal.CurrentWeight.Value,
                WeightUnit = (int)minimal.CurrentWeight.Unit,
                CurrentSetCount = minimal.CurrentSetCount,
                TargetTotalReps = minimal.TargetTotalReps,
                MinimumSets = minimal.MinimumSets,
                MaximumSets = minimal.MaximumSets
            }),
            _ => "{}"
        };

        return new ProgressionSnapshot(Id.Value, Name, progressionType, stateJson);
    }

    /// <summary>
    /// Restores progression state from a snapshot (used when undoing a completed day).
    /// </summary>
    public void RestoreFromSnapshot(ProgressionSnapshot snapshot)
    {
        if (snapshot.ExerciseId != Id.Value)
            throw new InvalidOperationException("Snapshot exercise ID does not match");

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(snapshot.ProgressionStateJson);
            var root = json.RootElement;

            switch (Progression)
            {
                case LinearProgressionStrategy linear:
                    if (snapshot.ProgressionType == "Linear")
                    {
                        var tmValue = root.GetProperty("TrainingMaxValue").GetDecimal();
                        var tmUnit = (WeightUnit)root.GetProperty("TrainingMaxUnit").GetInt32();
                        linear.RestoreState(TrainingMax.Create(tmValue, tmUnit));
                    }
                    break;

                case RepsPerSetStrategy reps:
                    if (snapshot.ProgressionType == "RepsPerSet")
                    {
                        var weightProp = root.GetProperty("CurrentWeight");
                        decimal? currentWeight = weightProp.ValueKind == System.Text.Json.JsonValueKind.Null
                            ? null
                            : weightProp.GetDecimal();
                        reps.RestoreState(
                            currentWeight,
                            root.GetProperty("CurrentSetCount").GetInt32(),
                            root.GetProperty("IsUnilateral").GetBoolean());
                    }
                    break;

                case MinimalSetsStrategy minimal:
                    if (snapshot.ProgressionType == "MinimalSets")
                    {
                        minimal.RestoreState(
                            root.GetProperty("CurrentWeight").GetDecimal(),
                            root.GetProperty("CurrentSetCount").GetInt32());
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to restore progression from snapshot: {ex.Message}", ex);
        }
    }
}
