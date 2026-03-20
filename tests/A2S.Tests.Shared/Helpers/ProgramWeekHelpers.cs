namespace A2S.Tests.Shared.Helpers;

/// <summary>
/// Pre-computed week lookup data from the A2S2 Hypertrophy program table.
/// Uses bounds-checking to safely handle week numbers outside the 1-21 range.
/// </summary>
public static class ProgramWeekHelpers
{
    private static readonly int?[] RepOutTargets =
    {
        null, 15, 13, 12, 13, 12, 11, null, 13, 12, 11, 12, 11, 10, null, 12, 11, 10, 11, 10, 9, null
    };

    private static readonly int[] RepsPerSet =
    {
        0, 12, 11, 10, 11, 10, 9, 5, 11, 10, 9, 10, 9, 8, 5, 10, 9, 8, 9, 8, 7, 5
    };

    private static readonly decimal[] Intensities =
    {
        0m, 0.65m, 0.68m, 0.70m, 0.68m, 0.70m, 0.73m, 0.60m,
        0.68m, 0.70m, 0.73m, 0.70m, 0.73m, 0.76m, 0.60m,
        0.70m, 0.73m, 0.76m, 0.73m, 0.76m, 0.79m, 0.60m
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
