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
    /// The External exercise template ID for syncing to Hevy.
    /// This is the canonical identifier from Hevy's exercise library.
    /// </summary>
    public string ExternalTemplateId { get; private set; }

    /// <summary>
    /// Polymorphic progression strategy (owned entity).
    /// Can be LinearProgressionStrategy or RepsPerSetStrategy.
    /// </summary>
    public ExerciseProgression Progression { get; private set; }

    // EF Core constructor
    private Exercise()
    {
        Name = string.Empty;
        ExternalTemplateId = string.Empty;
        Progression = null!;
    }

    private Exercise(
        ExerciseId id,
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string externalTemplateId,
        ExerciseProgression progression)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(name), "Exercise name cannot be empty");
        CheckRule(!string.IsNullOrWhiteSpace(externalTemplateId), "External exercise template ID cannot be empty");
        CheckRule(orderInDay >= 1, "Order in day must be at least 1");

        Name = name;
        Category = category;
        Equipment = equipment;
        AssignedDay = assignedDay;
        OrderInDay = orderInDay;
        ExternalTemplateId = externalTemplateId;
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
    /// <param name="externalTemplateId">External exercise template ID for syncing</param>
    /// <param name="trainingMax">Training max for calculating working weights</param>
    /// <param name="useAmrap">Whether to use AMRAP on final set</param>
    /// <param name="baseSetsPerExercise">Number of sets per session</param>
    public static Exercise CreateWithLinearProgression(
        string name,
        ExerciseCategory category,
        EquipmentType equipment,
        DayNumber assignedDay,
        int orderInDay,
        string externalTemplateId,
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
            externalTemplateId,
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
    /// <param name="externalTemplateId">External exercise template ID for syncing</param>
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
        string externalTemplateId,
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
            externalTemplateId,
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
    /// <param name="externalTemplateId">External exercise template ID for syncing</param>
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
        string externalTemplateId,
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
            externalTemplateId,
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
    /// Returns TrainingMaxAdjusted event if TM was adjusted, null otherwise.
    /// </summary>
    internal TrainingMaxAdjusted? ApplyProgression(ExercisePerformance performance)
    {
        CheckRule(performance.ExerciseId == Id,
            "Performance data must be for this exercise");

        var previousTm = Progression.GetTrainingMax();

        Progression.ApplyPerformanceResult(performance);

        var currentTm = Progression.GetTrainingMax();
        if (previousTm != null && currentTm != null && !currentTm.Equals(previousTm))
        {
            var delta = performance.GetAmrapDelta();
            var adjustment = AmrapDeltaTable.GetAdjustment(delta);
            return new TrainingMaxAdjusted(Progression.Id, currentTm, adjustment, delta);
        }

        return null;
    }

    /// <summary>
    /// Updates the starting weight. Delegates to progression strategy polymorphically.
    /// </summary>
    internal void UpdateStartingWeight(Weight weight)
    {
        Progression.UpdateWeight(weight);
    }

    /// <summary>
    /// Confirms the starting weight after the first session.
    /// </summary>
    internal void ConfirmStartingWeight(Weight weight)
    {
        Progression.ConfirmStartingWeight(weight);
    }

    /// <summary>
    /// Confirms the new working weight after Cable/Machine progression.
    /// Clears the PendingWeightConfirmation flag and applies the user-confirmed weight.
    /// </summary>
    internal void ConfirmWorkingWeight(Weight confirmedWeight)
    {
        Progression.ConfirmWorkingWeight(confirmedWeight);
    }

    /// <summary>
    /// Updates the Training Max for the exercise.
    /// Returns event to be raised by aggregate root.
    /// </summary>
    internal TrainingMaxAdjusted? UpdateTrainingMax(TrainingMax trainingMax, string? reason = null)
    {
        return Progression.UpdateTrainingMaxValue(trainingMax, reason);
    }

    /// <summary>
    /// Updates the rep range. Delegates to progression strategy polymorphically.
    /// </summary>
    internal void UpdateRepRange(RepRange repRange)
    {
        Progression.UpdateRepRange(repRange);
    }

    /// <summary>
    /// Changes the assigned training day for this exercise.
    /// </summary>
    internal void ChangeAssignedDay(DayNumber newDay, int newOrderInDay)
    {
        CheckRule(newOrderInDay >= 1, "Order in day must be at least 1");

        AssignedDay = newDay;
        OrderInDay = newOrderInDay;
    }

    /// <summary>
    /// Substitutes this exercise with a different exercise.
    /// Preserves all progression data, only changes the name and optionally the External template ID.
    /// </summary>
    /// <param name="newName">New exercise name</param>
    /// <param name="newExternalTemplateId">Optional new External template ID. If not provided, keeps the existing one.</param>
    /// <returns>The original name for audit purposes</returns>
    internal string Substitute(string newName, string? newExternalTemplateId = null)
    {
        CheckRule(!string.IsNullOrWhiteSpace(newName), "New exercise name cannot be empty");

        var originalName = Name;
        Name = newName;

        if (!string.IsNullOrWhiteSpace(newExternalTemplateId))
        {
            ExternalTemplateId = newExternalTemplateId;
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
    internal void ReplaceProgression(ExerciseProgression newProgression)
    {
        CheckRule(newProgression != null, "New progression cannot be null");
        Progression = newProgression;
    }

    /// <summary>
    /// Gets the Training Max if this exercise uses linear progression.
    /// Returns null for non-linear progression exercises.
    /// </summary>
    public TrainingMax? GetTrainingMax()
    {
        return Progression.GetTrainingMax();
    }

    /// <summary>
    /// Gets the current weight for weight-based progression strategies.
    /// Returns null for linear progression exercises.
    /// </summary>
    public Weight? GetCurrentWeight()
    {
        return Progression.GetCurrentWeight();
    }

    /// <summary>
    /// Updates the weight. Delegates to progression strategy polymorphically.
    /// </summary>
    internal void UpdateWeight(Weight weight)
    {
        Progression.UpdateWeight(weight);
    }

    /// <summary>
    /// Sets whether this exercise is unilateral. Delegates to progression strategy.
    /// </summary>
    internal void SetUnilateral(bool isUnilateral)
    {
        Progression.SetUnilateral(isUnilateral);
    }

    /// <summary>
    /// Gets whether this exercise is unilateral.
    /// </summary>
    public bool IsUnilateral()
    {
        return Progression.IsUnilateral;
    }

    /// <summary>
    /// Captures the current progression state as a snapshot for undo capability.
    /// </summary>
    public ProgressionSnapshot CaptureProgressionSnapshot()
    {
        return Progression.CaptureSnapshot(Id, Name);
    }

    /// <summary>
    /// Restores progression state from a snapshot (used when undoing a completed day).
    /// </summary>
    internal void RestoreFromSnapshot(ProgressionSnapshot snapshot)
    {
        if (snapshot.ExerciseId != Id)
        {
            throw new InvalidOperationException("Snapshot exercise ID does not match");
        }

        Progression.RestoreFromSnapshot(snapshot);
    }
}
