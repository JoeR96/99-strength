using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Mobility;

public class DrillProgressionTests
{
    private static readonly UserId Owner = new("user_mobility_levels_01");
    private static readonly DateTimeOffset At = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);

    private static DrillLevel[] Ladder() =>
    [
        new DrillLevel("Level 1", "Reverse tabletop, knees bent", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), "Hips high, squeeze glutes"),
        new DrillLevel("Level 2", "Hands on a bench, straight legs", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), "Body in one line"),
        new DrillLevel("Level 3", "Full reverse plank", 3, DrillDose.Seconds(15), new DoseRange(15, 30, 5), "Chest proud")
    ];

    private static DrillLevel[] EasierStandardHarder() =>
    [
        new DrillLevel("Easier", "Bent knee", 2, DrillDose.Breaths(5), null, "Ribs down"),
        new DrillLevel("Standard", "Straight leg", 2, DrillDose.Breaths(5), null, "Ribs down"),
        new DrillLevel("Harder", "Elevated", 2, DrillDose.Breaths(5), null, "Ribs down")
    ];

    private static MobilityDrill LadderDrill(int level = 0, int amount = 15) =>
        MobilityDrill.WithLevels("Reverse plank ladder", Ladder(), level, SideBias.None, perSide: false)
            .WithProgress(level, DrillDose.Seconds(amount), 0, null);

    [Fact]
    public void WithLevels_TakesSetsDoseAndCuesFromTheStartingLevel()
    {
        var drill = MobilityDrill.WithLevels("Reverse plank ladder", Ladder(), 1, SideBias.Left, perSide: false);

        drill.CurrentLevel.Should().Be(1);
        drill.Sets.Should().Be(3);
        drill.Dose.Should().Be(DrillDose.Seconds(15));
        drill.CurrentLevelDetails!.Name.Should().Be("Level 2");
        drill.EffectiveCues.Should().Be("Body in one line");
        drill.ExtraSetSide.Should().Be(SideBias.Left);
    }

    [Fact]
    public void OneCleanSession_OnlyCounts()
    {
        var result = DrillProgressionRule.Evaluate(LadderDrill(), DrillOutcome.Clean);

        result.Change.Should().BeNull();
        result.Drill.CleanStreak.Should().Be(1);
        result.Drill.Dose.Amount.Should().Be(15);
    }

    [Fact]
    public void TwoCleanSessions_InsideTheRange_AddTheStep()
    {
        var once = DrillProgressionRule.Evaluate(LadderDrill(), DrillOutcome.Clean).Drill;
        var twice = DrillProgressionRule.Evaluate(once, DrillOutcome.Clean);

        twice.Change.Should().Be(DrillChangeKind.DoseIncreased);
        twice.Drill.Dose.Amount.Should().Be(20);
        twice.Drill.CleanStreak.Should().Be(0);
        twice.Reason.Should().Contain("15 s").And.Contain("20 s");
    }

    [Fact]
    public void TwoCleanSessions_AtTheTopOfTheRange_SuggestTheNextLevel()
    {
        var atTop = DrillProgressionRule.Evaluate(LadderDrill(amount: 30), DrillOutcome.Clean).Drill;
        var result = DrillProgressionRule.Evaluate(atTop, DrillOutcome.Clean);

        result.Change.Should().Be(DrillChangeKind.LevelUpSuggested);
        result.Drill.SuggestedLevel.Should().Be(1);
        result.Drill.CurrentLevel.Should().Be(0);
        result.Reason.Should().Contain("Level 2").And.Contain("Hands on a bench");
    }

    [Fact]
    public void TwoCleanSessions_WithoutARange_SuggestTheNextLevel()
    {
        var drill = MobilityDrill.WithLevels("Bridge", EasierStandardHarder(), 1, SideBias.None, perSide: false);
        var once = DrillProgressionRule.Evaluate(drill, DrillOutcome.Clean).Drill;
        var result = DrillProgressionRule.Evaluate(once, DrillOutcome.Clean);

        result.Change.Should().Be(DrillChangeKind.LevelUpSuggested);
        result.Drill.SuggestedLevel.Should().Be(2);
    }

    [Fact]
    public void TopLevelAtTopOfRange_HasNowhereToGo()
    {
        var drill = LadderDrill(level: 2, amount: 30);
        var result = DrillProgressionRule.Evaluate(DrillProgressionRule.Evaluate(drill, DrillOutcome.Clean).Drill, DrillOutcome.Clean);

        result.Change.Should().BeNull();
        result.Drill.SuggestedLevel.Should().BeNull();
    }

    [Theory]
    [InlineData(DrillOutcome.Strained)]
    [InlineData(DrillOutcome.Stopped)]
    public void StrainOrStop_StepsTheDoseBackOnceAndResetsTheCount(DrillOutcome outcome)
    {
        var drill = DrillProgressionRule.Evaluate(LadderDrill(amount: 25), DrillOutcome.Clean).Drill;
        var result = DrillProgressionRule.Evaluate(drill, outcome);

        result.Change.Should().Be(DrillChangeKind.SteppedBack);
        result.Drill.Dose.Amount.Should().Be(20);
        result.Drill.CleanStreak.Should().Be(0);
    }

    [Fact]
    public void StrainAtTheBottomOfTheRange_Holds()
    {
        var result = DrillProgressionRule.Evaluate(LadderDrill(), DrillOutcome.Strained);

        result.Change.Should().Be(DrillChangeKind.Held);
        result.Drill.Dose.Amount.Should().Be(15);
    }

    [Fact]
    public void Strain_WithdrawsAPendingSuggestion()
    {
        var suggested = LadderDrill(amount: 30).WithProgress(0, DrillDose.Seconds(30), 0, 1);
        var result = DrillProgressionRule.Evaluate(suggested, DrillOutcome.Strained);

        result.Drill.SuggestedLevel.Should().BeNull();
    }

    [Fact]
    public void Skipped_ChangesNothing()
    {
        var drill = DrillProgressionRule.Evaluate(LadderDrill(), DrillOutcome.Clean).Drill;
        var result = DrillProgressionRule.Evaluate(drill, DrillOutcome.Skipped);

        result.Change.Should().BeNull();
        result.Drill.Should().Be(drill);
    }

    [Fact]
    public void DrillWithoutLevels_IsNeverProgressed()
    {
        var drill = new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, null, true);
        var result = DrillProgressionRule.Evaluate(DrillProgressionRule.Evaluate(drill, DrillOutcome.Clean).Drill, DrillOutcome.Clean);

        result.Change.Should().BeNull();
        result.Drill.Dose.Amount.Should().Be(5);
    }

    [Fact]
    public void RequiredCleanSessions_IsConfigurable()
    {
        var result = DrillProgressionRule.Evaluate(LadderDrill(), DrillOutcome.Clean, requiredCleanSessions: 1);

        result.Change.Should().Be(DrillChangeKind.DoseIncreased);
    }

    private static MobilityRoutine Routine() =>
        MobilityRoutine.Create(Owner, "Home yoga", 20,
        [
            LadderDrill(),
            new MobilityDrill("Child's pose", 1, DrillDose.Seconds(60), SideBias.None, null, null, false)
        ], MobilityRoutineKind.YogaFlow);

    private static void CleanTwice(MobilityRoutine routine, string drill = "Reverse plank ladder")
    {
        routine.RecordFeedback("s1", [new DrillFeedback(drill, DrillOutcome.Clean)], At);
        routine.RecordFeedback("s2", [new DrillFeedback(drill, DrillOutcome.Clean)], At.AddDays(2));
    }

    [Fact]
    public void RecordFeedback_StepsUpAndAuditsWithAReason()
    {
        var routine = Routine();

        CleanTwice(routine);

        routine.Drills[0].Dose.Amount.Should().Be(20);
        routine.ProgressionLog.Should().ContainSingle(e => e.Kind == DrillChangeKind.DoseIncreased)
            .Which.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RecordFeedback_IsIdempotentOnTheSessionKey()
    {
        var routine = Routine();

        routine.RecordFeedback("s1", [new DrillFeedback("Reverse plank ladder", DrillOutcome.Clean)], At);
        routine.RecordFeedback("s1", [new DrillFeedback("Reverse plank ladder", DrillOutcome.Clean)], At);

        routine.Drills[0].CleanStreak.Should().Be(1);
    }

    [Fact]
    public void RecordFeedback_IgnoresUnknownDrills()
    {
        var routine = Routine();

        var act = () => routine.RecordFeedback("s1", [new DrillFeedback("Gone", DrillOutcome.Clean)], At);

        act.Should().NotThrow();
    }

    [Fact]
    public void AcceptSuggestion_MovesUpAndResetsDoseToTheBottomOfTheLevel()
    {
        var routine = Routine();
        routine.ReplaceDrills([LadderDrill(amount: 30), routine.Drills[1]]);
        CleanTwice(routine);

        routine.RespondToSuggestion("Reverse plank ladder", accept: true, At.AddDays(3));

        var drill = routine.Drills[0];
        drill.CurrentLevel.Should().Be(1);
        drill.Dose.Amount.Should().Be(15);
        drill.SuggestedLevel.Should().BeNull();
        routine.ProgressionLog.Last().Kind.Should().Be(DrillChangeKind.LevelUp);
    }

    [Fact]
    public void DeclineSuggestion_StaysAndClearsIt()
    {
        var routine = Routine();
        routine.ReplaceDrills([LadderDrill(amount: 30), routine.Drills[1]]);
        CleanTwice(routine);

        routine.RespondToSuggestion("Reverse plank ladder", accept: false, At.AddDays(3));

        routine.Drills[0].CurrentLevel.Should().Be(0);
        routine.Drills[0].SuggestedLevel.Should().BeNull();
        routine.ProgressionLog.Last().Kind.Should().Be(DrillChangeKind.SuggestionDeclined);
    }

    [Fact]
    public void RespondToSuggestion_WithoutOne_IsABusinessRuleViolation()
    {
        var act = () => Routine().RespondToSuggestion("Reverse plank ladder", true, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void UndoLastChange_RestoresTheDrillAndMarksTheEntry()
    {
        var routine = Routine();
        CleanTwice(routine);

        routine.UndoLastChange(At.AddDays(3)).Should().BeTrue();

        routine.Drills[0].Dose.Amount.Should().Be(15);
        routine.Drills[0].CleanStreak.Should().Be(1);
        routine.ProgressionLog.Single(e => e.Kind == DrillChangeKind.DoseIncreased).Undone.Should().BeTrue();
        routine.UndoLastChange(At.AddDays(3)).Should().BeFalse();
    }

    [Fact]
    public void ApplyHoldIncrease_AddsSecondsToTimedHoldsAndShiftsTheRange()
    {
        var routine = Routine();

        routine.ApplyHoldIncrease(15, At);

        routine.Drills[0].Dose.Amount.Should().Be(30);
        routine.Drills[0].CurrentLevelDetails!.Range.Should().Be(new DoseRange(30, 45, 5));
        routine.Drills[1].Dose.Amount.Should().Be(75);
        routine.ProgressionLog.Where(e => e.Kind == DrillChangeKind.HoldIncreased).Should().HaveCount(2);
    }

    [Fact]
    public void UndoAfterHoldIncrease_RevertsEveryDrillInThatChange()
    {
        var routine = Routine();
        routine.ApplyHoldIncrease(15, At);

        routine.UndoLastChange(At);

        routine.Drills[0].Dose.Amount.Should().Be(15);
        routine.Drills[1].Dose.Amount.Should().Be(60);
    }

    [Fact]
    public void ApplyHoldIncrease_MustBePositive()
    {
        var act = () => Routine().ApplyHoldIncrease(0, At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void FollowAlong_NeedsAUrlAndNoDrills()
    {
        var routine = MobilityRoutine.Create(Owner, "Hip flow video", 25, [], MobilityRoutineKind.FollowAlong, "https://www.youtube.com/watch?v=abc");

        routine.Kind.Should().Be(MobilityRoutineKind.FollowAlong);
        routine.FollowAlongUrl.Should().Be("https://www.youtube.com/watch?v=abc");
        routine.Drills.Should().BeEmpty();
        routine.PostsToHevyAsYoga.Should().BeTrue();
    }

    [Fact]
    public void FollowAlong_WithoutAUrl_IsRejected()
    {
        var act = () => MobilityRoutine.Create(Owner, "Video", 25, [], MobilityRoutineKind.FollowAlong);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void FollowAlong_WithAnUnsafeUrl_IsRejected()
    {
        var act = () => MobilityRoutine.Create(Owner, "Video", 25, [], MobilityRoutineKind.FollowAlong, "javascript:alert(1)");

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData(MobilityRoutineKind.WarmUp, false)]
    [InlineData(MobilityRoutineKind.Corrective, false)]
    [InlineData(MobilityRoutineKind.Stretch, false)]
    [InlineData(MobilityRoutineKind.YogaFlow, true)]
    public void PostsToHevyAsYoga_FollowsTheKind(MobilityRoutineKind kind, bool yoga)
    {
        var routine = MobilityRoutine.Create(Owner, "R", 10, [new MobilityDrill("A", 1, DrillDose.Reps(5), SideBias.None, null, null, false)], kind);

        routine.PostsToHevyAsYoga.Should().Be(yoga);
    }

    [Fact]
    public void Levels_StartingLevelOutOfRange_IsRejected()
    {
        var act = () => MobilityDrill.WithLevels("X", Ladder(), 3, SideBias.None, false);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void DoseRange_MustBeOrderedWithAPositiveStep()
    {
        ((Action)(() => new DoseRange(30, 15, 5))).Should().Throw<BusinessRuleViolationException>();
        ((Action)(() => new DoseRange(15, 30, 0))).Should().Throw<BusinessRuleViolationException>();
    }
}

public class MobilityRoutineKindDefaultTests
{
    [Theory]
    [InlineData("Mobility A", MobilityRoutineKind.WarmUp)]
    [InlineData("Gym warm-up", MobilityRoutineKind.WarmUp)]
    [InlineData("Evening yoga", MobilityRoutineKind.YogaFlow)]
    [InlineData("Hamstring stretch", MobilityRoutineKind.Stretch)]
    public void DefaultKindFor_MirrorsTheMigrationBackfill(string name, MobilityRoutineKind expected) =>
        MobilityRoutine.DefaultKindFor(name).Should().Be(expected);
}
