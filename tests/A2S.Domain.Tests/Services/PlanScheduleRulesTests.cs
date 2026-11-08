using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

/// <summary>Brief § 5: "no 4×4 the day before Lower A or B" is a warning, never a block.</summary>
public class PlanScheduleRulesTests
{
    private static readonly CardioProtocolId FourByFourId = new(Guid.Parse("44444444-0000-0000-0000-000000000001"));
    private static readonly CardioProtocolId LissId = new(Guid.Parse("11551155-0000-0000-0000-000000000001"));

    private static CardioProtocolKind? KindOf(CardioProtocolId id) =>
        id == FourByFourId ? CardioProtocolKind.NorwegianFourByFour
        : id == LissId ? CardioProtocolKind.Liss
        : null;

    private static List<ScheduledSession> DefaultWeek() =>
    [
        ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
        ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
        ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFourId, "Norwegian 4×4 — outdoors, hills"),
        ScheduledSession.Mobility(DayOfWeek.Wednesday, null, "Mobility B"),
        ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B"),
        ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B"),
        ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K run or LISS 30–40 min"),
        ScheduledSession.Rest(DayOfWeek.Sunday, "Rest — walk only")
    ];

    [Fact]
    public void Warnings_OnTheDefaultWeek_IsClean()
    {
        PlanScheduleRules.Warnings(DefaultWeek(), KindOf).Should().BeEmpty("Wednesday 4×4 sits before Upper B, which is fine");
    }

    [Fact]
    public void Warnings_WhenFourByFourMovesToMonday_WarnsAboutLowerA()
    {
        var week = DefaultWeek();
        week[2] = ScheduledSession.Cardio(DayOfWeek.Monday, FourByFourId, "Norwegian 4×4");

        var warnings = PlanScheduleRules.Warnings(week, KindOf);

        warnings.Should().ContainSingle();
        warnings[0].Code.Should().Be(PlanScheduleRules.FourByFourBeforeLowerDay);
        warnings[0].Message.Should().Contain("Monday").And.Contain("Lower A").And.Contain("Tuesday");
    }

    [Fact]
    public void Warnings_WhenFourByFourIsOnThursday_WarnsAboutLowerB()
    {
        var week = DefaultWeek();
        week[2] = ScheduledSession.Cardio(DayOfWeek.Thursday, FourByFourId, "Norwegian 4×4");

        var warnings = PlanScheduleRules.Warnings(week, KindOf);

        warnings.Should().ContainSingle().Which.Message.Should().Contain("Lower B");
    }

    [Fact]
    public void Warnings_SundayCountsAsTheDayBeforeMonday()
    {
        var week = new List<ScheduledSession>
        {
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Lower A"),
            ScheduledSession.Cardio(DayOfWeek.Sunday, FourByFourId, "Norwegian 4×4")
        };

        PlanScheduleRules.Warnings(week, KindOf).Should().ContainSingle().Which.Message.Should().Contain("Sunday");
    }

    [Fact]
    public void Warnings_RecognisesFourByFourByLabelWhenNoProtocolIsKnown()
    {
        var week = new List<ScheduledSession>
        {
            ScheduledSession.Cardio(DayOfWeek.Monday, null, "4x4 intervals"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A")
        };

        PlanScheduleRules.Warnings(week).Should().ContainSingle();
    }

    [Fact]
    public void Warnings_ProtocolKindWinsOverTheLabel()
    {
        var week = new List<ScheduledSession>
        {
            ScheduledSession.Cardio(DayOfWeek.Monday, LissId, "Cardio 4×4 slot now used for a walk"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A")
        };

        PlanScheduleRules.Warnings(week, KindOf).Should().BeEmpty();
    }

    [Fact]
    public void Warnings_LissBeforeALowerDay_IsFine()
    {
        var week = new List<ScheduledSession>
        {
            ScheduledSession.Cardio(DayOfWeek.Monday, LissId, "LISS 35 min"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A")
        };

        PlanScheduleRules.Warnings(week, KindOf).Should().BeEmpty();
    }

    [Fact]
    public void Warnings_FourByFourBeforeAnUpperDay_IsFine()
    {
        var week = new List<ScheduledSession>
        {
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFourId, "Norwegian 4×4"),
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B")
        };

        PlanScheduleRules.Warnings(week, KindOf).Should().BeEmpty();
    }
}
