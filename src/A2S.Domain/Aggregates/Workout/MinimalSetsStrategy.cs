using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Minimal Sets progression strategy for exercises like Assisted Dips/Pullups.
/// The goal is to complete a target total number of reps in as few sets as possible.
/// Progress is measured by completing the target reps in fewer sets over time.
/// </summary>
/// <remarks>
/// Progression Logic:
/// - SUCCESS: Completed target reps in FEWER sets than CurrentSetCount -> Reduce set count
/// - MAINTAINED: Completed target reps in EXACTLY CurrentSetCount sets -> No change
/// - FAILED: Could not complete target reps in CurrentSetCount sets -> Add a set
///
/// Weight is user-controlled and not automatically adjusted by the system.
/// </remarks>
public sealed class MinimalSetsStrategy : ExerciseProgression
{
    /// <summary>
    /// The current weight being used (user-controlled for Assisted exercises,
    /// this is often the assistance weight).
    /// </summary>
    public Weight CurrentWeight { get; private set; }

    /// <summary>
    /// The target total reps to complete across all sets (e.g., 40 reps).
    /// </summary>
    public int TargetTotalReps { get; private set; }

    /// <summary>
    /// The current number of sets the user is targeting.
    /// Success means completing TargetTotalReps in fewer than this many sets.
    /// </summary>
    public int CurrentSetCount { get; private set; }

    /// <summary>
    /// The starting number of sets when the exercise was first added.
    /// </summary>
    public int StartingSets { get; private set; }

    /// <summary>
    /// The minimum number of sets allowed. Cannot reduce below this.
    /// </summary>
    public int MinimumSets { get; private set; }

    /// <summary>
    /// The maximum number of sets allowed. Cannot increase above this.
    /// </summary>
    public int MaximumSets { get; private set; }

    /// <summary>
    /// Equipment type used for this exercise.
    /// </summary>
    public EquipmentType Equipment { get; private set; }

    // EF Core constructor
    private MinimalSetsStrategy()
    {
        CurrentWeight = null!;
    }

    private MinimalSetsStrategy(
        ExerciseProgressionId id,
        Weight currentWeight,
        int targetTotalReps,
        int startingSets,
        int minimumSets,
        int maximumSets,
        EquipmentType equipment)
        : base(id, "MinimalSets")
    {
        CheckRule(targetTotalReps >= 10 && targetTotalReps <= 200,
            "Target total reps must be between 10 and 200");
        CheckRule(startingSets >= 1 && startingSets <= 20,
            "Starting sets must be between 1 and 20");
        CheckRule(minimumSets >= 1 && minimumSets <= startingSets,
            "Minimum sets must be between 1 and starting sets");
        CheckRule(maximumSets >= startingSets && maximumSets <= 20,
            "Maximum sets must be between starting sets and 20");

        CurrentWeight = currentWeight;
        TargetTotalReps = targetTotalReps;
        CurrentSetCount = startingSets;
        StartingSets = startingSets;
        MinimumSets = minimumSets;
        MaximumSets = maximumSets;
        Equipment = equipment;
    }

    /// <summary>
    /// Creates a new MinimalSetsStrategy with the specified parameters.
    /// </summary>
    /// <param name="currentWeight">Starting/current weight</param>
    /// <param name="targetTotalReps">Total reps to complete (e.g., 40)</param>
    /// <param name="startingSets">Initial set count</param>
    /// <param name="equipment">Equipment type</param>
    /// <param name="minimumSets">Floor for set count (default: 2)</param>
    /// <param name="maximumSets">Ceiling for set count (default: 10)</param>
    public static MinimalSetsStrategy Create(
        Weight currentWeight,
        int targetTotalReps,
        int startingSets,
        EquipmentType equipment,
        int minimumSets = 2,
        int maximumSets = 10)
    {
        return new MinimalSetsStrategy(
            new ExerciseProgressionId(Guid.NewGuid()),
            currentWeight,
            targetTotalReps,
            startingSets,
            minimumSets,
            maximumSets,
            equipment);
    }

    /// <summary>
    /// Calculates planned sets for the current state.
    /// Distributes target reps evenly across the current set count.
    /// </summary>
    public override IEnumerable<PlannedSet> CalculatePlannedSets(int weekNumber, int blockNumber)
    {
        var sets = new List<PlannedSet>();

        // Distribute reps evenly, with remainder going to earlier sets
        int baseRepsPerSet = TargetTotalReps / CurrentSetCount;
        int remainder = TargetTotalReps % CurrentSetCount;

        for (int i = 1; i <= CurrentSetCount; i++)
        {
            // Earlier sets get the extra reps
            int repsForThisSet = baseRepsPerSet + (i <= remainder ? 1 : 0);
            sets.Add(new PlannedSet(i, CurrentWeight, repsForThisSet, isAmrap: false));
        }

        return sets;
    }

