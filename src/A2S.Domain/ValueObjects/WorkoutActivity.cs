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

    // Use List<T> internally for EF Core JSON deserialization compatibility
    private readonly List<ExercisePerformance> _performances = new();
    private readonly List<ProgressionSnapshot> _progressionSnapshots = new();
    public IReadOnlyList<ExercisePerformance> Performances => _performances;
    public IReadOnlyList<ProgressionSnapshot> ProgressionSnapshots => _progressionSnapshots;
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
        IEnumerable<ProgressionSnapshot>? progressionSnapshots = null,
        DateTime? completedAt = null)
    {
        var performancesList = performances.ToList();

        CheckRule(weekNumber > 0, "Week number must be positive");
        CheckRule(blockNumber >= 1 && blockNumber <= 3, "Block number must be between 1 and 3");
        CheckRule(performancesList.Any(), "At least one exercise performance is required");

        Day = day;
        WeekNumber = weekNumber;
        BlockNumber = blockNumber;
        _performances = performancesList;
        _progressionSnapshots = progressionSnapshots?.ToList() ?? new List<ProgressionSnapshot>();
        CompletedAt = completedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this activity was during a deload week.
    /// </summary>
    public bool IsDeloadWeek()
    {
        return WeekNumber % 7 == 0; // Weeks 7, 14, 21
    }

    /// <summary>
    /// Creates a new WorkoutActivity with a replaced progression snapshot at the given index.
    /// Used by retrofix operations to correct historical snapshot data.
    /// </summary>
    public WorkoutActivity WithReplacedSnapshot(int index, ProgressionSnapshot replacement)
    {
        var newSnapshots = _progressionSnapshots.ToList();
        newSnapshots[index] = replacement;
        return new WorkoutActivity(Day, WeekNumber, BlockNumber, _performances, newSnapshots, CompletedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Day;
        yield return WeekNumber;
        yield return BlockNumber;
        yield return CompletedAt;
        foreach (var perf in Performances)
            yield return perf;
        foreach (var snap in ProgressionSnapshots)
            yield return snap;
    }
}
