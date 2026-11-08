using A2S.Application.Push;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Services;
using FluentAssertions;

namespace A2S.Application.Tests.Push;

public class PushNudgePlannerTests
{
    private static readonly TimeOnly Start = new(17, 30);

    [Theory]
    [InlineData(17, 29, false)]
    [InlineData(17, 30, true)]
    [InlineData(18, 29, true)]
    [InlineData(18, 30, false)]
    public void IsDue_IsTheHourFromTheScheduledTime(int hour, int minute, bool due)
    {
        PushNudgePlanner.IsDue(new TimeOnly(hour, minute), Start).Should().Be(due);
    }

    [Fact]
    public void SessionNudge_NamesEveryPlannedSession_AndSkipsDoneAndRest()
    {
        var sessions = new List<PlannedSession>
        {
            new("Upper A", SessionKind.Lift, SessionStatus.Planned),
            new("Mobility A", SessionKind.Mobility, SessionStatus.Planned),
            new("Norwegian 4×4", SessionKind.Cardio, SessionStatus.Done),
            new("Rest", SessionKind.Rest, SessionStatus.Rest),
        };

        var nudge = PushNudgePlanner.SessionNudge(new TimeOnly(17, 35), Start, sessions);

        nudge.Should().NotBeNull();
        nudge!.Kind.Should().Be(PushNudgeKind.SessionStart);
        nudge.Title.Should().Be("Time to train — Upper A + Mobility A");
        nudge.Body.Should().Be("Planned for 17:30. Open 99 Strength to start.");
        nudge.Url.Should().Be("/today");
        nudge.ToMessage().Tag.Should().Be("SessionStart");
    }

    [Fact]
    public void SessionNudge_IsNull_WhenNotDue_OrNothingPlanned_OrAllDone()
    {
        var planned = new List<PlannedSession> { new("Upper A", SessionKind.Lift, SessionStatus.Planned) };
        var done = new List<PlannedSession> { new("Upper A", SessionKind.Lift, SessionStatus.Done) };

        PushNudgePlanner.SessionNudge(new TimeOnly(16, 0), Start, planned).Should().BeNull();
        PushNudgePlanner.SessionNudge(new TimeOnly(17, 30), Start, new List<PlannedSession>()).Should().BeNull();
        PushNudgePlanner.SessionNudge(new TimeOnly(17, 30), Start, done).Should().BeNull();
    }

    [Fact]
    public void HabitsNudge_ListsOnlyHabitsUnderMinimum_At2000()
    {
        var habits = new List<HabitStanding>
        {
            new("Water", 2, 4),
            new("Walks", 0, 1),
            new("Creatine", 1, 1),
            new("Morning weigh-in", 94.6m, 1),
        };

        var nudge = PushNudgePlanner.HabitsNudge(new TimeOnly(20, 0), habits);

        nudge.Should().NotBeNull();
        nudge!.Kind.Should().Be(PushNudgeKind.HabitsUnderMinimum);
        nudge.Title.Should().Be("Still under minimum");
        nudge.Body.Should().Be("Water 2/4, Walks 0/1");
    }

    [Fact]
    public void HabitsNudge_FormatsDecimals_AndIsNullWhenAllMet_OrBefore2000()
    {
        var short1 = new List<HabitStanding> { new("Protein", 1.5m, 2.25m) };
        var allMet = new List<HabitStanding> { new("Water", 4, 4) };

        PushNudgePlanner.HabitsNudge(new TimeOnly(20, 59), short1)!.Body.Should().Be("Protein 1.5/2.25");
        PushNudgePlanner.HabitsNudge(new TimeOnly(20, 0), allMet).Should().BeNull();
        PushNudgePlanner.HabitsNudge(new TimeOnly(19, 59), short1).Should().BeNull();
    }
}
