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
/// Data matches the A2S2 Hypertrophy spreadsheet exactly.
/// </summary>
public sealed class A2SProgramProvider : IA2SProgramProvider
{
    public int TotalWeeks => 21;
    public int WeeksPerBlock => 7;

    /// <summary>
    /// Week-by-week programming data matching the A2S2 Hypertrophy spreadsheet exactly.
    /// Format: (Intensity%, Sets, RepsPerSet, RepOutTarget)
    ///
    /// Source: A2S2 Hypertrophy spreadsheet (validated against A2S2_Validation_Data.md)
    ///
    /// Hypertrophy uses 6 intensity levels: a=0.65, b=0.68, c=0.70, d=0.73, e=0.76, f=0.79
    /// mapped to rep levels: 12, 11, 10, 9, 8, 7.
    /// Rep-out target = RepsPerSet + 2 (except week 1 = +3).
    /// Always 4 sets. Deload at 60% intensity, 5 reps, no AMRAP.
    ///
    /// Block structure (3-week mini-cycles, overlapping):
    ///   Block 1: MC1(a,b,c) MC2(b,c,d) Deload
    ///   Block 2: MC1(b,c,d) MC2(c,d,e) Deload
    ///   Block 3: MC1(c,d,e) MC2(d,e,f) Deload
    /// </summary>
    private static readonly (decimal Intensity, int Sets, int RepsPerSet, int? RepOutTarget)[] WeeklyProgram =
    {
        // Week 0 placeholder (1-indexed access)
        (0.00m, 0, 0, null),

        // BLOCK 1: Weeks 1-7
        (0.65m, 4, 12, 15),   // Week 1
        (0.68m, 4, 11, 13),   // Week 2
        (0.70m, 4, 10, 12),   // Week 3
        (0.68m, 4, 11, 13),   // Week 4
        (0.70m, 4, 10, 12),   // Week 5
        (0.73m, 4,  9, 11),   // Week 6
        (0.60m, 4,  5, null), // Week 7 - DELOAD

        // BLOCK 2: Weeks 8-14
        (0.68m, 4, 11, 13),   // Week 8
        (0.70m, 4, 10, 12),   // Week 9
        (0.73m, 4,  9, 11),   // Week 10
        (0.70m, 4, 10, 12),   // Week 11
        (0.73m, 4,  9, 11),   // Week 12
        (0.76m, 4,  8, 10),   // Week 13
        (0.60m, 4,  5, null), // Week 14 - DELOAD

        // BLOCK 3: Weeks 15-21
        (0.70m, 4, 10, 12),   // Week 15
        (0.73m, 4,  9, 11),   // Week 16
        (0.76m, 4,  8, 10),   // Week 17
        (0.73m, 4,  9, 11),   // Week 18
        (0.76m, 4,  8, 10),   // Week 19
        (0.79m, 4,  7,  9),   // Week 20
        (0.60m, 4,  5, null), // Week 21 - DELOAD (final week)
    };

    private static readonly HashSet<int> DeloadWeeks = new() { 7, 14, 21 };

    private readonly WeekParameters[] _weekParameters;

    public A2SProgramProvider()
    {
        _weekParameters = new WeekParameters[TotalWeeks + 1];

        for (int week = 1; week <= TotalWeeks; week++)
        {
            var (intensity, sets, repsPerSet, repOutTarget) = WeeklyProgram[week];
            var blockNumber = ((week - 1) / WeeksPerBlock) + 1;

            _weekParameters[week] = new WeekParameters
            {
                WeekNumber = week,
                BlockNumber = blockNumber,
                Intensity = intensity,
                Sets = sets,
                TargetReps = repsPerSet,
                RepOutTarget = repOutTarget,
                IsDeload = DeloadWeeks.Contains(week)
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
