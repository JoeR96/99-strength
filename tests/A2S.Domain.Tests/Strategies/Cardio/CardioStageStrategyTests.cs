using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Strategies.Cardio;

public class CardioStageStrategyTests
{
    private static readonly ExerciseId ExerciseId = new(Guid.Parse("c0c0c0c0-0000-0000-0000-000000000002"));
    private static readonly DateTime FixedDate = new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Weight TestWeight = Weight.Kilograms(20m);

    private static ExercisePerformance Performance(CardioResult result) =>
        new(ExerciseId, [], [], FixedDate, cardio: result);

    private static CardioStageStrategy Stairs() => CardioStageStrategy.Create(CardioLanes.StairClimber());

    private static CardioStageStrategy WalkWithSettings()
    {
        var strategy = CardioStageStrategy.Create(CardioLanes.InclineWalk());
        strategy.SetSettings(5m, 5.5m);
        return strategy;
    }

    [Fact]
    public void Create_StairClimber_CopiesThePreset()
    {
        var preset = CardioLanes.StairClimber();

        var strategy = CardioStageStrategy.Create(preset);

        strategy.ProgressionType.Should().Be("CardioStage");
        strategy.Lane.Should().Be(CardioLane.Hard);
        strategy.Stages.Should().BeEquivalentTo(preset.Stages);
        strategy.WarmUp.Should().Be(new CardioSegment(3, 3m));
        strategy.CoolDown.Should().Be(new CardioSegment(2, 3m));
        strategy.RequiresSpeed.Should().BeFalse();
        strategy.State.Should().BeEquivalentTo(CardioStageState.Start(preset.Stages));
    }

    [Fact]
    public void CalculatePlannedSets_IsEmpty()
    {
        Stairs().CalculatePlannedSets(1, 1).Should().BeEmpty();
    }

    [Fact]
    public void CalculateCardioBlock_DescribesTodaysSession()
    {
        var block = Stairs().CalculateCardioBlock();

        block.Text.Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
        block.TotalMinutes.Should().Be(11);
    }

    [Fact]
    public void ApplyPerformanceResult_WithoutCardioResult_ChangesNothing()
    {
        var strategy = Stairs();
        var before = strategy.CaptureState();
        var sets = new ExercisePerformance(
            ExerciseId, [new PlannedSet(1, TestWeight, 5)], [new CompletedSet(1, TestWeight, 5)], FixedDate);

        strategy.ApplyPerformanceResult(sets);

        strategy.State.Should().BeSameAs(before);
    }

