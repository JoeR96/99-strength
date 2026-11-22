using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class CardioGuardrailTests
{
    private static readonly CardioTrackId Stair = new(new Guid("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly CardioTrackId Walk = new(new Guid("aaaaaaaa-0000-0000-0000-000000000002"));

    private static bool IsHard(CardioTrackId id) => id == Stair;

    [Fact]
    public void HardTrackDayBeforeLowerDay_Warns()
    {
        var schedule = new[]
        {
            ScheduledSession.Cardio(DayOfWeek.Sunday, null, "Stair intervals", Stair),
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Lower A")
        };

        PlanScheduleRules.Warnings(schedule, isHardTrack: IsHard)
            .Should().ContainSingle(w => w.Code == PlanScheduleRules.HardCardioBeforeLowerDay);
    }

    [Fact]
    public void EasyTrackDayBeforeLowerDay_DoesNotWarn()
    {
        var schedule = new[]
        {
            ScheduledSession.Cardio(DayOfWeek.Sunday, null, "Walk", Walk),
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Lower A")
        };

        PlanScheduleRules.Warnings(schedule, isHardTrack: IsHard).Should().BeEmpty();
    }

    [Fact]
    public void FinisherOnLowerDay_Warns_OnUpperDayDoesNot()
    {
        var lower = new[]
        {
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Lower A"),
            ScheduledSession.Cardio(DayOfWeek.Monday, null, "Stair finisher", Stair)
        };
        var upper = new[]
        {
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Tuesday, null, "Stair finisher", Stair)
        };

        PlanScheduleRules.Warnings(lower, isHardTrack: IsHard).Should().ContainSingle(w => w.Code == PlanScheduleRules.FinisherOnLowerDay);
        PlanScheduleRules.Warnings(upper, isHardTrack: IsHard).Should().BeEmpty();
    }

    [Fact]
    public void HighIntensityOverMax_WarnsOnlyAboveMax()
    {
        var target = new HighIntensityTarget(15, 30);

        PlanScheduleRules.HighIntensityOverMax(30, target).Should().BeNull();
        PlanScheduleRules.HighIntensityOverMax(31, target)!.Code.Should().Be(PlanScheduleRules.HighIntensityAboveMax);
    }

    [Fact]
    public void ScheduledSession_OnlyCardioCarriesATrack()
    {
        var act = () => new ScheduledSession(DayOfWeek.Monday, SessionKind.Rest, null, null, null, "Rest", Walk);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
