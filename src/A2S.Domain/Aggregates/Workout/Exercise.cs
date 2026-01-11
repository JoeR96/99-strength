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
    /// Polymorphic progression strategy (owned entity).
    /// Can be LinearProgressionStrategy or RepsPerSetStrategy.
    /// </summary>
    public ExerciseProgression Progression { get; private set; }

    // EF Core constructor
    private Exercise()
    {
        Name = string.Empty;
        Progression = null!;
    }

    private Exercise(
        ExerciseId id,
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        ExerciseProgression progression)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Exercise name cannot be empty");
        CheckRule(orderInDay >= 1, "Order in day must be at least 1");

        Name = name;
        Category = category;
        Equipment = equipment;
        AssignedDay = assignedDay;
        OrderInDay = orderInDay;
        Progression = progression;
    }

    /// <summary>
    /// Creates an exercise with linear progression (for main/auxiliary lifts).
    /// </summary>
    public static Exercise CreateWithLinearProgression(
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        TrainingMax trainingMax,
        bool useAmrap = true,
        int baseSetsPerExercise = 4)
    {
        CheckRule(
            category == ExerciseCategory.MainLift || category == ExerciseCategory.Auxiliary,
            "Linear progression is only for main lifts or auxiliary exercises");

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
            progression);
    }

    /// <summary>
    /// Creates an exercise with reps-per-set progression (for accessories).
    /// </summary>
    public static Exercise CreateWithRepsPerSetProgression(
        string name,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        RepRange repRange,
        Weight startingWeight,
        int startingSets = 2,
        int targetSets = 4)
    {
        var progression = RepsPerSetStrategy.Create(
            repRange,
            startingWeight,
            equipment,
            startingSets,
            targetSets);

        return new Exercise(
            new ExerciseId(Guid.NewGuid()),
            name,
            ExerciseCategory.Accessory,
            equipment,
            assignedDay,
            orderInDay,
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
                var adjustment = CalculateAdjustmentFromDelta(delta);
                return new TrainingMaxAdjusted(
                    Progression.Id,
                    linearStrategyAfter.TrainingMax,
                    adjustment,
                    delta);
            }
        }

        return null;
    }

    private static TrainingMaxAdjustment CalculateAdjustmentFromDelta(int delta)
    {
        return delta switch
        {
            >= 5 => TrainingMaxAdjustment.Percentage(0.03m),
            4 => TrainingMaxAdjustment.Percentage(0.02m),
            3 => TrainingMaxAdjustment.Percentage(0.015m),
            2 => TrainingMaxAdjustment.Percentage(0.01m),
            1 => TrainingMaxAdjustment.Percentage(0.005m),
            0 => TrainingMaxAdjustment.None,
            -1 => TrainingMaxAdjustment.Percentage(-0.02m),
            _ => TrainingMaxAdjustment.Percentage(-0.05m)
        };
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
    /// Updates the Training Max for the exercise.
    /// Only applicable for LinearProgressionStrategy.
    /// Returns event to be raised by aggregate root.
    /// </summary>
    public TrainingMaxAdjusted? UpdateTrainingMax(TrainingMax trainingMax, string? reason = null)
    {
        if (Progression is LinearProgressionStrategy linearStrategy)
        {
            var previousTm = linearStrategy.TrainingMax;
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
    /// Gets a summary of the current progression state.
    /// </summary>
    public ProgressionSummary GetProgressionSummary()
    {
        return Progression.GetSummary();
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
    /// Gets the current weight if this exercise uses reps-per-set progression.
    /// Returns null for linear progression exercises.
    /// </summary>
    public Weight? GetCurrentWeight()
    {
        return Progression is RepsPerSetStrategy repsStrategy
            ? repsStrategy.CurrentWeight
            : null;
    }
}
