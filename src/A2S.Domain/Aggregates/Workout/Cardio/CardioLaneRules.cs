using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout.Cardio;

public enum CardioStepDecision
{
    Hold = 1,
    StepUp = 2,
    StepBack = 3
}

/// <summary>
/// The lane rules. A miss (pain or stopped early) holds, and two in a row step back one, in both lanes.
/// Hard lane: Easy → step up; Solid → bank one, two banked → step up; Brutal → hold, banked Solids kept.
/// Easy lane: Easy → step up; Solid → hold; Brutal → step back one.
/// A step up or back clears both counters; any rated session that is not a miss clears the miss streak.
/// </summary>
public static class CardioLaneRules
{
    public const int SolidsToStepUp = 2;
    public const int MissesToStepBack = 2;

    public static CardioStepDecision Decide(
        CardioLane lane,
        CardioResult result,
        int solidsBanked,
        int missStreak,
        out int newSolids,
        out int newMisses)
    {
        var decision = result.IsMiss
            ? DecideMiss(solidsBanked, missStreak, out newSolids, out newMisses)
            : DecideEffort(lane, result.Effort, solidsBanked, out newSolids, out newMisses);

        if (decision != CardioStepDecision.Hold)
        {
            newSolids = 0;
            newMisses = 0;
        }

        return decision;
    }

    private static CardioStepDecision DecideMiss(int solidsBanked, int missStreak, out int newSolids, out int newMisses)
    {
        newSolids = solidsBanked;
        newMisses = missStreak + 1;
        return newMisses >= MissesToStepBack ? CardioStepDecision.StepBack : CardioStepDecision.Hold;
    }

    private static CardioStepDecision DecideEffort(
        CardioLane lane, EffortRating effort, int solidsBanked, out int newSolids, out int newMisses)
    {
        newSolids = solidsBanked;
        newMisses = 0;

        switch (effort)
        {
            case EffortRating.Easy:
                return CardioStepDecision.StepUp;

            case EffortRating.Solid when lane == CardioLane.Hard:
                newSolids = solidsBanked + 1;
                return newSolids >= SolidsToStepUp ? CardioStepDecision.StepUp : CardioStepDecision.Hold;

            case EffortRating.Brutal when lane == CardioLane.Easy:
                return CardioStepDecision.StepBack;

            default:
                return CardioStepDecision.Hold;
        }
    }
}
