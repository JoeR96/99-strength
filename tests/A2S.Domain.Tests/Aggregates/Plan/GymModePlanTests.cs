using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Plan;

public class GymModePlanTests
{
    private static readonly UserId Owner = new("user_plan_0001");
    private static readonly CardioProtocolId Stairs = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000003"));
    private static readonly MobilityRoutineId MobilityA = new(Guid.Parse("0a0a0a0a-0000-0000-0000-000000000001"));
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset RecordedAt = new(2026, 11, 16, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lift_CarriesAWarmUpAndAFinisher()
    {
        var session = ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", MobilityA, new SessionFinisher(Stairs));

        session.WarmUpRoutineId.Should().Be(MobilityA);
        session.Finisher!.CardioProtocolId.Should().Be(Stairs);
    }

    [Fact]
    public void Lift_WithoutExtras_KeepsTheOldDefaults()
    {
        var session = ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A");

        session.WarmUpRoutineId.Should().BeNull();
        session.Finisher.Should().BeNull();
    }

    [Fact]
    public void NonLiftSessions_CannotCarryAWarmUpOrFinisher()
    {
        var warmUp = () => new ScheduledSession(DayOfWeek.Monday, SessionKind.Cardio, null, Stairs, null, "Stairs", MobilityA);
        var finisher = () => new ScheduledSession(DayOfWeek.Monday, SessionKind.Mobility, null, null, MobilityA, "Mob", null, new SessionFinisher(Stairs));

        warmUp.Should().Throw<BusinessRuleViolationException>();
        finisher.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Finisher_NeedsAProtocol()
    {
        var act = () => new SessionFinisher(new CardioProtocolId(Guid.Empty));

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData(Effort.Easy, 4)]
    [InlineData(Effort.Solid, 7)]
    [InlineData(Effort.Brutal, 9)]
    public void Effort_MapsToRpe(Effort effort, int rpe)
    {
        effort.ToRpe().Should().Be(rpe);
    }

    [Fact]
    public void LinkCompletionToHevy_StoresTheId_SoTheSameHevyWorkoutDedupes()
    {
        var plan = TrainingPlan.Create(Owner, "Cut", Mon16Nov, [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        var completion = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, DayNumber.Day1, recordedAt: RecordedAt);

        var linked = plan.LinkCompletionToHevy(completion.Id, " hevy-1 ");
        var again = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Hevy, DayNumber.Day1, hevyWorkoutId: "hevy-1");

        linked.Should().BeTrue();
        completion.HevyWorkoutId.Should().Be("hevy-1");
        again.Should().BeSameAs(completion);
        plan.Completions.Should().ContainSingle();
    }

    [Fact]
    public void LinkCompletionToHevy_NeverOverwritesADifferentId()
    {
        var plan = TrainingPlan.Create(Owner, "Cut", Mon16Nov, [ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A")], 95m);
        var completion = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, DayNumber.Day1, recordedAt: RecordedAt);
        plan.LinkCompletionToHevy(completion.Id, "hevy-1");

        plan.LinkCompletionToHevy(completion.Id, "hevy-2").Should().BeFalse();
        plan.LinkCompletionToHevy(new SessionCompletionId(Guid.Parse("99999999-0000-0000-0000-000000000000")), "hevy-1").Should().BeFalse();
        completion.HevyWorkoutId.Should().Be("hevy-1");
    }
}
