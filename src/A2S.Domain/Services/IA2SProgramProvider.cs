namespace A2S.Domain.Services;

/// <summary>
/// Provides the A2S (Average to Savage) program parameters.
/// This is the single source of truth for all week-by-week programming data.
/// Inject this wherever you need program parameters instead of duplicating the data.
/// </summary>
public interface IA2SProgramProvider
{
    /// <summary>
    /// Gets the parameters for a specific week.
    /// </summary>
    /// <param name="weekNumber">Week number (1-21)</param>
    /// <returns>The week parameters</returns>
    WeekParameters GetWeekParameters(int weekNumber);

    /// <summary>
    /// Gets all week parameters.
    /// </summary>
    IReadOnlyList<WeekParameters> GetAllWeekParameters();

    /// <summary>
    /// Total number of weeks in the program.
    /// </summary>
    int TotalWeeks { get; }

    /// <summary>
    /// Weeks per block (before deload).
    /// </summary>
    int WeeksPerBlock { get; }
}

/// <summary>
/// Parameters for a single week in the A2S program.
/// </summary>
public sealed record WeekParameters
{
    /// <summary>
    /// Week number (1-21).
    /// </summary>
    public int WeekNumber { get; init; }

    /// <summary>
    /// Block number (1-3).
    /// </summary>
    public int BlockNumber { get; init; }

    /// <summary>
    /// Intensity as percentage of Training Max (0.0 - 0.79 for hypertrophy).
    /// </summary>
    public decimal Intensity { get; init; }

    /// <summary>
    /// Number of sets for main lifts (always 4 in hypertrophy).
    /// </summary>
    public int Sets { get; init; }

    /// <summary>
    /// Reps per set for normal (non-AMRAP) sets.
    /// </summary>
    public int TargetReps { get; init; }

    /// <summary>
    /// Rep-out target: the AMRAP baseline for delta calculation.
    /// Null for deload weeks (no AMRAP).
    /// Generally TargetReps + 2 (except week 1 which is +3).
    /// </summary>
    public int? RepOutTarget { get; init; }

    /// <summary>
    /// Whether this is a deload week.
    /// </summary>
    public bool IsDeload { get; init; }

    /// <summary>
    /// Intensity as a percentage (e.g., 65 for 65%).
    /// </summary>
    public decimal IntensityPercentage => Intensity * 100;
}

/// <summary>
/// Default implementation of the A2S program provider.
/// Delegates to A2SHypertrophyProgram for data — single source of truth.
/// </summary>
public sealed class A2SProgramProvider : IA2SProgramProvider
{
    public int TotalWeeks => A2SHypertrophyProgram.TotalWeeks;
    public int WeeksPerBlock => A2SHypertrophyProgram.WeeksPerBlock;

    private readonly WeekParameters[] _weekParameters;

    public A2SProgramProvider()
    {
        _weekParameters = new WeekParameters[TotalWeeks + 1];

        for (int week = 1; week <= TotalWeeks; week++)
        {
            var data = A2SHypertrophyProgram.GetWeekData(week);
            var blockNumber = ((week - 1) / WeeksPerBlock) + 1;

            _weekParameters[week] = new WeekParameters
            {
                WeekNumber = week,
                BlockNumber = blockNumber,
                Intensity = data.Intensity,
                Sets = data.Sets,
                TargetReps = data.RepsPerSet,
                RepOutTarget = data.RepOutTarget,
                IsDeload = A2SHypertrophyProgram.IsDeloadWeek(week)
            };
        }
    }

    public WeekParameters GetWeekParameters(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > TotalWeeks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                $"Week number must be between 1 and {TotalWeeks}, got {weekNumber}");
        }

        return _weekParameters[weekNumber];
    }

    public IReadOnlyList<WeekParameters> GetAllWeekParameters()
    {
        return _weekParameters.Skip(1).ToList(); // Skip the placeholder at index 0
    }
}
