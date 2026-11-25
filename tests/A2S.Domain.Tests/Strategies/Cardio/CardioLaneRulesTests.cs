using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Strategies.Cardio;

public class CardioLaneRulesTests
{
    private static CardioResult Done(EffortRating effort) => new(true, effort);

    private static CardioResult StoppedEarly() => new(false, EffortRating.Solid);

    private static CardioResult Pain() => new(true, EffortRating.Easy, PainFlag.Niggle, "knee");

    [Theory]
    [InlineData(EffortRating.Easy, 0, CardioStepDecision.StepUp, 0)]
    [InlineData(EffortRating.Easy, 1, CardioStepDecision.StepUp, 0)]
    [InlineData(EffortRating.Solid, 0, CardioStepDecision.Hold, 1)]
    [InlineData(EffortRating.Solid, 1, CardioStepDecision.StepUp, 0)]
    [InlineData(EffortRating.Brutal, 0, CardioStepDecision.Hold, 0)]
    [InlineData(EffortRating.Brutal, 1, CardioStepDecision.Hold, 1)]
    public void HardLane_Effort(EffortRating effort, int banked, CardioStepDecision expected, int bankedAfter)
    {
        var decision = CardioLaneRules.Decide(CardioLane.Hard, Done(effort), banked, 0, out var solids, out var misses);

        decision.Should().Be(expected);
        solids.Should().Be(bankedAfter);
        misses.Should().Be(0);
    }

    [Fact]
    public void HardLane_TwoMissesInARow_StepsBack()
    {
        var first = CardioLaneRules.Decide(CardioLane.Hard, StoppedEarly(), 0, 0, out _, out var missesAfterFirst);
        var second = CardioLaneRules.Decide(CardioLane.Hard, Pain(), 0, missesAfterFirst, out _, out var missesAfterSecond);

        first.Should().Be(CardioStepDecision.Hold);
        missesAfterFirst.Should().Be(1);
        second.Should().Be(CardioStepDecision.StepBack);
        missesAfterSecond.Should().Be(0);
    }

    [Fact]
    public void HardLane_MissKeepsBankedSolids()
    {
        var decision = CardioLaneRules.Decide(CardioLane.Hard, StoppedEarly(), 1, 0, out var solids, out var misses);

        decision.Should().Be(CardioStepDecision.Hold);
        solids.Should().Be(1);
        misses.Should().Be(1);
    }

    [Fact]
    public void AMiss_WinsOverAnEasyRating()
    {
        var decision = CardioLaneRules.Decide(CardioLane.Hard, Pain(), 0, 0, out _, out var misses);

        decision.Should().Be(CardioStepDecision.Hold);
        misses.Should().Be(1);
    }

    [Theory]
    [InlineData(EffortRating.Easy, CardioStepDecision.StepUp)]
    [InlineData(EffortRating.Solid, CardioStepDecision.Hold)]
    [InlineData(EffortRating.Brutal, CardioStepDecision.StepBack)]
    public void EasyLane_Effort(EffortRating effort, CardioStepDecision expected)
    {
        var decision = CardioLaneRules.Decide(CardioLane.Easy, Done(effort), 0, 0, out var solids, out var misses);

        decision.Should().Be(expected);
        solids.Should().Be(0);
        misses.Should().Be(0);
    }

    [Fact]
    public void EasyLane_TwoMissesInARow_StepsBack()
    {
        var first = CardioLaneRules.Decide(CardioLane.Easy, Pain(), 0, 0, out _, out var missesAfterFirst);
        var second = CardioLaneRules.Decide(CardioLane.Easy, StoppedEarly(), 0, missesAfterFirst, out _, out var missesAfterSecond);

        first.Should().Be(CardioStepDecision.Hold);
        missesAfterFirst.Should().Be(1);
        second.Should().Be(CardioStepDecision.StepBack);
        missesAfterSecond.Should().Be(0);
    }

    [Theory]
    [InlineData(CardioLane.Hard, EffortRating.Brutal)]
    [InlineData(CardioLane.Easy, EffortRating.Solid)]
    public void AnEffortAfterAMiss_ResetsTheMissStreak(CardioLane lane, EffortRating effort)
    {
        var decision = CardioLaneRules.Decide(lane, Done(effort), 0, 1, out _, out var misses);

        decision.Should().Be(CardioStepDecision.Hold);
        misses.Should().Be(0);
    }
}
