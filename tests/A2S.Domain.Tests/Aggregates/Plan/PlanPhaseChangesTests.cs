using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Plan;

public class PlanPhaseChangesTests
{
    private static readonly DateOnly BuildStarts = new(2027, 1, 4);

    private static readonly IReadOnlyList<ScheduledSession> Schedule =
        [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")];

    private static PlanPhase Build(IReadOnlyList<string>? changes = null, IReadOnlyList<PhaseSwap>? swaps = null) =>
        new(PhaseType.Cut, BuildStarts, new DateOnly(2027, 2, 28), null, "Build",
            changes ?? ["Back squat replaces pause squat", "Stair finisher: start intervals"],
            swaps ?? [new PhaseSwap("Pause Squat (Barbell)", "Squat (Barbell)")]);

    [Theory]
    [InlineData(-8, false)]
    [InlineData(-7, true)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void HeadsUp_ShowsTheWeekBeforeAndTheFirstDay(int offset, bool expected)
    {
        var phase = Build();

        PlanPhase.HeadsUpOn([phase], BuildStarts.AddDays(offset)).Should().Be(expected ? phase : null);
    }

    [Fact]
    public void HeadsUp_IgnoresPhasesWithoutChanges()
    {
        var quiet = new PlanPhase(PhaseType.Cut, BuildStarts, BuildStarts.AddDays(30), "note");

        PlanPhase.HeadsUpOn([quiet], BuildStarts).Should().BeNull();
    }

    [Fact]
    public void Changes_AreTrimmedAndBlankLinesDropped()
    {
        var phase = Build(["  +15 s holds  ", " ", ""], []);

        phase.Changes.Should().Equal("+15 s holds");
        phase.Label.Should().Be("Build");
        phase.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Swap_WithoutAReplacement_Throws()
    {
        var act = () => Build(swaps: [new PhaseSwap("Pause Squat", " ")]);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void TrainingPlan_SetPhaseChanges_ReplacesThePhaseInPlace()
    {
        var plan = TrainingPlan.Create(new UserId("user_phase"), "Plan", new DateOnly(2026, 11, 16), Schedule, 95m);
        plan.AddPhase(new PlanPhase(PhaseType.Cut, BuildStarts, new DateOnly(2027, 2, 28), "Cut"));

        var updated = plan.SetPhaseChanges(BuildStarts, "Build", ["Home routines: +15 s holds"], []);

        plan.Phases.Should().ContainSingle().Which.Should().BeSameAs(updated);
        updated.Note.Should().Be("Cut");
        updated.Changes.Should().Equal("Home routines: +15 s holds");
    }

    [Fact]
    public void TrainingPlan_SetPhaseChanges_UnknownPhase_Throws()
    {
        var plan = TrainingPlan.Create(new UserId("user_phase"), "Plan", new DateOnly(2026, 11, 16), Schedule, 95m);

        var act = () => plan.SetPhaseChanges(BuildStarts, null, [], []);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