    /// <summary>
    /// Applies performance results and adjusts set count based on efficiency.
    /// </summary>
    public override void ApplyPerformanceResult(ExercisePerformance performance)
    {
        var totalRepsCompleted = performance.GetTotalRepsCompleted();
        var setsUsed = performance.GetSetsUsed();

        var evaluation = EvaluatePerformance(totalRepsCompleted, setsUsed);

        switch (evaluation)
        {
            case MinimalSetsEvaluation.Success:
                HandleSuccess();
                break;

            case MinimalSetsEvaluation.Failed:
                HandleFailure();
                break;

            case MinimalSetsEvaluation.Maintained:
                // No change - completed target in expected sets
                break;
        }
    }

    /// <summary>
    /// Gets a summary of current progression state for UI display.
    /// </summary>
    public override ProgressionSummary GetSummary()
    {
        return new ProgressionSummary
        {
            Type = "Minimal Sets",
            Details = new Dictionary<string, string>
            {
                ["Target Total Reps"] = TargetTotalReps.ToString(),
                ["Current Sets"] = CurrentSetCount.ToString(),
                ["Set Range"] = $"{MinimumSets}-{MaximumSets}",
                ["Current Weight"] = CurrentWeight.ToString(),
                ["Equipment"] = Equipment.ToString()
            }
        };
    }

    /// <summary>
    /// Evaluates performance based on total reps completed and sets used.
    /// </summary>
    private MinimalSetsEvaluation EvaluatePerformance(int totalRepsCompleted, int setsUsed)
    {
        // Did not hit target reps - failure
        if (totalRepsCompleted < TargetTotalReps)
        {
            return MinimalSetsEvaluation.Failed;
        }

        // Hit target in fewer sets than expected - success!
        if (setsUsed < CurrentSetCount)
        {
            return MinimalSetsEvaluation.Success;
        }

        // Hit target in exactly expected sets - maintained
        return MinimalSetsEvaluation.Maintained;
    }

    /// <summary>
    /// Handles successful performance (completed target in fewer sets).
    /// Reduces set count by 1 if above minimum.
    /// </summary>
    private void HandleSuccess()
    {
        if (CurrentSetCount > MinimumSets)
        {
            CurrentSetCount--;
        }
        // If already at minimum sets, user has mastered this exercise at this weight
        // They may want to reduce assistance weight (user-controlled)
    }

    /// <summary>
    /// Handles failed performance (could not complete target reps).
    /// Adds a set if below maximum.
    /// </summary>
    private void HandleFailure()
    {
        if (CurrentSetCount < MaximumSets)
        {
            CurrentSetCount++;
        }
        // If at max sets and still failing, user may need to increase assistance
        // weight (user-controlled) or work on form
    }

    /// <summary>
    /// Manually updates the current weight.
    /// Weight changes are user-driven for MinimalSets exercises.
    /// </summary>
    public void UpdateWeight(Weight newWeight)
    {
        CheckRule(newWeight.Unit == CurrentWeight.Unit,
            "New weight must use the same unit as current weight");

        CurrentWeight = newWeight;
    }

    /// <summary>
    /// Manually updates the target total reps.
    /// </summary>
    public void UpdateTargetTotalReps(int newTarget)
    {
        CheckRule(newTarget >= 10 && newTarget <= 200,
            "Target total reps must be between 10 and 200");

        TargetTotalReps = newTarget;
    }

    /// <summary>
    /// Manually resets the set count to starting value.
    /// Useful when weight is changed significantly.
    /// </summary>
    public void ResetSetCount()
    {
        CurrentSetCount = StartingSets;
    }
}

/// <summary>
/// Result of evaluating performance for MinimalSets progression.
/// </summary>
public enum MinimalSetsEvaluation
{
    /// <summary>
    /// Completed target reps in fewer sets than expected - progress.
    /// </summary>
    Success,

    /// <summary>
    /// Completed target reps in expected number of sets - no change.
    /// </summary>
    Maintained,

    /// <summary>
    /// Could not complete target reps - need more sets.
    /// </summary>
    Failed
}
