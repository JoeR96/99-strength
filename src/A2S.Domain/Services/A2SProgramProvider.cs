namespace A2S.Domain.Services;

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
            var data = A2SHypertrophyProgram.GetWeekData(week, Enums.ProgramTier.Primary);
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
        return _weekParameters.Skip(1).ToList();
    }
}