    [Fact]
    public void ApplyPerformanceResult_WhileSettingsPending_ChangesNothing()
    {
        var strategy = CardioStageStrategy.Create(CardioLanes.InclineWalk());
        var before = strategy.CaptureState();

        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));

        strategy.IsCardioSettingsPending.Should().BeTrue();
        strategy.State.Should().BeSameAs(before);
    }

    [Fact]
    public void ApplyPerformanceResult_Easy_StepsUpAndResetsCounters()
    {
        var strategy = Stairs();
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Solid)));

        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));

        strategy.State.Minutes.Should().Be(7);
        strategy.State.SolidsBanked.Should().Be(0);
        strategy.State.MissStreak.Should().Be(0);
    }

    [Fact]
    public void ApplyPerformanceResult_Hold_StoresTheNewCounters()
    {
        var strategy = Stairs();
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Solid)));

        strategy.ApplyPerformanceResult(Performance(new CardioResult(false, EffortRating.Solid)));

        strategy.State.Minutes.Should().Be(6);
        strategy.State.SolidsBanked.Should().Be(1);
        strategy.State.MissStreak.Should().Be(1);
    }

    [Fact]
    public void ApplyPerformanceResult_AtTheVeryTop_StaysAndClearsCounters()
    {
        var strategy = Stairs();
        strategy.RestoreFromState(strategy.State with { StageIndex = 2, Minutes = null, FormatIndex = 0, Intensity = 20m, SolidsBanked = 1 });

        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Solid)));

        strategy.State.Intensity.Should().Be(20m);
        strategy.State.SolidsBanked.Should().Be(0);
    }

    [Fact]
    public void SetSettings_ReturnsTheTextBeforeAndAfter()
    {
        var strategy = CardioStageStrategy.Create(CardioLanes.InclineWalk());

        var change = strategy.SetSettings(5m, 5.5m);

        change.Should().Be(new CardioSettingsChange("10 min @ ? % incline", "10 min @ 5 % incline, 5.5 km/h"));
        strategy.IsCardioSettingsPending.Should().BeFalse();
        strategy.State.ChosenIntensities[0].Should().Be(5m);
    }

    [Fact]
    public void SetSettings_NullLeavesAValueAsItIs()
    {
        var strategy = WalkWithSettings();

        strategy.SetSettings(null, 6m);

        strategy.State.Intensity.Should().Be(5m);
        strategy.State.Speed.Should().Be(6m);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    public void SetSettings_NotPositive_Throws(int? intensity, int? speed)
    {
        var strategy = WalkWithSettings();

        var act = () => strategy.SetSettings(intensity, speed);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SnapshotRoundTrip_RestoresExactState()
    {
        var strategy = Stairs();
        for (var i = 0; i < 7; i++)
        {
            strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));
        }

        strategy.SetSettings(8m, null);
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Solid)));
        var expected = strategy.State;
        var snapshot = ((ExerciseProgression)strategy).CaptureSnapshot(ExerciseId, "Stair Machine (Floors)");

        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));
        strategy.SetSettings(10m, null);
        ((ExerciseProgression)strategy).RestoreFromSnapshot(snapshot);

        snapshot.ProgressionType.Should().Be("CardioStage");
        strategy.State.Should().BeEquivalentTo(expected);
        strategy.State.History.Should().HaveCount(7);
        strategy.State.ChosenIntensities[1].Should().Be(8m);
        strategy.State.SolidsBanked.Should().Be(1);
    }

    [Fact]
    public void RestoreFromSnapshot_OfAnotherType_Throws()
    {
        var snapshot = ProgressionSnapshot.FromState(ExerciseId, "Bench", new LinearProgressionState(100m, 1, true, 4));

        var act = () => ((ExerciseProgression)Stairs()).RestoreFromSnapshot(snapshot);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Description_HardLane_Easy_NamesTheNextBlock()
    {
        Stairs().GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Easy)))
            .Should().Be("Step up: 3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void Description_HardLane_FirstSolid_Banks()
    {
        Stairs().GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Solid)))
            .Should().Be("Banked 1 of 2 Solids");
    }

    [Fact]
    public void Description_HardLane_SecondSolid_StepsUp()
    {
        var strategy = Stairs();
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Solid)));

        strategy.GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Solid)))
            .Should().Be("Step up: 3 min easy (L3) → 7 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void Description_HardLane_Brutal_SameAgain()
    {
        Stairs().GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Brutal)))
            .Should().Be("Same again");
    }

    [Fact]
    public void Description_FirstMiss_WarnsOfAStepBack()
    {
        Stairs().GetProgressionChangeDescription(Performance(new CardioResult(false, EffortRating.Solid)))
            .Should().Be("Same again (1 of 2 before a step back)");
    }

    [Fact]
    public void Description_SecondMiss_NamesThePreviousBlock()
    {
        var strategy = Stairs();
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));
        strategy.ApplyPerformanceResult(Performance(new CardioResult(false, EffortRating.Solid)));

        strategy.GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Solid, PainFlag.Stop)))
            .Should().Be("Step back: 3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void Description_EasyLane_SolidAndBrutal()
    {
        var strategy = WalkWithSettings();
        strategy.ApplyPerformanceResult(Performance(new CardioResult(true, EffortRating.Easy)));

        strategy.GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Solid)))
            .Should().Be("Same again");
        strategy.GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Brutal)))
            .Should().Be("Step back: 10 min @ 5 % incline, 5.5 km/h");
    }

    [Fact]
    public void Description_DoesNotChangeTheState()
    {
        var strategy = Stairs();
        var before = strategy.State;

        strategy.GetProgressionChangeDescription(Performance(new CardioResult(true, EffortRating.Easy)));

        strategy.State.Should().BeSameAs(before);
    }

    [Fact]
    public void GetProgressionData_CarriesLaneStageAndPrescription()
    {
        var data = Stairs().GetProgressionData();

        data.CardioLane.Should().Be(CardioLane.Hard);
        data.CardioStageNumber.Should().Be(1);
        data.CardioStageName.Should().Be("Continuous");
        data.CardioPrescription.Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }

    [Fact]
    public void CreateFromConfig_CardioStage_Hard_IsStairClimber()
    {
        var strategy = ExerciseProgression.CreateFromConfig(new ProgressionConfig
        {
            ProgressionType = "CardioStage",
            EquipmentType = EquipmentType.Machine,
            CardioLane = CardioLane.Hard
        });

        strategy.Should().BeOfType<CardioStageStrategy>()
            .Which.Stages.Should().BeEquivalentTo(CardioLanes.StairClimber().Stages);
    }

    [Fact]
    public void CreateFromConfig_CardioStage_Easy_IsInclineWalk()
    {
        var strategy = ExerciseProgression.CreateFromConfig(new ProgressionConfig
        {
            ProgressionType = "CardioStage",
            EquipmentType = EquipmentType.Machine,
            CardioLane = CardioLane.Easy
        });

        strategy.Should().BeOfType<CardioStageStrategy>().Which.Lane.Should().Be(CardioLane.Easy);
        ((CardioStageStrategy)strategy).RequiresSpeed.Should().BeTrue();
    }

    [Fact]
    public void CreateFromConfig_CardioStage_WithoutLane_Throws()
    {
        var act = () => ExerciseProgression.CreateFromConfig(new ProgressionConfig
        {
            ProgressionType = "CardioStage",
            EquipmentType = EquipmentType.Machine
        });

        act.Should().Throw<ArgumentException>().WithMessage("*lane*");
    }

    [Fact]
    public void CreateFromConfig_UnknownType_ListsCardioStage()
    {
        var act = () => ExerciseProgression.CreateFromConfig(new ProgressionConfig
        {
            ProgressionType = "Nope",
            EquipmentType = EquipmentType.Machine
        });

        act.Should().Throw<ArgumentException>().WithMessage("*CardioStage*");
    }
}
