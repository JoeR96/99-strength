namespace A2S.Domain.Aggregates.Workout.Cardio;

/// <summary>A prescription on the ladder, kept in <see cref="CardioStageState.History"/> so a step back can return to it.</summary>
public record CardioPosition(int StageIndex, int? Minutes, int? FormatIndex, decimal? Intensity, decimal? Speed);

/// <summary>
/// Where a cardio exercise sits on its ladder. Intensity (and, for the walk, speed) is null until the
/// person sets it; <see cref="SuggestedIntensity"/> is what gym mode offers meanwhile.
/// <see cref="ChosenIntensities"/> remembers the level chosen per stage, so re-entering an asking stage reuses it.
/// </summary>
public record CardioStageState(
    int StageIndex,
    int? Minutes,
    int? FormatIndex,
    decimal? Intensity,
    decimal? SuggestedIntensity,
    decimal? Speed,
    int SolidsBanked,
    int MissStreak,
    IReadOnlyList<CardioPosition> History,
    IReadOnlyDictionary<int, decimal> ChosenIntensities)
{
    public static CardioStageState Start(IReadOnlyList<CardioStageDefinition> stages)
    {
        var blank = new CardioStageState(0, null, null, null, null, null, 0, 0, [], new Dictionary<int, decimal>());
        return CardioLadder.Enter(stages, blank, 0);
    }
}
