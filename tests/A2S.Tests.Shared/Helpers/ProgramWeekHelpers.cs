namespace A2S.Tests.Shared.Helpers;

/// <summary>
/// Pre-computed week lookup data from the A2S2 Hypertrophy program table.
/// Uses bounds-checking to safely handle week numbers outside the 1-21 range.
/// </summary>
public static class ProgramWeekHelpers
{
    private static readonly int?[] RepOutTargets =
    {
        null, 10, 8, 6, 9, 7, 5, null, 8, 6, 4, 7, 5, 3, null, 6, 4, 2, 4, 2, 2, null
    };

    private static readonly int[] RepsPerSet =
    {
        0, 5, 4, 3, 5, 4, 3, 5, 4, 3, 2, 4, 3, 2, 5, 3, 2, 1, 2, 1, 1, 5
    };

    private static readonly decimal[] Intensities =
    {
        0m, 0.79m, 0.84m, 0.87m, 0.79m, 0.84m, 0.87m, 0.58m,
        0.84m, 0.87m, 0.92m, 0.84m, 0.87m, 0.92m, 0.58m,
        0.87m, 0.92m, 0.96m, 0.92m, 0.96m, 0.96m, 0.58m
    };

    public static int GetRepOutTargetForWeek(int week)
    {
        return week < RepOutTargets.Length ? RepOutTargets[week] ?? GetRepsPerSetForWeek(week) : 10;
    }

    public static int GetRepsPerSetForWeek(int week)
    {
        return week < RepsPerSet.Length ? RepsPerSet[week] : 10;
    }

    public static decimal GetIntensityForWeek(int week)
    {
        return week < Intensities.Length ? Intensities[week] : 0.70m;
    }
}
