using A2S.Domain.Enums;

namespace A2S.Domain.Services;

/// <summary>
/// Single source of truth for the A2S2 21-week program table.
/// Supports Primary (T1) and Auxiliary (T2) tiers with different rep ranges.
/// </summary>
/// <remarks>
/// Program structure:
///   21 weeks = 3 blocks × (6 working weeks + 1 deload)
///   Each block has 2 mini-cycles (MC) of 3 weeks, ascending intensity.
///
/// Primary (T1): reps 5→1 across blocks (floor=1)
/// Auxiliary (T2): reps 7→2 across blocks (floor=2)
///
/// Rep formula:
///   MC1 and MC2 (blocks 1-2): max(floor, base - block - position)
///   MC2 (block 3): max(floor, base - block - position - 1)
///   where base=7 for T1, base=9 for T2; position=1-3 within mini-cycle
///
/// Rep-out target:
///   MC1: reps × 2
///   MC2 (blocks 1-2): reps × 2 - 1
///   MC2 (block 3): reps × 2
///
/// Sets: 5 working, 4 deload.
/// Deload: 5 reps, no AMRAP.
/// </remarks>
public static class A2SHypertrophyProgram
{
    public const int TotalWeeks = 21;
    public const int WeeksPerBlock = 7;
    public const int WorkingSets = 5;
    public const int DeloadSets = 4;
    public const int DeloadReps = 5;
    public const decimal DeloadIntensity = 0.58m;

    /// <summary>
    /// Parameters for a single week in the program table.
    /// </summary>
    public readonly record struct WeekData(decimal Intensity, int Sets, int RepsPerSet, int? RepOutTarget);

    /// <summary>
    /// Gets the week data for a specific week number (1-21) and program tier.
    /// </summary>
    public static WeekData GetWeekData(int weekNumber, ProgramTier tier = ProgramTier.Primary)
    {
        ValidateWeekNumber(weekNumber);

        if (IsDeloadWeek(weekNumber))
        {
            return new WeekData(DeloadIntensity, DeloadSets, DeloadReps, null);
        }

        var block = (weekNumber - 1) / WeeksPerBlock + 1;
        var weekInBlock = (weekNumber - 1) % WeeksPerBlock + 1; // 1-7
        var isMC2 = weekInBlock > 3;
        var position = isMC2 ? weekInBlock - 3 : weekInBlock; // 1-3

        var reps = CalculateReps(block, position, isMC2, tier);
        var repOutTarget = CalculateRepOutTarget(reps, block, isMC2);
        var intensity = GetIntensityForReps(reps, tier);

        return new WeekData(intensity, WorkingSets, reps, repOutTarget);
    }

    /// <summary>
    /// Gets the number of sets for a given week (5 working, 4 deload).
    /// </summary>
    public static int GetSetsForWeek(int weekNumber)
    {
        ValidateWeekNumber(weekNumber);
        return IsDeloadWeek(weekNumber) ? DeloadSets : WorkingSets;
    }

    /// <summary>
    /// Gets the rep-out target (AMRAP baseline) for a given week.
    /// Returns null for deload weeks.
    /// </summary>
    public static int? GetRepOutTarget(int weekNumber, ProgramTier tier = ProgramTier.Primary)
    {
        return GetWeekData(weekNumber, tier).RepOutTarget;
    }

    /// <summary>
    /// Gets the reps per set (for normal sets) for a given week.
    /// </summary>
    public static int GetRepsPerSet(int weekNumber, ProgramTier tier = ProgramTier.Primary)
    {
        return GetWeekData(weekNumber, tier).RepsPerSet;
    }

    /// <summary>
    /// Gets the intensity percentage for a given week.
    /// </summary>
    public static decimal GetIntensity(int weekNumber, ProgramTier tier = ProgramTier.Primary)
    {
        return GetWeekData(weekNumber, tier).Intensity;
    }

    /// <summary>
    /// Whether the given week is a deload week (weeks 7, 14, 21).
    /// </summary>
    public static bool IsDeloadWeek(int weekNumber)
        => weekNumber == 7 || weekNumber == 14 || weekNumber == 21;

    /// <summary>
    /// Calculate reps for a working week based on block, position, mini-cycle, and tier.
    /// T1 (Primary): max(1, 7 - block - position), with -1 adjustment for B3 MC2
    /// T2 (Auxiliary): max(2, 9 - block - position), with -1 adjustment for B3 MC2
    /// </summary>
    private static int CalculateReps(int block, int position, bool isMC2, ProgramTier tier)
    {
        var (baseValue, floor) = tier == ProgramTier.Primary ? (7, 1) : (9, 2);
        var mc2Offset = isMC2 && block == 3 ? 1 : 0;
        return Math.Max(floor, baseValue - block - position - mc2Offset);
    }

    /// <summary>
    /// Calculate rep-out target (AMRAP baseline) from reps.
    /// MC1: reps × 2
    /// MC2 blocks 1-2: reps × 2 - 1
    /// MC2 block 3: reps × 2
    /// </summary>
    private static int CalculateRepOutTarget(int reps, int block, bool isMC2)
    {
        if (!isMC2 || block == 3)
        {
            return reps * 2;
        }

        return reps * 2 - 1;
    }

    /// <summary>
    /// Approximate intensity percentage based on rep count and tier.
    /// These are placeholders — exact values will be refined against the spreadsheet.
    /// </summary>
    private static decimal GetIntensityForReps(int reps, ProgramTier tier)
    {
        // Simple rep-based intensity lookup.
        // These produce reasonable working weights but are not exact spreadsheet values.
        return reps switch
        {
            >= 7 => 0.70m,
            6 => 0.75m,
            5 => 0.79m,
            4 => 0.84m,
            3 => 0.87m,
            2 => 0.92m,
            1 => 0.96m,
            _ => 0.70m
        };
    }

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
