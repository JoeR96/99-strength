using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Strategies.Cardio;

public class PlannedCardioBlockTests
{
    private static readonly CardioSegment WarmUp = new(3, 3m);
    private static readonly CardioSegment CoolDown = new(2, 3m);

    private static CardioStageDefinition IntervalStage(params IntervalFormat[] formats) =>
        new("Intervals", CardioStageKind.Intervals, CardioStepAxis.IntervalFormat, IntensityUnit.Level,
            null, null, null, null, formats, null, false, null, null, 1m, null, null, false, 3m);

    private static CardioStageState StateAt(int formatIndex, decimal? intensity) =>
        new(0, null, formatIndex, intensity, null, null, 0, 0, [], new Dictionary<int, decimal>());

    [Fact]
    public void From_Intervals_PutsRecoveriesOnlyBetweenRounds()
    {
        var stages = new[] { IntervalStage(new IntervalFormat(4, 4, 3)) };

        var block = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 10m));

        block.Kind.Should().Be(CardioStageKind.Intervals);
        block.WorkMinutes.Should().Be(25);
        block.TotalMinutes.Should().Be(30);
        block.Text.Should().Be("3 min easy (L3) → 4 × 4 min @ L10 / 3 min @ L3 → 2 min easy (L3)");
    }

    [Fact]
    public void From_CopiesStageAndSettings()
    {
        var stages = new[] { IntervalStage(new IntervalFormat(4, 2, 2)) };

        var block = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 8.5m));

        block.Lane.Should().Be(CardioLane.Hard);
        block.StageName.Should().Be("Intervals");
        block.StageNumber.Should().Be(1);
        block.StageCount.Should().Be(1);
        block.WarmUp.Should().Be(WarmUp);
        block.CoolDown.Should().Be(CoolDown);
        block.IntensityUnit.Should().Be(IntensityUnit.Level);
        block.Intensity.Should().Be(8.5m);
        block.Speed.Should().BeNull();
        block.IsSettingsPending.Should().BeFalse();
        block.Text.Should().Be("3 min easy (L3) → 4 × 2 min @ L8.5 / 2 min @ L3 → 2 min easy (L3)");
    }

    [Fact]
    public void Title_Intervals_IsTheWorkWithoutWarmUpOrCoolDown()
    {
        var stages = new[] { IntervalStage(new IntervalFormat(4, 2, 2)) };

        var block = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 8m));

        block.Title.Should().Be("4 × 2 min @ L8 / 2 min @ L3");
    }

    [Fact]
    public void Title_StairContinuous_IsTheWorkOnly()
    {
        var block = CardioStageStrategy.Create(CardioLanes.StairClimber()).CalculateCardioBlock();

        block.Title.Should().Be("6 min @ level 6");
    }

    [Fact]
    public void Title_Walk_IncludesInclineAndSpeed()
    {
        var walk = CardioStageStrategy.Create(CardioLanes.InclineWalk());
        walk.RestoreFromState(walk.State with { Minutes = 30, Intensity = 8m, Speed = 5.9m });

        walk.CalculateCardioBlock().Title.Should().Be("30 min @ 8 % incline, 5.9 km/h");
    }

    [Fact]
    public void Title_WalkWithoutSpeed_IsMinutesAndIncline()
    {
        var walk = CardioStageStrategy.Create(CardioLanes.InclineWalk());
        walk.RestoreFromState(walk.State with { Minutes = 12, Intensity = 5m });

        walk.CalculateCardioBlock().Title.Should().Be("12 min @ 5 % incline");
    }

    [Fact]
    public void From_WithoutIntensity_IsSettingsPending()
    {
        var stages = new[] { IntervalStage(new IntervalFormat(4, 2, 2)) };

        var block = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, null));

        block.IsSettingsPending.Should().BeTrue();
    }

    [Fact]
    public void Blocks_WithSameValues_AreEqual()
    {
        var stages = new[] { IntervalStage(new IntervalFormat(4, 2, 2)) };

        var first = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 8m));
        var second = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 8m));
        var other = PlannedCardioBlock.From(CardioLane.Hard, stages, WarmUp, CoolDown, false, StateAt(0, 9m));

        first.Should().Be(second);
        first.Should().NotBe(other);
    }
}
