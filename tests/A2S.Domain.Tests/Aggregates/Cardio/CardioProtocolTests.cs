using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Cardio;

public class CardioProtocolTests
{
    private static readonly UserId Owner = new("user_cardio_0001");

    [Fact]
    public void NorwegianFourByFour_HasNineIntervalsTotalling40Minutes()
    {
        var protocol = CardioProtocol.NorwegianFourByFour(Owner);

        protocol.Kind.Should().Be(CardioProtocolKind.NorwegianFourByFour);
        protocol.UserId.Should().Be(Owner);
        protocol.Intervals.Should().HaveCount(9);
        protocol.Intervals[0].Should().Be(new CardioInterval("Warm-up", 600, CardioEffort.WarmUp));
        protocol.Intervals[1].Should().Be(new CardioInterval("Work 1", 240, CardioEffort.Work));
        protocol.Intervals[2].Should().Be(new CardioInterval("Recovery 1", 180, CardioEffort.Recovery));
        protocol.Intervals[6].Should().Be(new CardioInterval("Recovery 3", 180, CardioEffort.Recovery));
        protocol.Intervals[7].Should().Be(new CardioInterval("Work 4", 240, CardioEffort.Work));
        protocol.Intervals[8].Should().Be(new CardioInterval("Cool-down", 300, CardioEffort.CoolDown));
        protocol.Intervals.Count(i => i.Effort == CardioEffort.Recovery).Should().Be(3, "recovery sits between work intervals");
        protocol.Intervals.Sum(i => i.Seconds).Should().Be(2400);
        protocol.TotalMinutes.Should().Be(40);
        protocol.WeekIndex.Should().BeNull();
        protocol.RepeatWeek.Should().BeFalse();
    }

