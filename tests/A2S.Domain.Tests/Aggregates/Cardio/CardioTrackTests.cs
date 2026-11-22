using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Cardio;

public class CardioTrackTests
{
    private static readonly UserId Owner = new("user_track_0001");
    private static readonly DateOnly Start = new(2026, 11, 2);
    private static readonly DateTimeOffset At = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    private static CardioLog Easy(int minutes = 20) =>
        new(minutes * 60, null, null, null, null, effort: EffortRating.Easy);

    private static CardioLog Hard(int minutes, int zoneMinutes, PainFlag pain = PainFlag.None) =>
        new(minutes * 60, 165, null, null, null, zoneMinutes, effort: EffortRating.Brutal, pain: pain);

    private static CardioTrack Walk() => CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Owner, Start);

    private static CardioTrack Stair() => CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Owner, Start);

    [Fact]
    public void Templates_ShipWalkBuilderWalkToRunAndStairFinisher()
    {
        CardioTrackTemplates.All.Select(t => t.Key).Should().Equal(
            CardioTrackTemplates.WalkBuilder, CardioTrackTemplates.WalkToRun, CardioTrackTemplates.StairFinisher);

        var walk = Walk();
        walk.Modality.Should().Be(CardioModality.Walk);
        walk.Stages.Should().HaveCount(2);
        walk.Prescription.Title.Should().Be("20 min continuous");
        walk.Stages[1].DoubleProgression!.IntensityMax.Should().Be(10);

        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);
        run.Modality.Should().Be(CardioModality.Run);
        run.Stages[0].WeekProgram!.Weeks.Should().HaveCount(9);
        run.Stages[0].WeekProgram!.SessionsToAdvance.Should().Be(3);
        run.Stages[0].WeekProgram!.SessionsPerWeek.Should().Be(2);
        run.Stages[1].DoubleProgression!.VolumeMax.Should().Be(35);
        run.Notes.Should().ContainEquivalentOf("treadmill or grass");
        run.Prescription.Title.Should().Be("C25K wk 1 day 1");

        var stair = Stair();
        stair.Modality.Should().Be(CardioModality.StairClimber);
        stair.IsHard.Should().BeTrue();
        stair.Prescription.Title.Should().Be("5 min continuous @ level 6");
        stair.Benchmark!.EveryWeeks.Should().Be(4);
    }

    [Fact]
    public void StairFinisher_HasEasyWarmUpAndCoolDown_AroundTheWorkBlock()
    {
        var stair = Stair();

        foreach (var stage in stair.Stages)
        {
            stage.WarmUp.Should().Be(new TrackSegment(3, 3, "Z2"));
            stage.CoolDown.Should().Be(new TrackSegment(3, 3, "Z1–2"));
        }

        var p = stair.Prescription;
        p.WorkMinutes.Should().Be(5);
        p.Minutes.Should().Be(11);
        p.Title.Should().Be("5 min continuous @ level 6");
        p.Text.Should().Be("3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)");
    }

    [Fact]
    public void StairFinisher_Ranges_ContinuousFiveToFifteenResetTen_IntervalsSixToTenResetEight()
    {
        var stair = Stair();
        var continuous = stair.Stages[0].DoubleProgression!;
        var intervals = stair.Stages[1].DoubleProgression!;

        (continuous.VolumeMin, continuous.VolumeMax, continuous.VolumeStep, continuous.VolumeResetTo, continuous.SessionsToAdvance)
            .Should().Be((5m, 15m, 1m, (decimal?)10m, 2));
        (intervals.VolumeMin, intervals.VolumeMax, intervals.VolumeStep, intervals.VolumeResetTo, intervals.SessionsToAdvance)
            .Should().Be((6m, 10m, 1m, (decimal?)8m, 2));
        stair.Stages[0].Exit.Should().Be(StageExit.AtTop);
    }

    [Fact]
    public void StairFinisher_ZoneShare_IsJudgedOnWorkMinutes_NotTheTotal()
    {
        var track = Stair();

        // 11 min total, 5 min work: 3 zone minutes is ≥ 50 % of the work (would be < 50 % of the total).
        var result = track.LogSession("s1", Start, Hard(11, 3), true, null, At);

        result.Qualified.Should().BeTrue();
    }

    [Fact]
    public void TrackWithoutSegments_PrescriptionIsUnchanged()
    {
        var walk = Walk();

        walk.Prescription.WarmUp.Should().BeNull();
        walk.Prescription.Minutes.Should().Be(20);
        walk.Prescription.Text.Should().Be("20 min continuous");
    }

    [Fact]
    public void Segment_ZeroMinutesIsOff_OutOfRangeThrows()
    {
        TrackSegment.OrNone(0, 3, "Z2").Should().BeNull();
        var act = () => new TrackSegment(31, 3, null);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_WithUnknownTemplate_Throws()
    {
        var act = () => CardioTrackTemplates.Create("nope", Owner, Start);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void LogSession_EvaluatesAndAppendsAudit()
    {
        var track = Walk();

        track.LogSession("s1", Start, Easy(), completedAsPrescribed: true, zones: null, At);
        var result = track.LogSession("s2", Start.AddDays(2), Easy(), true, null, At);

        result.Outcome.Should().Be(TrackOutcome.SteppedUp);
        track.Prescription.Title.Should().Be("25 min continuous");
        track.Sessions.Should().HaveCount(2);
        track.Audit.Should().HaveCount(2);
        track.Audit[^1].Before.Should().Be("20 min continuous");
        track.Audit[^1].After.Should().Be("25 min continuous");
        track.Audit[^1].Reason.Should().NotBeNullOrWhiteSpace();
        track.SessionsToStepUp.Should().Be(2);
    }

    [Fact]
    public void LogSession_SameKeyTwice_IsIdempotent()
    {
        var track = Walk();

        track.LogSession("hevy:w1:0", Start, Easy(), true, null, At);
        var again = track.LogSession("hevy:w1:0", Start, Easy(), true, null, At);

        again.AlreadyRecorded.Should().BeTrue();
        track.Sessions.Should().HaveCount(1);
        track.State.Qualifying.Should().Be(1);
    }

    [Fact]
    public void Undo_RestoresExactPreviousState()
    {
        var track = Walk();
        track.LogSession("s1", Start, Easy(), true, null, At);
        var stateBefore = track.State;
        var auditBefore = track.Audit.ToList();

        track.LogSession("s2", Start.AddDays(1), Easy(), true, null, At);
        track.UndoLastEvaluation();

        track.State.Should().Be(stateBefore);
        track.Audit.Should().Equal(auditBefore);
        track.Sessions.Should().HaveCount(1);
        track.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Undo_WithNothingToUndo_Throws()
    {
        var act = () => Walk().UndoLastEvaluation();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void StageTop_SuggestsNextStage_AcceptMovesOn_UndoRestores()
    {
        var track = Stair();
        var day = Start;
        // Level 6→9, 5→15 min (back to 10 after each level): two qualifying sessions per step to the top of stage one.
        for (var i = 0; i < 120 && track.State.Suggestion == TrackSuggestion.None; i++)
        {
            day = day.AddDays(1);
            var minutes = (int)track.State.Volume;
            track.LogSession($"s{i}", day, Hard(minutes + 6, minutes), true, null, At);
        }

        track.State.Suggestion.Should().Be(TrackSuggestion.NextStage);
        track.State.StageIndex.Should().Be(0);
        track.Prescription.Title.Should().Be("15 min continuous @ level 9");
        var beforeAccept = track.State;

        track.AcceptSuggestion(At);

        track.State.StageIndex.Should().Be(1);
        track.Prescription.Title.Should().Be("6 × 1 min hard / 1 min easy @ level 10");
        track.Audit[^1].Action.Should().Be("StageAccepted");

        track.UndoLastEvaluation();
        track.State.Should().Be(beforeAccept);
    }

    [Fact]
    public void PendingSuggestion_FurtherSessionsRecordedButNotEvaluated()
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);
        track.LogSession("s1", Start, Easy(30) with { }, true, null, At);
        track.LogSession("s2", Start.AddDays(2), new CardioLog(1800, null, null, null, null, effort: EffortRating.Brutal), true, null, At);
        track.LogSession("s3", Start.AddDays(4), Easy(30), true, null, At);
        track.State.Suggestion.Should().Be(TrackSuggestion.RepeatWeek);

        var result = track.LogSession("s4", Start.AddDays(6), Easy(30), true, null, At);

        result.Outcome.Should().Be(TrackOutcome.AwaitingDecision);
        track.State.Week.Should().Be(1);
    }

    [Fact]
    public void RepeatWeek_ConfirmRepeats_OverrideAdvances()
    {
        var confirm = RunWithRepeatSuggestion();
        confirm.AcceptSuggestion(At);
        confirm.State.Week.Should().Be(1);
        confirm.State.WeekSessions.Should().Be(0);
        confirm.State.Suggestion.Should().Be(TrackSuggestion.None);

        var overridden = RunWithRepeatSuggestion();
        overridden.DeclineSuggestion(At);
        overridden.State.Week.Should().Be(2);
        overridden.Audit[^1].Action.Should().Be("SuggestionOverridden");
    }

    [Fact]
    public void Paused_RecordsSessionWithoutProgressing()
    {
        var track = Walk();
        track.Pause();

        var result = track.LogSession("s1", Start, Easy(), true, null, At);

        result.Outcome.Should().Be(TrackOutcome.Paused);
        track.State.Qualifying.Should().Be(0);
        track.Sessions.Should().ContainSingle();

        track.Resume();
        track.Status.Should().Be(CardioTrackStatus.Active);
    }

    [Fact]
    public void Benchmark_DueEveryNWeeks_AndTracksPersonalBests()
    {
        var track = Stair();

        track.IsBenchmarkDue(Start.AddDays(27)).Should().BeFalse();
        track.IsBenchmarkDue(Start.AddDays(28)).Should().BeTrue();

        track.RecordBenchmark(Start.AddDays(28), 30, null).IsPersonalBest.Should().BeTrue();
        track.RecordBenchmark(Start.AddDays(56), 28, "tired").IsPersonalBest.Should().BeFalse();
        track.RecordBenchmark(Start.AddDays(84), 33, null).IsPersonalBest.Should().BeTrue();

        track.IsBenchmarkDue(Start.AddDays(90)).Should().BeFalse();
        track.State.Should().Be(Stair().State, "a benchmark never changes progression");
    }

    [Fact]
    public void Benchmark_LowerIsBetter_ForTimes()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);

        run.RecordBenchmark(Start, 34, null).IsPersonalBest.Should().BeTrue();
        run.RecordBenchmark(Start.AddDays(28), 35, null).IsPersonalBest.Should().BeFalse();
        run.RecordBenchmark(Start.AddDays(56), 32, null).IsPersonalBest.Should().BeTrue();
    }

    [Fact]
    public void UpdateStages_KeepsPositionInsideNewRangeAndIsUndoable()
    {
        var track = Walk();
        var stage = track.Stages[0];
        var edited = stage with { DoubleProgression = stage.DoubleProgression! with { VolumeMin = 25, VolumeMax = 40 } };

        track.UpdateStages([edited, track.Stages[1]], At);

        track.State.Volume.Should().Be(25);
        track.Audit[^1].Action.Should().Be("Edited");
        track.UndoLastEvaluation();
        track.Stages[0].Should().Be(stage);
        track.State.Volume.Should().Be(20);
    }

    [Fact]
    public void Rename_RequiresAName()
    {
        var track = Walk();
        track.Rename("  Morning walk ");
        track.Name.Should().Be("Morning walk");

        var act = () => track.Rename(" ");
        act.Should().Throw<BusinessRuleViolationException>();
    }

    private static CardioTrack RunWithRepeatSuggestion()
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);
        track.LogSession("s1", Start, Easy(30), true, null, At);
        track.LogSession("s2", Start.AddDays(2), Easy(30), false, null, At);
        track.LogSession("s3", Start.AddDays(4), Easy(30), true, null, At);
        return track;
    }
}
