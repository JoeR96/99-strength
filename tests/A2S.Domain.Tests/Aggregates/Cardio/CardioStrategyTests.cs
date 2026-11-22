using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Cardio;

public class DoubleProgressionStrategyTests
{
    private static readonly DoubleProgressionStrategy Strategy = new();

    private static TrackStage EasyMinutes(int sessionsToAdvance = 2) => TrackStage.Double(
        "Duration",
        TrackEffortTarget.Easy,
        new DoubleProgressionSettings(VolumeUnit.Minutes, 20, 30, 5, null, IntensityUnit.None, null, null, null, sessionsToAdvance),
        hrCeilingZone: 2);

    private static TrackStage HardLevel() => TrackStage.Double(
        "Continuous",
        TrackEffortTarget.Hard,
        new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 6, 1, null, IntensityUnit.Level, 8, 1, 9, 2),
        zoneTargetFraction: 0.5m);

    private static SessionCheck Good() => new(true, EffortRating.Easy, PainFlag.None, null, null);

    [Fact]
    public void Start_PutsVolumeAtBottomAndIntensityAtStart()
    {
        var state = Strategy.Start(HardLevel(), 0);

        state.Volume.Should().Be(5);
        state.Intensity.Should().Be(8);
        state.Qualifying.Should().Be(0);
    }

    private static readonly SessionCheck HardGood = new(true, EffortRating.Brutal, PainFlag.None, true, null);

    [Fact]
    public void AtTheTop_WithoutResetTo_VolumeGoesBackToTheRangeMinimum()
    {
        var stage = HardLevel();
        var top = Strategy.Start(stage, 0) with { Volume = 6, Qualifying = 1 };

        var next = Strategy.Evaluate(stage, top, HardGood);

        next.Outcome.Should().Be(TrackOutcome.SteppedUp);
        next.State.Intensity.Should().Be(9);
        next.State.Volume.Should().Be(5);
    }

    [Fact]
    public void AtTheTop_WithResetTo_VolumeGoesBackToTheResetValue()
    {
        var stage = TrackStage.Double("Continuous", TrackEffortTarget.Hard,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 15, 1, null, IntensityUnit.Level, 6, 1, 9, 2, volumeResetTo: 10),
            zoneTargetFraction: 0.5m);
        var top = Strategy.Start(stage, 0) with { Volume = 15, Qualifying = 1 };

        var next = Strategy.Evaluate(stage, top, HardGood);

        next.State.Intensity.Should().Be(7);
        next.State.Volume.Should().Be(10);
        Strategy.Start(stage, 0).Volume.Should().Be(5, "a fresh stage still starts at the bottom");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public void ResetTo_OutsideTheRange_Throws(int reset)
    {
        var act = () => new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 15, 1, null, IntensityUnit.Level, 6, 1, 9, 2, volumeResetTo: reset);

        act.Should().Throw<A2S.Domain.Common.BusinessRuleViolationException>();
    }

    [Fact]
    public void TwoQualifyingSessions_StepVolumeUp()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0);

        var first = Strategy.Evaluate(stage, state, Good());
        first.State.Volume.Should().Be(20);
        first.State.Qualifying.Should().Be(1);
        first.Outcome.Should().Be(TrackOutcome.Qualified);

        var second = Strategy.Evaluate(stage, first.State, Good());
        second.State.Volume.Should().Be(25);
        second.State.Qualifying.Should().Be(0);
        second.State.Step.Should().Be(1);
        second.Outcome.Should().Be(TrackOutcome.SteppedUp);
    }

    [Fact]
    public void SessionsToAdvance_IsConfigurable()
    {
        var stage = EasyMinutes(sessionsToAdvance: 3);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good()).State;
        state = Strategy.Evaluate(stage, state, Good()).State;
        state.Volume.Should().Be(20);
        Strategy.Evaluate(stage, state, Good()).State.Volume.Should().Be(25);
    }

    [Fact]
    public void BrutalOnEasyTrack_Holds()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0) with { Qualifying = 1 };

        var result = Strategy.Evaluate(stage, state, Good() with { Effort = EffortRating.Brutal });

        result.Outcome.Should().Be(TrackOutcome.Held);
        result.State.Volume.Should().Be(20);
        result.State.Qualifying.Should().Be(1);
        result.Reason.Should().Contain("Brutal");
    }

    [Fact]
    public void PainFlagOnLastQualifyingSession_Holds()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0) with { Qualifying = 1 };

        var result = Strategy.Evaluate(stage, state, Good() with { Pain = PainFlag.Niggle });

        result.Outcome.Should().Be(TrackOutcome.Held);
        result.State.Volume.Should().Be(20);
        result.Reason.Should().Contain("pain");
    }

    [Fact]
    public void TwoMissesInARow_StepBackOnce()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0) with { Volume = 30 };

        var first = Strategy.Evaluate(stage, state, Good() with { Completed = false });
        first.Outcome.Should().Be(TrackOutcome.Held);
        first.State.ConsecutiveMisses.Should().Be(1);

        var second = Strategy.Evaluate(stage, first.State, Good() with { Completed = false });
        second.Outcome.Should().Be(TrackOutcome.SteppedBack);
        second.State.Volume.Should().Be(25);
        second.State.ConsecutiveMisses.Should().Be(0);
    }

    [Fact]
    public void MissThenQualify_ResetsMissCount()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good() with { Completed = false }).State;
        state = Strategy.Evaluate(stage, state, Good()).State;

        state.ConsecutiveMisses.Should().Be(0);
    }

    [Fact]
    public void AtTopOfVolume_StepsIntensityAndResetsVolume()
    {
        var stage = HardLevel();
        var state = Strategy.Start(stage, 0) with { Volume = 6, Qualifying = 1 };

        var result = Strategy.Evaluate(stage, state, new SessionCheck(true, EffortRating.Brutal, PainFlag.None, true, null));

        result.State.Volume.Should().Be(5);
        result.State.Intensity.Should().Be(9);
        result.Outcome.Should().Be(TrackOutcome.SteppedUp);
    }

    [Fact]
    public void AtTopOfVolumeAndIntensity_CompletesStage()
    {
        var stage = HardLevel();
        var state = Strategy.Start(stage, 0) with { Volume = 6, Intensity = 9, Qualifying = 1 };

        var result = Strategy.Evaluate(stage, state, new SessionCheck(true, EffortRating.Solid, PainFlag.None, true, null));

        result.Outcome.Should().Be(TrackOutcome.StageComplete);
        result.State.StageComplete.Should().BeTrue();
        result.State.Volume.Should().Be(6);
    }

    [Fact]
    public void HardTrack_ZoneTargetMissed_Holds()
    {
        var stage = HardLevel();
        var state = Strategy.Start(stage, 0);

        var result = Strategy.Evaluate(stage, state, new SessionCheck(true, EffortRating.Brutal, PainFlag.None, false, null));

        result.Outcome.Should().Be(TrackOutcome.Held);
        result.Reason.Should().Contain("zone 4–5");
    }

    [Fact]
    public void HardTrack_NoHeartRate_FallsBackToEffort()
    {
        var stage = HardLevel();
        var state = Strategy.Start(stage, 0);

        Strategy.Evaluate(stage, state, new SessionCheck(true, EffortRating.Solid, PainFlag.None, null, null))
            .Outcome.Should().Be(TrackOutcome.Qualified);
        Strategy.Evaluate(stage, state, new SessionCheck(true, EffortRating.Easy, PainFlag.None, null, null))
            .Outcome.Should().Be(TrackOutcome.Held);
    }

    [Fact]
    public void EasyTrack_HeartRateAboveCeiling_Holds()
    {
        var stage = EasyMinutes();
        var state = Strategy.Start(stage, 0);

        var result = Strategy.Evaluate(stage, state, Good() with { HeartRateWithinCeiling = false });

        result.Outcome.Should().Be(TrackOutcome.Held);
        result.Reason.Should().Contain("zone 2");
    }

    [Fact]
    public void PercentStep_RoundsUpToWholeMinutesAndCapsAtMax()
    {
        var stage = TrackStage.Double("5K builder", TrackEffortTarget.Easy,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 30, 35, 1, 10, IntensityUnit.None, null, null, null, 2));
        var state = Strategy.Start(stage, 0) with { Qualifying = 1 };

        var first = Strategy.Evaluate(stage, state, Good());
        first.State.Volume.Should().Be(33);

        var second = Strategy.Evaluate(stage, first.State with { Qualifying = 1 }, Good());
        second.State.Volume.Should().Be(35);
    }

    [Fact]
    public void Prescribe_DescribesContinuousAndIntervals()
    {
        var continuous = HardLevel();
        Strategy.Prescribe(continuous, Strategy.Start(continuous, 0) with { Volume = 7, Intensity = 9 })
            .Title.Should().Be("7 min continuous @ level 9");

        var intervals = TrackStage.Double("Intervals", TrackEffortTarget.Hard,
            new DoubleProgressionSettings(VolumeUnit.Rounds, 4, 8, 1, null, IntensityUnit.Level, 10, 1, null, 2, 60, 60),
            zoneTargetFraction: 0.5m);
        var prescription = Strategy.Prescribe(intervals, Strategy.Start(intervals, 0));
        prescription.Title.Should().Be("4 × 1 min hard / 1 min easy @ level 10");
        prescription.Minutes.Should().Be(8);
        prescription.WorkMinutes.Should().Be(4);

        var incline = TrackStage.Double("Incline", TrackEffortTarget.Easy,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 45, 45, 5, null, IntensityUnit.InclinePercent, 1, 1, 10, 2));
        Strategy.Prescribe(incline, Strategy.Start(incline, 0)).Title.Should().Be("45 min continuous @ 1% incline");
    }
}

