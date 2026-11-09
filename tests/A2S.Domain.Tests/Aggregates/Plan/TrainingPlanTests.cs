using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Plan;

public class TrainingPlanTests
{
    private static readonly UserId Owner = new("user_plan_0001");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001"));
    private static readonly CardioProtocolId Liss = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000002"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset RecordedAt = new(2026, 11, 16, 18, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Wed11Nov = new(2026, 11, 11);
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);

    private static IReadOnlyList<ScheduledSession> BriefSchedule() =>
    [
        ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
        ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
        ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
        ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
        ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B"),
        ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B"),
        ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K or LISS"),
        ScheduledSession.Rest(DayOfWeek.Sunday, "Rest — walk only")
    ];

    private static TrainingPlan CreatePlan(DateOnly? requestedStart = null) =>
        TrainingPlan.Create(Owner, "Cut to 85 — Nov 2026", requestedStart ?? Wed11Nov, BriefSchedule(), 95m);

    [Fact]
    public void Create_OnAWednesday_StartsTheComingMonday()
    {
        var plan = CreatePlan(Wed11Nov);

        plan.StartDate.Should().Be(Mon16Nov);
    }

    [Fact]
    public void Create_OnAMonday_StartsThatDay()
    {
        var plan = CreatePlan(Mon16Nov);

        plan.StartDate.Should().Be(Mon16Nov);
    }

    [Fact]
    public void Create_SetsDefaultsAndRaisesStartedEvent()
    {
        var plan = CreatePlan();

        plan.UserId.Should().Be(Owner);
        plan.Name.Should().Be("Cut to 85 — Nov 2026");
        plan.IsActive.Should().BeTrue();
        plan.LinkedWorkoutId.Should().BeNull();
        plan.StartWeightKg.Should().Be(95m);
        plan.Allowances.Should().Be(new WeeklyAllowances(1, 1));
        plan.Phases.Should().BeEmpty();
        plan.Checkpoints.Should().BeEmpty();
        plan.Completions.Should().BeEmpty();
        plan.AllowanceUses.Should().BeEmpty();
        plan.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TrainingPlanStarted>()
            .Which.Should().Match<TrainingPlanStarted>(e => e.PlanId == plan.Id && e.UserId == Owner && e.StartDate == Mon16Nov);
    }

    [Fact]
    public void Create_OrdersScheduleMondayFirstKeepingInsertionOrderWithinADay()
    {
        var shuffled = new[]
        {
            ScheduledSession.Rest(DayOfWeek.Sunday, "Rest"),
            ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "4×4")
        };

        var plan = TrainingPlan.Create(Owner, "Plan", Mon16Nov, shuffled, 95m);

        plan.Schedule.Select(s => s.Label).Should().Equal("Upper A", "Mobility B", "4×4", "Rest");
    }

    [Fact]
    public void Create_WithOnlyRestSessions_Throws()
    {
        var act = () => TrainingPlan.Create(Owner, "Lazy", Mon16Nov, [ScheduledSession.Rest(DayOfWeek.Sunday, "Rest")], 95m);

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*non-rest*");
    }

    [Theory]
    [InlineData(29)]
    [InlineData(301)]
    public void Create_WithStartWeightOutOfRange_Throws(decimal kg)
    {
        var act = () => TrainingPlan.Create(Owner, "Plan", Mon16Nov, BriefSchedule(), kg);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        var act = () => TrainingPlan.Create(Owner, " ", Mon16Nov, BriefSchedule(), 95m);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void SessionsOn_BeforeStart_IsEmpty()
    {
        var plan = CreatePlan(Wed11Nov);

        plan.SessionsOn(new DateOnly(2026, 11, 12)).Should().BeEmpty();
        plan.SessionsOn(new DateOnly(2026, 11, 15)).Should().BeEmpty();
    }

    [Fact]
    public void SessionsOn_AWednesdayAfterStart_ReturnsBothSessions()
    {
        var plan = CreatePlan();

        var sessions = plan.SessionsOn(new DateOnly(2026, 11, 18));

        sessions.Select(s => s.Kind).Should().Equal(SessionKind.Cardio, SessionKind.Mobility);
    }

    [Fact]
    public void SessionsOn_StartDate_ReturnsMondaysLift()
    {
        var plan = CreatePlan();

        plan.SessionsOn(Mon16Nov).Should().ContainSingle().Which.LiftDay.Should().Be(DayNumber.Day1);
    }

    [Fact]
    public void ReplaceSchedule_SwapsTheWholeSchedule()
    {
        var plan = CreatePlan();

        plan.ReplaceSchedule([ScheduledSession.Lift(DayOfWeek.Saturday, DayNumber.Day1, "Only day")]);

        plan.Schedule.Should().ContainSingle().Which.Day.Should().Be(DayOfWeek.Saturday);
    }

    [Fact]
    public void AddPhase_OverlappingAnExistingPhase_Throws()
    {
        var plan = CreatePlan();
        plan.AddPhase(new PlanPhase(PhaseType.Cut, new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 21), null));

        var act = () => plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2026, 12, 21), new DateOnly(2027, 1, 3), "Christmas"));

        act.Should().Throw<BusinessRuleViolationException>().WithMessage("*overlap*");
        plan.Phases.Should().HaveCount(1);
    }

    [Fact]
    public void AddPhase_AdjacentPhases_AreAllowedAndKeptInDateOrder()
    {
        var plan = CreatePlan();
        plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3), "Christmas"));
        plan.AddPhase(new PlanPhase(PhaseType.Cut, new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 21), null));

        plan.Phases.Select(p => p.Type).Should().Equal(PhaseType.Cut, PhaseType.Maintenance);
        plan.DomainEvents.OfType<PhaseAdded>().Should().HaveCount(2);
    }

    [Fact]
    public void PlanPhase_WithFromAfterTo_Throws()
    {
        var act = () => new PlanPhase(PhaseType.Pause, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 1), null);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void RemovePhase_ByStartDate_RemovesIt()
    {
        var plan = CreatePlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 6), "Away"));

        plan.RemovePhase(new DateOnly(2026, 12, 2)).Should().BeTrue();
        plan.RemovePhase(new DateOnly(2026, 12, 2)).Should().BeFalse();
        plan.Phases.Should().BeEmpty();
    }

    [Fact]
    public void IsPaused_InsideAPausePhase_IsTrueAndOutsideIsFalse()
    {
        var plan = CreatePlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 6), "Away"));
        plan.AddPhase(new PlanPhase(PhaseType.Cut, new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 1), null));

        plan.IsPaused(new DateOnly(2026, 12, 1)).Should().BeFalse();
        plan.IsPaused(new DateOnly(2026, 12, 2)).Should().BeTrue();
        plan.IsPaused(new DateOnly(2026, 12, 6)).Should().BeTrue();
        plan.IsPaused(new DateOnly(2026, 12, 7)).Should().BeFalse();
        plan.PhaseOn(new DateOnly(2026, 11, 20))!.Type.Should().Be(PhaseType.Cut);
        plan.PhaseOn(new DateOnly(2026, 12, 7)).Should().BeNull();
    }

    [Fact]
    public void AddCheckpoint_KeepsDateOrderAndRejectsDuplicateDates()
    {
        var plan = CreatePlan();
        plan.AddCheckpoint(new Checkpoint(new DateOnly(2026, 12, 21), 91m, "Cut pauses after this"));
        plan.AddCheckpoint(new Checkpoint(new DateOnly(2026, 12, 7), 92m, "4 cutting weeks"));

        var act = () => plan.AddCheckpoint(new Checkpoint(new DateOnly(2026, 12, 7), 90m, null));

        act.Should().Throw<BusinessRuleViolationException>();
        plan.Checkpoints.Select(c => c.TargetKg).Should().Equal(92m, 91m);
    }

    [Fact]
    public void RemoveCheckpoint_ByDate_RemovesIt()
    {
        var plan = CreatePlan();
        plan.AddCheckpoint(new Checkpoint(new DateOnly(2027, 3, 8), 85m, "Goal"));

        plan.RemoveCheckpoint(new DateOnly(2027, 3, 8)).Should().BeTrue();
        plan.RemoveCheckpoint(new DateOnly(2027, 3, 8)).Should().BeFalse();
    }

    [Theory]
    [InlineData(29)]
    [InlineData(301)]
    public void Checkpoint_WithTargetOutOfRange_Throws(decimal kg)
    {
        var act = () => new Checkpoint(new DateOnly(2027, 3, 8), kg, null);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void LinkWorkout_ActivateAndDeactivate_UpdateState()
    {
        var plan = CreatePlan();
        var workoutId = new WorkoutId(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));

        plan.LinkWorkout(workoutId);
        plan.Deactivate();

        plan.LinkedWorkoutId.Should().Be(workoutId);
        plan.IsActive.Should().BeFalse();

        plan.Activate();
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CompleteSession_WithTheSameHevyId_ReturnsTheExistingCompletionAndCountStaysOne()
    {
        var plan = CreatePlan();

        var first = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Hevy,
            liftDay: DayNumber.Day1, hevyWorkoutId: "hevy-123", recordedAt: RecordedAt);
        var second = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Hevy,
            liftDay: DayNumber.Day1, hevyWorkoutId: "hevy-123", recordedAt: RecordedAt.AddMinutes(5));

        second.Should().BeSameAs(first);
        plan.Completions.Should().HaveCount(1);
        first.Source.Should().Be(CompletionSource.Hevy);
        first.HevyWorkoutId.Should().Be("hevy-123");
        first.RecordedAtUtc.Should().Be(RecordedAt);
        plan.DomainEvents.OfType<SessionCompleted>().Should().ContainSingle()
            .Which.Should().Match<SessionCompleted>(e => e.PlanId == plan.Id && e.CompletionId == first.Id && e.Date == Mon16Nov && e.Kind == SessionKind.Lift);
    }

    [Fact]
    public void CompleteSession_ManualDuplicateSameDayAndKind_ReturnsExisting()
    {
        var plan = CreatePlan();
        var wednesday = new DateOnly(2026, 11, 18);

        var first = plan.CompleteSession(wednesday, SessionKind.Cardio, CompletionSource.Manual,
            cardio: FourByFour, log: new CardioLog(2580, 165, 8, null, "Hilly"), recordedAt: RecordedAt);
        var second = plan.CompleteSession(wednesday, SessionKind.Cardio, CompletionSource.Manual,
            cardio: FourByFour, recordedAt: RecordedAt.AddHours(1));

        second.Should().BeSameAs(first);
        plan.Completions.Should().HaveCount(1);
        first.Cardio.Should().Be(new CardioLog(2580, 165, 8, null, "Hilly"));
    }

    [Fact]
    public void CompleteSession_DifferentLiftDaysSameDate_AreSeparateCompletions()
    {
        var plan = CreatePlan();

        plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day1, recordedAt: RecordedAt);
        plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day2, recordedAt: RecordedAt);

        plan.Completions.Should().HaveCount(2);
    }

    [Fact]
    public void CompleteSession_Mobility_KeepsDrillTicks()
    {
        var plan = CreatePlan();

        var completion = plan.CompleteSession(new DateOnly(2026, 11, 18), SessionKind.Mobility, CompletionSource.Manual,
            mobility: MobilityB, drillTicks: [true, true, false, true, true], recordedAt: RecordedAt);

        completion.MobilityRoutineId.Should().Be(MobilityB);
        completion.DrillTicks.Should().Equal(true, true, false, true, true);
        completion.Kind.Should().Be(SessionKind.Mobility);
        completion.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CompleteSession_Rest_Throws()
    {
        var plan = CreatePlan();

        var act = () => plan.CompleteSession(new DateOnly(2026, 11, 22), SessionKind.Rest, CompletionSource.Manual, recordedAt: RecordedAt);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void CompleteSession_LiftWithoutLiftDay_Throws()
    {
        var plan = CreatePlan();

        var act = () => plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, recordedAt: RecordedAt);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void CompleteSession_HevySourceWithoutId_Throws()
    {
        var plan = CreatePlan();

        var act = () => plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Hevy, liftDay: DayNumber.Day1, recordedAt: RecordedAt);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void UndoCompletion_RemovesItAndReportsWhetherAnythingWasRemoved()
    {
        var plan = CreatePlan();
        var completion = plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day1, recordedAt: RecordedAt);

        plan.UndoCompletion(completion.Id).Should().BeTrue();
        plan.UndoCompletion(completion.Id).Should().BeFalse();
        plan.Completions.Should().BeEmpty();
    }

    [Fact]
    public void UseAllowance_TwiceSameKindSameWeek_IsAllowed()
    {
        var plan = CreatePlan();

        var friday = plan.UseAllowance(new DateOnly(2026, 11, 20), AllowanceKind.FlexibleMeal);
        var saturday = plan.UseAllowance(new DateOnly(2026, 11, 21), AllowanceKind.FlexibleMeal);

        plan.AllowanceUses.Should().Equal(friday, saturday);
        friday.Should().Be(new AllowanceUse(new DateOnly(2026, 11, 20), AllowanceKind.FlexibleMeal));
    }

    [Fact]
    public void RemoveAllowanceUse_RemovesOneMatchingUse()
    {
        var plan = CreatePlan();
        plan.UseAllowance(new DateOnly(2026, 11, 20), AllowanceKind.Alcohol);
        plan.UseAllowance(new DateOnly(2026, 11, 20), AllowanceKind.Alcohol);

        plan.RemoveAllowanceUse(new DateOnly(2026, 11, 20), AllowanceKind.Alcohol).Should().BeTrue();
        plan.AllowanceUses.Should().HaveCount(1);
        plan.RemoveAllowanceUse(new DateOnly(2026, 11, 20), AllowanceKind.FlexibleMeal).Should().BeFalse();
    }

    [Fact]
    public void WeeklyAllowances_RejectNegativeValues()
    {
        var act = () => new WeeklyAllowances(-1, 1);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void CardioLog_RejectsNonPositiveDuration()
    {
        var act = () => new CardioLog(0, null, null, null, null);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ScheduledSession_LiftWithoutLiftDay_Throws()
    {
        var act = () => new ScheduledSession(DayOfWeek.Monday, SessionKind.Lift, null, null, null, "Upper A");

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
