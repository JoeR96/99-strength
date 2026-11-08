using A2S.Domain.Common;

namespace A2S.Domain.Services;

/// <summary>
/// Estimates how long a lifting session takes: warm-up plus, per set, the work time and the
/// rest that follows it. Exercises sharing a superset group alternate, so each round of the
/// superset costs one work period per exercise plus a single rest (the longest in the round).
/// </summary>
public static class SessionTimeEstimator
{
    public const int DefaultWarmUpMinutes = 10;
    public const int DefaultWorkSeconds = 40;
    public const int DefaultBudgetMinutes = 55;

    public static SessionTimeEstimate Estimate(
        IEnumerable<ExerciseTiming> exercises,
        int warmUpMinutes = DefaultWarmUpMinutes,
        int workSeconds = DefaultWorkSeconds,
        int budgetMinutes = DefaultBudgetMinutes)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        CheckRule(warmUpMinutes >= 0, "Warm-up minutes cannot be negative");
        CheckRule(workSeconds > 0, "Work seconds per set must be positive");
        CheckRule(budgetMinutes >= 0, "Budget minutes cannot be negative");

        var list = exercises.ToList();
        foreach (var exercise in list)
        {
            CheckRule(exercise.Sets >= 0, $"{exercise.Name}: sets cannot be negative");
            CheckRule(exercise.RestSeconds >= 0, $"{exercise.Name}: rest seconds cannot be negative");
        }

        var seconds = new decimal[list.Count];

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].SupersetGroup is null)
            {
                seconds[i] = list[i].Sets * (decimal)(workSeconds + list[i].RestSeconds);
            }
        }

        foreach (var group in list
                     .Select((exercise, index) => (exercise, index))
                     .Where(pair => pair.exercise.SupersetGroup is not null)
                     .GroupBy(pair => pair.exercise.SupersetGroup!.Value))
        {
            AddSupersetSeconds(group.ToList(), workSeconds, seconds);
        }

        var totalSeconds = seconds.Sum();
        var totalMinutes = warmUpMinutes + CeilMinutes(totalSeconds);

        var perExercise = list
            .Select((exercise, index) => (exercise.Name, CeilMinutes(seconds[index])))
            .ToList();

        return new SessionTimeEstimate(
            totalMinutes,
            warmUpMinutes,
            perExercise,
            totalMinutes > budgetMinutes,
            budgetMinutes);
    }

    /// <summary>
    /// Round r of a superset includes every member that still has a set r; the round costs
    /// one work period per member plus the longest rest among them. The shared rest is split
    /// evenly across the members for the per-exercise figures.
    /// </summary>
    private static void AddSupersetSeconds(
        IReadOnlyList<(ExerciseTiming exercise, int index)> members,
        int workSeconds,
        decimal[] seconds)
    {
        var rounds = members.Max(m => m.exercise.Sets);
        for (var round = 1; round <= rounds; round++)
        {
            var active = members.Where(m => m.exercise.Sets >= round).ToList();
            var rest = active.Max(m => m.exercise.RestSeconds);
            var restShare = rest / (decimal)active.Count;

            foreach (var (_, index) in active)
            {
                seconds[index] += workSeconds + restShare;
            }
        }
    }

    private static int CeilMinutes(decimal seconds) => (int)Math.Ceiling(seconds / 60m);

    private static void CheckRule(bool condition, string message)
    {
        if (!condition)
        {
            throw new BusinessRuleViolationException(message);
        }
    }
}