public class WeekProgramStrategyTests
{
    private static readonly WeekProgramStrategy Strategy = new();

    private static TrackStage Program(int sessionsToAdvance = 3) => TrackStage.Weeks(
        "C25K",
        new WeekProgramSettings(
            [
                new ProgramWeek(1, "C25K wk 1", 30, "Run 1 min, walk 1½ min ×8"),
                new ProgramWeek(2, "C25K wk 2", 31, "Run 1½ min, walk 2 min ×6"),
                new ProgramWeek(3, "C25K wk 3", 28, "Run 1½/3 min, walk ×2")
            ],
            sessionsToAdvance,
            sessionsPerWeek: 2));

    private static SessionCheck Good() => new(true, EffortRating.Solid, PainFlag.None, null, null);

    [Fact]
    public void AllPlannedSessionsGood_AdvancesWeek()
    {
        var stage = Program();
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good()).State;
        state = Strategy.Evaluate(stage, state, Good()).State;
        var third = Strategy.Evaluate(stage, state, Good());

        third.Outcome.Should().Be(TrackOutcome.SteppedUp);
        third.State.Week.Should().Be(2);
        third.State.WeekSessions.Should().Be(0);
    }

    [Fact]
    public void BrutalSessionInWeek_SuggestsRepeat()
    {
        var stage = Program(sessionsToAdvance: 2);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good() with { Effort = EffortRating.Brutal }).State;
        var second = Strategy.Evaluate(stage, state, Good());

        second.Outcome.Should().Be(TrackOutcome.SuggestRepeat);
        second.State.Suggestion.Should().Be(TrackSuggestion.RepeatWeek);
        second.State.Week.Should().Be(1);
    }

    [Fact]
    public void TwoNiggles_SuggestDropBack()
    {
        var stage = Program();
        var state = Strategy.Start(stage, 0) with { Week = 3 };

        state = Strategy.Evaluate(stage, state, Good() with { Pain = PainFlag.Niggle }).State;
        state.Suggestion.Should().Be(TrackSuggestion.None);
        var second = Strategy.Evaluate(stage, state, Good() with { Pain = PainFlag.Niggle });

        second.Outcome.Should().Be(TrackOutcome.SuggestDropBack);
        second.State.Suggestion.Should().Be(TrackSuggestion.DropBackWeek);
    }

    [Fact]
    public void AnyStop_SuggestsDropBackImmediately()
    {
        var stage = Program();
        var state = Strategy.Start(stage, 0) with { Week = 2 };

        var result = Strategy.Evaluate(stage, state, Good() with { Pain = PainFlag.Stop });

        result.State.Suggestion.Should().Be(TrackSuggestion.DropBackWeek);
    }

    [Fact]
    public void LastWeekPassed_CompletesStage()
    {
        var stage = Program(sessionsToAdvance: 1);
        var state = Strategy.Start(stage, 0) with { Week = 3 };

        var result = Strategy.Evaluate(stage, state, Good());

        result.Outcome.Should().Be(TrackOutcome.StageComplete);
        result.State.Week.Should().Be(3);
        result.State.StageComplete.Should().BeTrue();
    }

    [Fact]
    public void Prescribe_NamesWeekAndDay()
    {
        var stage = Program();
        var prescription = Strategy.Prescribe(stage, Strategy.Start(stage, 0) with { Week = 2, WeekSessions = 1 });

        prescription.Title.Should().Be("C25K wk 2 day 2");
        prescription.Minutes.Should().Be(31);
        prescription.Week.Should().Be(2);
    }
}

