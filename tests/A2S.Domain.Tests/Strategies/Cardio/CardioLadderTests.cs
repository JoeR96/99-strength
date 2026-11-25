using System.Text.Json;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Strategies.Cardio;

public class CardioLadderTests
{
    private static readonly CardioLanePreset Stairs = CardioLanes.StairClimber();
    private static readonly CardioLanePreset Walk = CardioLanes.InclineWalk();

    private static PlannedCardioBlock Block(CardioLanePreset preset, CardioStageState state) =>
        PlannedCardioBlock.From(preset.Lane, preset.Stages, preset.WarmUp, preset.CoolDown, preset.RequiresSpeed, state);

    private static CardioStageState StepUp(CardioLanePreset preset, CardioStageState state, int times)
    {
        for (var i = 0; i < times; i++)
        {
            state = CardioLadder.StepUp(preset.Stages, state);
        }

        return state;
    }

    [Fact]
    public void StairClimber_StartsAtSixMinutesLevelSix()
    {
        var state = CardioStageState.Start(Stairs.Stages);
        var block = Block(Stairs, state);

        state.StageIndex.Should().Be(0);
        state.Minutes.Should().Be(6);
        state.Intensity.Should().Be(6m);
        block.TotalMinutes.Should().Be(11);
        block.IsSettingsPending.Should().BeFalse();
        block.Text.Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void StairClimber_NineStepUps_ReachFullFourByFour()
    {
        var state = StepUp(Stairs, CardioStageState.Start(Stairs.Stages), 6);

        state.Minutes.Should().Be(12);
        Block(Stairs, state).TotalMinutes.Should().Be(17);

        state = CardioLadder.StepUp(Stairs.Stages, state);
        var pending = Block(Stairs, state);

        state.StageIndex.Should().Be(1);
        state.Intensity.Should().BeNull();
        state.SuggestedIntensity.Should().Be(8m);
        pending.IsSettingsPending.Should().BeTrue();
        pending.Intervals.Should().Be(new IntervalFormat(4, 2, 2));
        pending.TotalMinutes.Should().Be(19);

        state = CardioLadder.Choose(state, 8m, null);
        state.SuggestedIntensity.Should().BeNull();
        Block(Stairs, state).Text.Should().Be("3 min easy (L3) → 4 × 2 min @ L8 / 2 min @ L3 → 2 min easy (L3)");

        state = CardioLadder.StepUp(Stairs.Stages, state);
        Block(Stairs, state).Intervals.Should().Be(new IntervalFormat(4, 3, 3));
        Block(Stairs, state).TotalMinutes.Should().Be(26);

        state = CardioLadder.StepUp(Stairs.Stages, state);
        var full = Block(Stairs, state);
        state.StageIndex.Should().Be(2);
        state.Intensity.Should().Be(8m);
        full.Intervals.Should().Be(new IntervalFormat(4, 4, 3));
        full.RecoveryLevel.Should().Be(3m);
        full.TotalMinutes.Should().Be(30);
        full.StageNumber.Should().Be(3);
        full.StageCount.Should().Be(3);
    }

    [Fact]
    public void StairClimber_StageThree_StepUpRaisesHardLevel_CappedAtTwenty()
    {
        var state = StepUp(Stairs, CardioStageState.Start(Stairs.Stages), 7);
        state = CardioLadder.Choose(state, 8m, null);
        state = StepUp(Stairs, state, 2);

        state = CardioLadder.StepUp(Stairs.Stages, state);
        state.Intensity.Should().Be(9m);
        state.StageIndex.Should().Be(2);

        state = StepUp(Stairs, state, 11);
        state.Intensity.Should().Be(20m);

        var atTop = CardioLadder.StepUp(Stairs.Stages, state);
        atTop.Should().BeSameAs(state);
    }

    [Fact]
    public void StepUp_ResetsCountersAndKeepsAtMostFiftyPositions()
    {
        var state = CardioStageState.Start(Walk.Stages) with { SolidsBanked = 1, MissStreak = 1 };
        state = CardioLadder.Choose(state, 5m, 5.6m);

        state = CardioLadder.StepUp(Walk.Stages, state);
        state.SolidsBanked.Should().Be(0);
        state.MissStreak.Should().Be(0);
        state.History.Should().ContainSingle().Which.Should().Be(new CardioPosition(0, 10, null, 5m, 5.6m));

        state = StepUp(Walk, state, 59);
        state.History.Should().HaveCount(50);
        state.History[0].Should().NotBe(new CardioPosition(0, 10, null, 5m, 5.6m));
    }

    [Fact]
    public void StepBack_FromFirstIntervalFormat_ReturnsToTwelveMinutesContinuous()
    {
        var state = StepUp(Stairs, CardioStageState.Start(Stairs.Stages), 7);
        state = CardioLadder.Choose(state, 8m, null) with { SolidsBanked = 1, MissStreak = 1 };

        state = CardioLadder.StepBack(state);

        state.StageIndex.Should().Be(0);
        state.Minutes.Should().Be(12);
        state.FormatIndex.Should().BeNull();
        state.Intensity.Should().Be(6m);
        state.SolidsBanked.Should().Be(0);
        state.MissStreak.Should().Be(0);
        state.History.Should().HaveCount(6);
        Block(Stairs, state).Text.Should().Be("3 min easy (L3) → 12 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void StepBack_WithNoHistory_IsUnchanged()
    {
        var state = CardioStageState.Start(Stairs.Stages);

        CardioLadder.StepBack(state).Should().BeSameAs(state);
    }

    [Fact]
    public void ReenteringStageTwo_ReusesChosenHardLevel()
    {
        var state = StepUp(Stairs, CardioStageState.Start(Stairs.Stages), 7);
        state = CardioLadder.Choose(state, 8m, null);
        state = CardioLadder.StepBack(state);

        state = CardioLadder.StepUp(Stairs.Stages, state);

        state.StageIndex.Should().Be(1);
        state.Intensity.Should().Be(8m);
        state.SuggestedIntensity.Should().BeNull();
        Block(Stairs, state).IsSettingsPending.Should().BeFalse();
    }

    [Fact]
    public void InclineWalk_StartsPending_SuggestsFivePercent()
    {
        var state = CardioStageState.Start(Walk.Stages);
        var block = Block(Walk, state);

        state.Minutes.Should().Be(10);
        state.Intensity.Should().BeNull();
        state.SuggestedIntensity.Should().Be(5m);
        state.Speed.Should().BeNull();
        block.IsSettingsPending.Should().BeTrue();
        block.WarmUp.Should().BeNull();
        block.CoolDown.Should().BeNull();
        block.TotalMinutes.Should().Be(10);
    }

    [Fact]
    public void InclineWalk_WithInclineButNoSpeed_IsStillPending()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), 5m, null);
        state = StepUp(Walk, state, 1);
        var block = Block(Walk, state);

        block.IsSettingsPending.Should().BeTrue();
        block.Text.Should().Be("12 min @ 5 % incline");
    }

