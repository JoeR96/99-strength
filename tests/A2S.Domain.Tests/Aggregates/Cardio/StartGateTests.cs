using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Cardio;

public class StartGateTests
{
    private static readonly UserId Owner = new("user_gate_0001");
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTimeOffset At = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static CardioLog Walk(int minutes, decimal incline, EffortRating effort, PainFlag pain = PainFlag.None, string? area = null) =>
        new(minutes * 60, null, null, null, null, effort: effort, intensityUnit: IntensityUnit.InclinePercent,
            intensityValue: incline, pain: pain, painArea: area);

    private static CardioTrack WalkBuilderWith(params (DateOnly Date, CardioLog Log)[] sessions)
    {
        var track = CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Owner, Start);
        var i = 0;
        foreach (var (date, log) in sessions)
        {
            track.LogSession($"s{i++}", date, log, completedAsPrescribed: true, zones: null, At);
        }

        return track;
    }

    private static GateInputs Inputs(decimal? trendKg = 89.5m, IReadOnlyList<CardioTrack>? tracks = null, IReadOnlyList<PainReport>? pain = null) =>
        new(Today, trendKg, tracks ?? [], pain ?? []);

    [Fact]
    public void TrendWeight_AtOrBelowTarget_IsMet()
    {
        var spec = StartGate.SpecFor(GateCondition.TrendWeightAtOrBelow(90m));

        spec.Evaluate(Inputs(90m)).Met.Should().BeTrue();
        spec.Evaluate(Inputs(89.4m)).Progress.Should().Be("Trend 89.4 kg");
    }

    [Fact]
    public void TrendWeight_AboveTarget_ShowsKgToGo()
    {
        var result = StartGate.SpecFor(GateCondition.TrendWeightAtOrBelow(90m)).Evaluate(Inputs(91.3m));

        result.Met.Should().BeFalse();
        result.Label.Should().Be("Trend weight ≤ 90 kg");
        result.Progress.Should().Be("Trend 91.3 kg, 1.3 kg to go");
    }

    [Fact]
    public void TrendWeight_WithNoWeighIns_IsNotMet()
    {
        StartGate.SpecFor(GateCondition.TrendWeightAtOrBelow(90m)).Evaluate(Inputs(null)).Met.Should().BeFalse();
    }

    [Fact]
    public void TrackAtOrAbove_TwoConsecutiveWeeksOfQualifyingSessions_IsMet()
    {
        var walk = WalkBuilderWith(
            (Today.AddDays(-9), Walk(45, 8, EffortRating.Easy)),
            (Today.AddDays(-2), Walk(46, 9, EffortRating.Easy)));
        var condition = GateCondition.TrackAtOrAbove(CardioTrackTemplates.WalkBuilder, 45, 8m, EffortRating.Easy, 2);

        var result = StartGate.SpecFor(condition).Evaluate(Inputs(tracks: [walk]));

        result.Met.Should().BeTrue();
        result.Progress.Should().Be("2 of 2 weeks");
        result.Label.Should().Be("Walk builder ≥ 45 min at ≥ 8 % incline, rated Easy, for 2 weeks");
    }

    [Fact]
    public void TrackAtOrAbove_CurrentWeekEmpty_CountsFromTheWeekBefore()
    {
        var walk = WalkBuilderWith(
            (Today.AddDays(-16), Walk(45, 8, EffortRating.Easy)),
            (Today.AddDays(-9), Walk(45, 8, EffortRating.Easy)));
        var condition = GateCondition.TrackAtOrAbove(CardioTrackTemplates.WalkBuilder, 45, 8m, EffortRating.Easy, 2);

        StartGate.SpecFor(condition).Evaluate(Inputs(tracks: [walk])).Met.Should().BeTrue();
    }

    [Theory]
    [InlineData(40, 8, EffortRating.Easy)]
    [InlineData(45, 7, EffortRating.Easy)]
    [InlineData(45, 8, EffortRating.Solid)]
    public void TrackAtOrAbove_ShortFlatOrHarderSessions_DoNotCount(int minutes, int incline, EffortRating effort)
    {
        var walk = WalkBuilderWith(
            (Today.AddDays(-9), Walk(45, 8, EffortRating.Easy)),
            (Today.AddDays(-2), Walk(minutes, incline, effort)));
        var condition = GateCondition.TrackAtOrAbove(CardioTrackTemplates.WalkBuilder, 45, 8m, EffortRating.Easy, 2);

        var result = StartGate.SpecFor(condition).Evaluate(Inputs(tracks: [walk]));

        result.Met.Should().BeFalse();
        result.Progress.Should().Be("1 of 2 weeks");
    }

    [Fact]
    public void TrackAtOrAbove_WithoutTheTrack_AsksToStartIt()
    {
        var condition = GateCondition.TrackAtOrAbove(CardioTrackTemplates.WalkBuilder, 45, 8m, EffortRating.Easy, 2);

        var result = StartGate.SpecFor(condition).Evaluate(Inputs());

        result.Met.Should().BeFalse();
        result.Progress.Should().Be("Start that track first");
    }

    [Fact]
    public void NoPainInAreas_MatchingPainInWindow_IsNotMet()
    {
        var condition = GateCondition.NoPainInAreas(["knee", "shin", "Achilles"], 4);
        PainReport[] pain = [new(Today.AddDays(-10), PainFlag.Niggle, "Left knee")];

        var result = StartGate.SpecFor(condition).Evaluate(Inputs(pain: pain));

        result.Met.Should().BeFalse();
        result.Progress.Should().Be("Left knee pain on 12 Nov; clear from 10 Dec");
    }

    [Fact]
    public void NoPainInAreas_OldOrOtherPain_IsMet()
    {
        var condition = GateCondition.NoPainInAreas(["knee", "shin", "Achilles"], 4);
        PainReport[] pain =
        [
            new(Today.AddDays(-28), PainFlag.Stop, "achilles"),
            new(Today.AddDays(-3), PainFlag.Niggle, "lower back"),
            new(Today.AddDays(-1), PainFlag.None, "knee")
        ];

        var result = StartGate.SpecFor(condition).Evaluate(Inputs(pain: pain));

        result.Met.Should().BeTrue();
        result.Label.Should().Be("No knee/shin/Achilles pain for 4 weeks");
    }

    [Fact]
    public void Evaluate_RunningGate_AllMetOnlyWhenEveryConditionIs()
    {
        var walk = WalkBuilderWith(
            (Today.AddDays(-9), Walk(45, 8, EffortRating.Easy)),
            (Today.AddDays(-2), Walk(45, 8, EffortRating.Easy)));

        StartGate.Evaluate(StartGate.RunningGate(), Inputs(89.9m, [walk])).AllMet.Should().BeTrue();

        var heavy = StartGate.Evaluate(StartGate.RunningGate(), Inputs(90.4m, [walk]));
        heavy.AllMet.Should().BeFalse();
        heavy.MetCount.Should().Be(2);
    }

    [Fact]
    public void Evaluate_WeightAndPainRunningGate_NeedsNoWalkBuilderHistory()
    {
        var gate = StartGate.WeightAndPainRunningGate();

        gate.Select(c => c.Type).Should().Equal(GateConditionType.TrendWeightAtOrBelow, GateConditionType.NoPainInAreas);
        StartGate.Evaluate(gate, Inputs(89.9m, [])).AllMet.Should().BeTrue();
        StartGate.Evaluate(gate, Inputs(90.4m, [])).AllMet.Should().BeFalse();
        CardioTrackTemplates.DefaultGate(CardioTrackTemplates.WalkToRun).Should().HaveCount(3);
    }

    [Fact]
    public void Validate_RejectsIncompleteConditions()
    {
        var noKg = () => GateCondition.Validate(new GateCondition(GateConditionType.TrendWeightAtOrBelow));
        var noTrack = () => GateCondition.TrackAtOrAbove(" ", 45, 8m, EffortRating.Easy, 2);
        var noAreas = () => GateCondition.NoPainInAreas([], 4);

        noKg.Should().Throw<BusinessRuleViolationException>();
        noTrack.Should().Throw<BusinessRuleViolationException>();
        noAreas.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void GatedTrack_RecordsWithoutEvaluating_UntilStarted()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start, withGate: true);

        run.Status.Should().Be(CardioTrackStatus.Gated);
        run.StartGate.Should().HaveCount(3);
        run.CanUndo.Should().BeFalse();

        var result = run.LogSession("early", Today, Walk(30, 0, EffortRating.Easy), true, null, At);
        result.Outcome.Should().Be(TrackOutcome.Paused);
        result.Reason.Should().StartWith("Not started (gate)");

        run.StartFromGate(Today, At);

        run.Status.Should().Be(CardioTrackStatus.Active);
        run.State.WeekStartedOn.Should().Be(Today);
        run.Audit[^1].Action.Should().Be("GateOpened");
    }

    [Fact]
    public void StartFromGate_OnAnUngatedTrack_Throws()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);

        var act = () => run.StartFromGate(Today, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SetStartGate_GatesAnActiveTrack_ClearingStartsIt_AndUndoRestoresTheGate()
    {
        var run = CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Owner, Start);

        run.SetStartGate(StartGate.RunningGate(), At);
        run.Status.Should().Be(CardioTrackStatus.Gated);

        run.SetStartGate([], At);
        run.Status.Should().Be(CardioTrackStatus.Active);
        run.StartGate.Should().BeEmpty();

        run.UndoLastEvaluation();
        run.Status.Should().Be(CardioTrackStatus.Gated);
        run.StartGate.Should().HaveCount(3);
    }
}
