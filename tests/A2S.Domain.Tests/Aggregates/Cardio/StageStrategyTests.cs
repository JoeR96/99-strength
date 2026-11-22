using A2S.Domain.Aggregates.Cardio;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Cardio;

public class StageStrategyTests
{
    private static readonly SessionCheck Good = new(true, EffortRating.Easy, PainFlag.None, null, null);

    private static TrackStage Minutes(string name, decimal min, decimal max, StageExit exit = StageExit.AtTop) =>
        TrackStage.Double(name, TrackEffortTarget.Easy,
            new DoubleProgressionSettings(VolumeUnit.Minutes, min, max, 5, null, IntensityUnit.None, null, null, null, 1)) with { Exit = exit };

    [Fact]
    public void Start_IsTheFirstStage()
    {
        var strategy = new StageStrategy([Minutes("A", 20, 25), Minutes("B", 30, 30)]);

        var state = strategy.Start();

        state.StageIndex.Should().Be(0);
        strategy.Prescribe(state).Title.Should().Be("20 min continuous");
        strategy.Current(state).Name.Should().Be("A");
    }

    [Fact]
    public void ReachingTheTop_WithANextStage_SuggestsItInsteadOfMoving()
    {
        var strategy = new StageStrategy([Minutes("A", 20, 20), Minutes("B", 30, 30)]);

        var result = strategy.Evaluate(strategy.Start(), Good);

        result.Outcome.Should().Be(TrackOutcome.StageComplete);
        result.State.Suggestion.Should().Be(TrackSuggestion.NextStage);
        result.State.StageIndex.Should().Be(0);
        result.Reason.Should().Contain("B");
    }

    [Fact]
    public void MoveToNext_StartsTheNextStage_AndCountsAStep()
    {
        var strategy = new StageStrategy([Minutes("A", 20, 20), Minutes("B", 30, 30)]);
        var waiting = strategy.Evaluate(strategy.Start(), Good).State;

        var next = strategy.MoveToNext(waiting);

        next.StageIndex.Should().Be(1);
        next.Suggestion.Should().Be(TrackSuggestion.None);
        next.Step.Should().Be(waiting.Step + 1);
        strategy.Prescribe(next).Title.Should().Be("30 min continuous");
    }

    [Fact]
    public void ExitNever_HoldsAtTheTop_WithoutSuggesting()
    {
        var strategy = new StageStrategy([Minutes("A", 20, 20, StageExit.Never), Minutes("B", 30, 30)]);

        var result = strategy.Evaluate(strategy.Start(), Good);

        result.Outcome.Should().Be(TrackOutcome.AtTop);
        result.State.Suggestion.Should().Be(TrackSuggestion.None);
    }

    [Fact]
    public void LastStage_TopReached_HasNoNextStage()
    {
        var strategy = new StageStrategy([Minutes("Only", 20, 20)]);

        var result = strategy.Evaluate(strategy.Start(), Good);

        result.State.Suggestion.Should().Be(TrackSuggestion.None);
        strategy.HasNext(result.State).Should().BeFalse();
        var act = () => strategy.MoveToNext(result.State);
        act.Should().Throw<A2S.Domain.Common.BusinessRuleViolationException>();
    }
}

public class WeekProgramPlannedSessionsTests
{
    private static readonly WeekProgramStrategy Strategy = new();
    private static readonly SessionCheck Good = new(true, EffortRating.Solid, PainFlag.None, null, null);

    private static TrackStage Program(int sessionsToAdvance) => TrackStage.Weeks("C25K",
        new WeekProgramSettings([new ProgramWeek(1, "wk 1", 30, "a"), new ProgramWeek(2, "wk 2", 30, "b")], sessionsToAdvance, 2));

    [Fact]
    public void PlannedSessionsFromTheSchedule_SetTheGate_WhenMoreThanTheMinimum()
    {
        var stage = Program(sessionsToAdvance: 1);
        var context = new WeekContext(PlannedSessionsThisWeek: 3, MissedPlannedSessions: 0);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good, context).State;
        state = Strategy.Evaluate(stage, state, Good, context).State;
        state.Week.Should().Be(1);
        Strategy.SessionsToStepUp(stage, state, context).Should().Be(1);

        Strategy.Evaluate(stage, state, Good, context).State.Week.Should().Be(2);
    }

    [Fact]
    public void WalkToRun_TwoPlannedAWeek_StillNeedsThreeQualifyingSessions()
    {
        var stage = Program(sessionsToAdvance: 3);
        var context = new WeekContext(2, 0);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good, context).State;
        state = Strategy.Evaluate(stage, state, Good, context).State;
        state.Week.Should().Be(1);

        Strategy.Evaluate(stage, state, Good, context).State.Week.Should().Be(2);
    }

    [Fact]
    public void AMissedPlannedSession_BlocksAdvance_AndSuggestsRepeat()
    {
        var stage = Program(sessionsToAdvance: 2);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good, new WeekContext(2, 0)).State;
        var result = Strategy.Evaluate(stage, state, Good, new WeekContext(2, 1));

        result.Outcome.Should().Be(TrackOutcome.SuggestRepeat);
        result.Reason.Should().Contain("missed");
    }

    [Fact]
    public void WithoutAPlan_FallsBackToSessionsToAdvance()
    {
        var stage = Program(sessionsToAdvance: 2);
        var state = Strategy.Start(stage, 0);

        state = Strategy.Evaluate(stage, state, Good).State;
        Strategy.Evaluate(stage, state, Good).State.Week.Should().Be(2);
    }
}