public class SessionCheckTests
{
    private static readonly HeartRateZones Zones = HeartRateZones.Whoop(200);

    [Fact]
    public void From_EasyStage_ComparesAverageHeartRateWithZoneCeiling()
    {
        var stage = TrackStage.Double("Walk", TrackEffortTarget.Easy,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 20, 45, 5, null, IntensityUnit.None, null, null, null, 2),
            hrCeilingZone: 2);

        SessionCheck.From(stage, Log(avgHr: 140), completed: true, Zones, workMinutes: 20).HeartRateWithinCeiling.Should().BeTrue();
        SessionCheck.From(stage, Log(avgHr: 141), completed: true, Zones, workMinutes: 20).HeartRateWithinCeiling.Should().BeFalse();
        SessionCheck.From(stage, Log(avgHr: null), completed: true, Zones, workMinutes: 20).HeartRateWithinCeiling.Should().BeNull();
        SessionCheck.From(stage, Log(avgHr: 150), completed: true, null, workMinutes: 20).HeartRateWithinCeiling.Should().BeNull();
    }

    [Fact]
    public void From_HardStage_ComparesZoneMinutesWithHalfTheWorkTime()
    {
        var stage = TrackStage.Double("Stair", TrackEffortTarget.Hard,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 10, 1, null, IntensityUnit.Level, 6, 1, 9, 2),
            zoneTargetFraction: 0.5m);

        SessionCheck.From(stage, Log(zone45: 4), completed: true, Zones, workMinutes: 8).ZoneTargetMet.Should().BeTrue();
        SessionCheck.From(stage, Log(zone45: 3), completed: true, Zones, workMinutes: 8).ZoneTargetMet.Should().BeFalse();
        SessionCheck.From(stage, Log(zone45: null), completed: true, Zones, workMinutes: 8).ZoneTargetMet.Should().BeNull();
    }

    [Fact]
    public void From_UsesEffortThenRpe()
    {
        var stage = TrackStage.Double("Walk", TrackEffortTarget.Easy,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 20, 45, 5, null, IntensityUnit.None, null, null, null, 2));

        SessionCheck.From(stage, Log(rpe: 9), true, null, 20).Effort.Should().Be(EffortRating.Brutal);
        SessionCheck.From(stage, Log(rpe: 6), true, null, 20).Effort.Should().Be(EffortRating.Solid);
        SessionCheck.From(stage, Log(rpe: null), true, null, 20).Effort.Should().Be(EffortRating.Solid);
    }

    private static CardioLog Log(int? avgHr = null, int? zone45 = null, int? rpe = null) =>
        new(1200, avgHr, rpe, null, null, zone45);
}