    [Fact]
    public void TotalMinutes_RoundsUpPartialMinutes()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 1);

        protocol.Intervals.Sum(i => i.Seconds).Should().Be(1800);
        protocol.TotalMinutes.Should().Be(30);

        var week2 = CardioProtocol.CouchToFiveK(Owner, 2);
        week2.Intervals.Sum(i => i.Seconds).Should().Be(1860);
        week2.TotalMinutes.Should().Be(31);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(41)]
    [InlineData(0)]
    public void Liss_OutsideThirtyToFortyMinutes_Throws(int minutes)
    {
        var act = () => CardioProtocol.Liss(Owner, minutes, null);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*30*40*");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(35)]
    [InlineData(40)]
    public void Liss_WithinRange_IsOneEasyInterval(int minutes)
    {
        var protocol = CardioProtocol.Liss(Owner, minutes, "Incline walk");

        protocol.Kind.Should().Be(CardioProtocolKind.Liss);
        protocol.Intervals.Should().ContainSingle()
            .Which.Should().Be(new CardioInterval("Easy", minutes * 60, CardioEffort.Easy));
        protocol.TotalMinutes.Should().Be(minutes);
        protocol.Notes.Should().Be("Incline walk");
    }

    [Fact]
    public void Liss_WithoutNote_HasEmptyNotes()
    {
        CardioProtocol.Liss(Owner, 30, null).Notes.Should().BeEmpty();
    }

    [Fact]
    public void CouchToFiveK_WeekOne_IsEightRunWalkRepeats()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 1);

        protocol.Kind.Should().Be(CardioProtocolKind.CouchToFiveK);
        protocol.WeekIndex.Should().Be(1);
        protocol.Name.Should().Be("Couch to 5K — Week 1");
        protocol.Intervals.Should().HaveCount(18);
        protocol.Intervals[0].Should().Be(new CardioInterval("Warm-up walk", 300, CardioEffort.WarmUp));
        protocol.Intervals.Skip(1).Take(16).Where((_, i) => i % 2 == 0)
            .Should().AllSatisfy(run => run.Should().Be(new CardioInterval(run.Label, 60, CardioEffort.Work)));
        protocol.Intervals.Skip(1).Take(16).Where((_, i) => i % 2 == 1)
            .Should().AllSatisfy(walk => walk.Should().Be(new CardioInterval(walk.Label, 90, CardioEffort.Recovery)));
        protocol.Intervals[1].Label.Should().Be("Run 1");
        protocol.Intervals[2].Label.Should().Be("Walk 1");
        protocol.Intervals[17].Should().Be(new CardioInterval("Cool-down walk", 300, CardioEffort.CoolDown));
    }

    [Fact]
    public void CouchToFiveK_WeekNine_IsThirtyMinuteRun()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 9);

        protocol.WeekIndex.Should().Be(9);
        protocol.Intervals.Should().Equal(
            new CardioInterval("Warm-up walk", 300, CardioEffort.WarmUp),
            new CardioInterval("Run", 1800, CardioEffort.Work),
            new CardioInterval("Cool-down walk", 300, CardioEffort.CoolDown));
        protocol.TotalMinutes.Should().Be(40);
    }

    [Theory]
    [InlineData(1, 18)]
    [InlineData(2, 14)]
    [InlineData(3, 10)]
    [InlineData(4, 9)]
    [InlineData(5, 7)]
    [InlineData(6, 7)]
    [InlineData(7, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 3)]
    public void CouchToFiveK_EveryWeek_StartsWithWarmUpAndEndsWithCoolDown(int week, int expectedIntervals)
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, week);

        protocol.Intervals.Should().HaveCount(expectedIntervals);
        protocol.Intervals.First().Effort.Should().Be(CardioEffort.WarmUp);
        protocol.Intervals.Last().Effort.Should().Be(CardioEffort.CoolDown);
        protocol.Intervals.Should().Contain(i => i.Effort == CardioEffort.Work);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void CouchToFiveK_WeekOutsideOneToNine_Throws(int week)
    {
        var act = () => CardioProtocol.CouchToFiveK(Owner, week);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void NextWeek_AdvancesTheWeekAndRebuildsIntervals()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 3);

        var result = protocol.NextWeek();

        result.Should().BeSameAs(protocol);
        protocol.WeekIndex.Should().Be(4);
        protocol.Name.Should().Be("Couch to 5K — Week 4");
        protocol.Intervals.Should().Equal(CardioProtocol.CouchToFiveK(Owner, 4).Intervals);
    }

    [Fact]
    public void NextWeek_WhenRepeatingTheWeek_StaysOnTheSameWeekOnceThenAdvances()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 5).WithRepeatWeek();

        protocol.RepeatWeek.Should().BeTrue();

        protocol.NextWeek();
        protocol.WeekIndex.Should().Be(5);
        protocol.RepeatWeek.Should().BeFalse();

        protocol.NextWeek();
        protocol.WeekIndex.Should().Be(6);
    }

    [Fact]
    public void NextWeek_CapsAtWeekNine()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 9);

        protocol.NextWeek();

        protocol.WeekIndex.Should().Be(9);
        protocol.Intervals.Should().Equal(CardioProtocol.CouchToFiveK(Owner, 9).Intervals);
    }

    [Fact]
    public void CouchToFiveK_CreatedWithRepeat_RepeatsTheWeek()
    {
        CardioProtocol.CouchToFiveK(Owner, 2, repeatWeek: true).RepeatWeek.Should().BeTrue();
    }

    [Fact]
    public void WithRepeatWeek_OnNonCouchToFiveK_Throws()
    {
        var act = () => CardioProtocol.NorwegianFourByFour(Owner).WithRepeatWeek();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void NextWeek_OnNonCouchToFiveK_Throws()
    {
        var act = () => CardioProtocol.Liss(Owner, 30, null).NextWeek();

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ClearRepeatWeek_TurnsRepeatOff()
    {
        var protocol = CardioProtocol.CouchToFiveK(Owner, 2, repeatWeek: true);

        protocol.ClearRepeatWeek();

        protocol.RepeatWeek.Should().BeFalse();
    }

    [Fact]
    public void CardioInterval_RejectsNonPositiveSeconds()
    {
        var act = () => new CardioInterval("Work", 0, CardioEffort.Work);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Ids_AreDistinctPerProtocol()
    {
        var a = CardioProtocol.NorwegianFourByFour(Owner);
        var b = CardioProtocol.NorwegianFourByFour(Owner);

        a.Id.Should().NotBe(b.Id);
        a.Id.Value.Should().NotBe(Guid.Empty);
    }
}
