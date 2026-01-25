using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Domain.ValueObjects;

/// <summary>
/// Represents a completed training day with all exercise performances.
/// Immutable record of what was accomplished.
/// </summary>
public sealed class WorkoutActivity : ValueObject
{
    public DayNumber Day { get; private init; }
    public int WeekNumber { get; private init; }
    public int BlockNumber { get; private init; }

    // Use List<T> for EF Core JSON deserialization compatibility (arrays are fixed-size)
    public List<ExercisePerformance> Performances { get; private init; } = new();
    public DateTime CompletedAt { get; private init; }

    // EF Core constructor for JSON deserialization
    private WorkoutActivity()
    {
    }

    public WorkoutActivity(
        DayNumber day,
        int weekNumber,
        int blockNumber,
        IEnumerable<ExercisePerformance> performances,
        DateTime? completedAt = null)
    {
        var performancesList = performances.ToList();

        CheckRule(weekNumber > 0 && weekNumber <= 21, "Week number must be between 1 and 21");
        CheckRule(blockNumber >= 1 && blockNumber <= 3, "Block number must be between 1 and 3");
        CheckRule(performancesList.Any(), "At least one exercise performance is required");

        Day = day;
        WeekNumber = weekNumber;
        BlockNumber = blockNumber;
        Performances = performancesList;
        CompletedAt = completedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this activity was during a deload week.
    /// </summary>
    public bool IsDeloadWeek()
    {
        return WeekNumber % 7 == 0; // Weeks 7, 14, 21
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Day;
        yield return WeekNumber;
        yield return BlockNumber;
        yield return CompletedAt;
        foreach (var perf in Performances)
            yield return perf;
    }
}