    [Fact]
    public void InclineWalk_TenStepUps_ReachThirtyMinutes()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), 5m, 5.6m);

        state = StepUp(Walk, state, 10);

        state.StageIndex.Should().Be(0);
        state.Minutes.Should().Be(30);
        Block(Walk, state).Text.Should().Be("30 min @ 5 % incline, 5.6 km/h");
    }

    [Fact]
    public void InclineWalk_SteeperThenFaster_ResetsInclineToEightAndAddsSpeed()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), 5m, 5.6m);

        state = StepUp(Walk, state, 11);
        state.StageIndex.Should().Be(1);
        state.Minutes.Should().Be(30);
        state.Intensity.Should().Be(6m);

        state = StepUp(Walk, state, 6);
        state.Intensity.Should().Be(12m);

        state = CardioLadder.StepUp(Walk.Stages, state);
        state.StageIndex.Should().Be(2);
        state.Intensity.Should().Be(8m);
        state.Speed.Should().Be(5.9m);
        Block(Walk, state).Text.Should().Be("30 min @ 8 % incline, 5.9 km/h");

        state = StepUp(Walk, state, 4);
        state.Intensity.Should().Be(12m);

        state = CardioLadder.StepUp(Walk.Stages, state);
        state.StageIndex.Should().Be(2);
        state.Intensity.Should().Be(8m);
        state.Speed.Should().Be(6.2m);
    }

    [Fact]
    public void InclineWalk_EditedInclineInStageThree_StillResetsToEightOnRepeat()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), 5m, 5.6m);
        state = StepUp(Walk, state, 18);
        state = CardioLadder.Choose(state, 9m, null);

        state = StepUp(Walk, state, 4);

        state.Intensity.Should().Be(8m);
        state.Speed.Should().Be(6.2m);
    }

    [Fact]
    public void Enter_WithoutSpeed_KeepsSpeedNull()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), 5m, null);

        state = CardioLadder.Enter(Walk.Stages, state, 2);

        state.Speed.Should().BeNull();
        state.Intensity.Should().Be(8m);
    }

    [Fact]
    public void Choose_WithOnlySpeed_KeepsIntensityAndSuggestion()
    {
        var state = CardioLadder.Choose(CardioStageState.Start(Walk.Stages), null, 5.6m);

        state.Speed.Should().Be(5.6m);
        state.Intensity.Should().BeNull();
        state.SuggestedIntensity.Should().Be(5m);
        state.ChosenIntensities.Should().BeEmpty();
    }

    [Fact]
    public void State_RoundTripsThroughJson()
    {
        var state = StepUp(Stairs, CardioStageState.Start(Stairs.Stages), 7);
        state = CardioLadder.Choose(state, 8m, null);
        state = StepUp(Stairs, state, 1);

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<CardioStageState>(json)!;

        restored.Should().BeEquivalentTo(state);
        restored.ChosenIntensities[1].Should().Be(8m);
        CardioLadder.StepBack(restored).FormatIndex.Should().Be(0);
    }

    [Fact]
    public void Presets_RoundTripThroughJson()
    {
        var json = JsonSerializer.Serialize(Stairs);
        var restored = JsonSerializer.Deserialize<CardioLanePreset>(json)!;

        restored.Should().BeEquivalentTo(Stairs);
    }
}
