namespace A2S.Domain.Services;

/// <summary>
/// Single source of truth for the A2S2 Hypertrophy 21-week program table.
/// Static class so it can be referenced by EF Core entities (which cannot use DI)
/// and by DI-based services alike.
/// </summary>
/// <remarks>
/// Source: A2S2 Hypertrophy spreadsheet (validated against A2S2_Validation_Data.md)
///
/// Hypertrophy uses 6 intensity levels: a=0.65, b=0.68, c=0.70, d=0.73, e=0.76, f=0.79
/// mapped to rep levels: 12, 11, 10, 9, 8, 7.
/// Rep-out target = RepsPerSet + 2 (except week 1 which is +3).
/// Always 4 sets. Deload at 60% intensity, 5 reps, no AMRAP.
///
/// Block structure (3-week mini-cycles, overlapping):
///   Block 1: MC1(a,b,c) MC2(b,c,d) Deload
///   Block 2: MC1(b,c,d) MC2(c,d,e) Deload
///   Block 3: MC1(c,d,e) MC2(d,e,f) Deload
/// </remarks>
public static class A2SHypertrophyProgram
{
    public const int TotalWeeks = 21;
    public const int WeeksPerBlock = 7;

    /// <summary>
    /// Parameters for a single week in the program table.
    /// </summary>
    public readonly record struct WeekData(decimal Intensity, int Sets, int RepsPerSet, int? RepOutTarget);

    private static readonly WeekData[] ProgramTable =
    [
        // Week 0 placeholder (1-indexed access)
        new(0.00m, 0, 0, null),

        // BLOCK 1: Weeks 1-7
        new(0.65m, 4, 12, 15),   // Week 1
        new(0.68m, 4, 11, 13),   // Week 2
        new(0.70m, 4, 10, 12),   // Week 3
        new(0.68m, 4, 11, 13),   // Week 4
        new(0.70m, 4, 10, 12),   // Week 5
        new(0.73m, 4,  9, 11),   // Week 6
        new(0.60m, 4,  5, null), // Week 7 - DELOAD (no AMRAP)

        // BLOCK 2: Weeks 8-14
        new(0.68m, 4, 11, 13),   // Week 8
        new(0.70m, 4, 10, 12),   // Week 9
        new(0.73m, 4,  9, 11),   // Week 10
        new(0.70m, 4, 10, 12),   // Week 11
        new(0.73m, 4,  9, 11),   // Week 12
        new(0.76m, 4,  8, 10),   // Week 13
        new(0.60m, 4,  5, null), // Week 14 - DELOAD

        // BLOCK 3: Weeks 15-21
        new(0.70m, 4, 10, 12),   // Week 15
        new(0.73m, 4,  9, 11),   // Week 16
        new(0.76m, 4,  8, 10),   // Week 17
        new(0.73m, 4,  9, 11),   // Week 18
        new(0.76m, 4,  8, 10),   // Week 19
        new(0.79m, 4,  7,  9),   // Week 20
        new(0.60m, 4,  5, null), // Week 21 - DELOAD (final week)
    ];

    /// <summary>
    /// Gets the week data for a specific week number (1-21).
    /// </summary>
    public static WeekData GetWeekData(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return ProgramTable[weekNumber];
    }

    /// <summary>
    /// Gets the number of sets for a given week (always 4 in hypertrophy).
    /// </summary>
    public static int GetSetsForWeek(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return ProgramTable[weekNumber].Sets;
    }

    /// <summary>
    /// Gets the rep-out target (AMRAP baseline) for a given week.
    /// Returns null for deload weeks.
    /// </summary>
    public static int? GetRepOutTarget(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return ProgramTable[weekNumber].RepOutTarget;
    }

    /// <summary>
    /// Gets the reps per set (for normal sets) for a given week.
    /// </summary>
    public static int GetRepsPerSet(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return ProgramTable[weekNumber].RepsPerSet;
    }

    /// <summary>
    /// Gets the intensity percentage for a given week.
    /// </summary>
    public static decimal GetIntensity(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return ProgramTable[weekNumber].Intensity;
    }

    /// <summary>
    /// Whether the given week is a deload week (weeks 7, 14, 21).
    /// </summary>
    public static bool IsDeloadWeek(int weekNumber)
        => weekNumber == 7 || weekNumber == 14 || weekNumber == 21;

    private static void ValidateWeekNumber(int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > TotalWeeks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                $"Week number must be between 1 and {TotalWeeks}, got {weekNumber}");
        }
    }
}
