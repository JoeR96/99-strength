using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class CardioWeekContextTests
{
    private static readonly UserId Owner = new("user_week_ctx_0001");
    private static readonly CardioTrackId Run = new(Guid.Parse("7a7a7a7a-0000-0000-0000-000000000011"));
    private static readonly CardioTrackId Stairs = new(Guid.Parse("7a7a7a7a-0000-0000-0000-000000000012"));
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private static TrainingPlan Plan() => TrainingPlan.Create(Owner, "Plan", Mon16Nov,
    [
        ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", finisher: SessionFinisher.ForTrack(Stairs)),
        ScheduledSession.Cardio(DayOfWeek.Tuesday, null, "Run", Run),
        ScheduledSession.Cardio(DayOfWeek.Thursday, null, "Run", Run)
    ], 95m);

    [Fact]
    public void CountsPlannedSlots_AndMissedPastDays()
    {
        var plan = Plan();
        plan.CompleteSession(Mon16Nov.AddDays(1), SessionKind.Cardio, CompletionSource.Manual, recordedAt: DateTimeOffset.UnixEpoch, cardioTrack: Run);

        CardioWeekContext.For(plan, Run, Mon16Nov.AddDays(3), Mon16Nov)
            .Should().Be(new WeekContext(2, 0), "Tuesday was done and Thursday is today");
        CardioWeekContext.For(plan, Run, Mon16Nov.AddDays(10), Mon16Nov)
            .Should().Be(new WeekContext(2, 2), "Thursday and the next Tuesday were missed");
    }

    [Fact]
    public void LiftFinishersCount_AndUnscheduledTracksHaveNoContext()
    {
        var plan = Plan();

        CardioWeekContext.For(plan, Stairs, Mon16Nov, Mon16Nov).Should().Be(new WeekContext(1, 0));
        CardioWeekContext.For(plan, new CardioTrackId(Guid.Parse("7a7a7a7a-0000-0000-0000-000000000099")), Mon16Nov, null).Should().BeNull();
        CardioWeekContext.For(null, Run, Mon16Nov, null).Should().BeNull();
    }

    [Fact]
    public void PausedDays_AreNotMissed()
    {
        var plan = Plan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Mon16Nov, Mon16Nov.AddDays(6), "Ill"));

        CardioWeekContext.For(plan, Run, Mon16Nov.AddDays(7), Mon16Nov).Should().Be(new WeekContext(2, 0));
    }
}
