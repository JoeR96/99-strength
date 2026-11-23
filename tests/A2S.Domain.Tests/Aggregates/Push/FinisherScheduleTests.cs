using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using FluentAssertions;

namespace A2S.Domain.Tests.Aggregates.Push;

public class FinisherScheduleTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Start = new(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);

    private static FinisherNudge At(int seconds, string body) => new(Start.AddSeconds(seconds), body);

    private static FinisherSchedule Schedule(params FinisherNudge[] nudges) =>
        FinisherSchedule.Create(Joe, "gym-abc_1", 2, null, nudges, Start);

    [Fact]
    public void Create_SortsNudges_AndPointsAtTheGymDay()
    {
        var schedule = Schedule(At(240, "Easy: level 3 for 1 min"), At(180, "Hard: level 6 for 1 min"));

        schedule.Nudges.Select(n => n.Body).Should().Equal("Hard: level 6 for 1 min", "Easy: level 3 for 1 min");
        schedule.NextDueAtUtc.Should().Be(Start.AddSeconds(180));
        schedule.Title.Should().Be("Stair finisher");
        schedule.Url.Should().Be("/gym/2?finisher=1");
        schedule.Tag.Should().Be("finisher-gym-abc_1");
        schedule.Revision.Should().Be(1);
    }

    [Fact]
    public void Replace_DropsTheOldNudges_AndPastOnes()
    {
        var schedule = Schedule(At(180, "Hard: level 6 for 1 min"));

        schedule.Replace(2, "Stairs", [At(-5, "gone"), At(210, "Hard: level 6 for 1 min")], Start);

        schedule.Nudges.Should().ContainSingle().Which.DueAtUtc.Should().Be(Start.AddSeconds(210));
        schedule.Title.Should().Be("Stairs");
        schedule.Revision.Should().Be(2);
    }

    [Fact]
    public void TakeDue_SendsOnce()
    {
        var schedule = Schedule(At(180, "Hard: level 6 for 1 min"), At(240, "Easy"));

        schedule.TakeDue(Start.AddSeconds(179)).Should().BeNull();
        schedule.TakeDue(Start.AddSeconds(181))!.Body.Should().Be("Hard: level 6 for 1 min");
        schedule.TakeDue(Start.AddSeconds(182)).Should().BeNull();
        schedule.NextDueAtUtc.Should().Be(Start.AddSeconds(240));
        schedule.IsExhausted.Should().BeFalse();
    }

    [Fact]
    public void TakeDue_WithSeveralDue_SendsOnlyTheLatest()
    {
        var schedule = Schedule(At(60, "first"), At(90, "second"));

        schedule.TakeDue(Start.AddSeconds(100))!.Body.Should().Be("second");
        schedule.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void TakeDue_DropsStaleNudges()
    {
        var schedule = Schedule(At(60, "late"));

        schedule.TakeDue(Start.AddMinutes(5)).Should().BeNull();
        schedule.IsExhausted.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad id!")]
    public void Create_RejectsABadSessionId(string id)
    {
        var act = () => FinisherSchedule.Create(Joe, id, 1, null, [], Start);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Replace_RejectsFarFutureAndEmptyText()
    {
        var schedule = Schedule();

        ((Action)(() => schedule.Replace(1, null, [new FinisherNudge(Start.AddHours(7), "x")], Start))).Should().Throw<BusinessRuleViolationException>();
        ((Action)(() => schedule.Replace(1, null, [At(10, " ")], Start))).Should().Throw<BusinessRuleViolationException>();
        ((Action)(() => schedule.Replace(0, null, [], Start))).Should().Throw<BusinessRuleViolationException>();
    }
}
