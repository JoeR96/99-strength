namespace A2S.Domain.Aggregates.Workout.Cardio;

/// <summary>
/// Pure moves on a cardio ladder: step up, step back, enter a stage, choose settings.
/// Stage changes are automatic; a step back returns to the exact previous prescription.
/// </summary>
public static class CardioLadder
{
    public const int HistoryLimit = 50;

    /// <summary>
    /// One step along the current stage's axis; past its end the stage repeats or the next one is entered.
    /// At the very top (no next stage) the state is returned unchanged. A step up always changes the
    /// prescription: if entering a stage lands on the same prescription, the new stage takes its first step.
    /// </summary>
    public static CardioStageState StepUp(IReadOnlyList<CardioStageDefinition> stages, CardioStageState state)
    {
        var next = StepWithinStage(stages[state.StageIndex], state);

        if (next is null)
        {
            var stage = stages[state.StageIndex];
            var target = stage.RepeatsAtTop ? state.StageIndex : state.StageIndex + 1;
            if (target >= stages.Count)
            {
                return state;
            }

            next = Enter(stages, state, target);
            if (SamePrescription(stages, next, state))
            {
                next = StepWithinStage(stages[target], next) ?? next;
            }
        }

        var history = state.History.Append(PositionOf(state)).TakeLast(HistoryLimit).ToList();
        return next with { History = history, SolidsBanked = 0, MissStreak = 0 };
    }

    /// <summary>Returns to the previous prescription; with no history the state is unchanged.</summary>
    public static CardioStageState StepBack(CardioStageState state)
    {
        if (state.History.Count == 0)
        {
            return state;
        }

        var previous = state.History[^1];
        return state with
        {
            StageIndex = previous.StageIndex,
            Minutes = previous.Minutes,
            FormatIndex = previous.FormatIndex,
            Intensity = previous.Intensity,
            SuggestedIntensity = null,
            Speed = previous.Speed,
            History = state.History.Take(state.History.Count - 1).ToList(),
            SolidsBanked = 0,
            MissStreak = 0
        };
    }

    /// <summary>
    /// Puts the state at the start of a stage. An asking stage reuses the level chosen there before,
    /// else leaves the level blank with a suggestion. Speed moves by the stage's entry step (null stays null).
    /// History and counters are left as they are.
    /// </summary>
    public static CardioStageState Enter(IReadOnlyList<CardioStageDefinition> stages, CardioStageState state, int stageIndex)
    {
        var stage = stages[stageIndex];

        decimal? intensity;
        decimal? suggested = null;
        if (stage.AsksForIntensity && state.ChosenIntensities.TryGetValue(stageIndex, out var chosen))
        {
            intensity = chosen;
        }
        else if (stage.AsksForIntensity)
        {
            intensity = null;
            suggested = stage.AskSuggestValue ?? state.Intensity + stage.AskSuggestOffset;
        }
        else
        {
            intensity = stage.IntensityOnEntry ?? state.Intensity;
        }

        return state with
        {
            StageIndex = stageIndex,
            Minutes = stage.MinMinutes ?? stage.FixedMinutes,
            FormatIndex = stage.Formats is null ? null : 0,
            Intensity = intensity,
            SuggestedIntensity = suggested,
            Speed = state.Speed + (stage.SpeedStepOnEntry ?? 0m)
        };
    }

    /// <summary>Sets the current intensity (remembered for this stage) and/or speed. Null leaves a value as it is.</summary>
    public static CardioStageState Choose(CardioStageState state, decimal? intensity, decimal? speed)
    {
        var next = state;
        if (intensity is { } level)
        {
            var chosen = new Dictionary<int, decimal>(state.ChosenIntensities) { [state.StageIndex] = level };
            next = next with { Intensity = level, SuggestedIntensity = null, ChosenIntensities = chosen };
        }

        if (speed is { } kmh)
        {
            next = next with { Speed = kmh };
        }

        return next;
    }

    private static CardioStageState? StepWithinStage(CardioStageDefinition stage, CardioStageState state) =>
        stage.Axis switch
        {
            CardioStepAxis.Minutes when state.Minutes + stage.MinuteStep <= stage.MaxMinutes =>
                state with { Minutes = state.Minutes + stage.MinuteStep },
            CardioStepAxis.IntervalFormat when state.FormatIndex + 1 < stage.Formats?.Count =>
                state with { FormatIndex = state.FormatIndex + 1 },
            CardioStepAxis.Intensity when state.Intensity is { } level
                                          && (stage.IntensityCap is null || level + stage.IntensityStep <= stage.IntensityCap) =>
                state with { Intensity = level + stage.IntensityStep },
            _ => null
        };

    private static bool SamePrescription(IReadOnlyList<CardioStageDefinition> stages, CardioStageState a, CardioStageState b) =>
        a.Minutes == b.Minutes
        && a.Intensity == b.Intensity
        && a.Speed == b.Speed
        && stages[a.StageIndex].Kind == stages[b.StageIndex].Kind
        && FormatAt(stages[a.StageIndex], a.FormatIndex) == FormatAt(stages[b.StageIndex], b.FormatIndex);

    private static IntervalFormat? FormatAt(CardioStageDefinition stage, int? index) =>
        stage.Formats is { } formats && index is { } i ? formats[i] : null;

    private static CardioPosition PositionOf(CardioStageState state) =>
        new(state.StageIndex, state.Minutes, state.FormatIndex, state.Intensity, state.Speed);
}
