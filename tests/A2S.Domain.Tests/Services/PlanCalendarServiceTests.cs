using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace A2S.Domain.Tests.Services;

public class PlanCalendarServiceTests
{
    private static readonly UserId Owner = new("user_calendar_0001");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000001"));
    private static readonly CardioProtocolId Liss = new(Guid.Parse("c4c4c4c4-0000-0000-0000-000000000002"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-000000000001"));
    private static readonly ExerciseId Bench = new(Guid.Parse("e1e1e1e1-0000-0000-0000-000000000001"));
    private static readonly IReadOnlyCollection<WorkoutActivity> NoHistory = Array.Empty<WorkoutActivity>();

    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateOnly Tue17Nov = new(2026, 11, 17);
    private static readonly DateOnly Wed18Nov = new(2026, 11, 18);
    private static readonly DateOnly Thu19Nov = new(2026, 11, 19);
    private static readonly DateOnly Fri20Nov = new(2026, 11, 20);
    private static readonly DateOnly Sat21Nov = new(2026, 11, 21);
    private static readonly DateOnly Sun22Nov = new(2026, 11, 22);

    private static PlanCalendarService ServiceAt(string utcIso) =>
        new(new LocalClock(new FakeTimeProvider(DateTimeOffset.Parse(utcIso, null, System.Globalization.DateTimeStyles.AssumeUniversal))));

    private static TrainingPlan BriefPlan(DateOnly? requestedStart = null) =>
        TrainingPlan.Create(Owner, "Cut to 85", requestedStart ?? Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, FourByFour, "Norwegian 4×4"),
            ScheduledSession.Mobility(DayOfWeek.Wednesday, MobilityB, "Mobility B"),
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day3, "Upper B"),
            ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day4, "Lower B"),
            ScheduledSession.Cardio(DayOfWeek.Saturday, null, "C25K or LISS"),
            ScheduledSession.Rest(DayOfWeek.Sunday, "Rest — walk only")
        ], 95m);

    private static WorkoutActivity Activity(DayNumber day, string completedAtUtcIso)
    {
        var completedAt = DateTime.Parse(completedAtUtcIso, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        var weight = Weight.Create(100m, WeightUnit.Kilograms);
        var performance = new ExercisePerformance(
            Bench,
            [new PlannedSet(1, weight, 5)],
            [new CompletedSet(1, weight, 5)],
            completedAt);
        return new WorkoutActivity(day, 1, 1, [performance], null, completedAt);
    }

    [Fact]
    public void ReviewFocus1_PauseStartingMidWeekWithTwoLiftsDone()
    {
        var service = ServiceAt("2026-11-21T12:00:00Z");
        var plan = BriefPlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Wed18Nov, Sun22Nov, "Away"));
        plan.CompleteSession(Mon16Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day1, recordedAt: new DateTimeOffset(2026, 11, 16, 19, 0, 0, TimeSpan.Zero));
        var history = new[] { Activity(DayNumber.Day2, "2026-11-17T18:30:00Z") };

        var week = service.Week(plan, Thu19Nov, history);
        var goals = service.ForWeek(plan, Thu19Nov, history);

        week.Select(d => d.Date).Should().Equal(Mon16Nov, Tue17Nov, Wed18Nov, Thu19Nov, Fri20Nov, Sat21Nov, Sun22Nov);
        week[0].Sessions.Single().Status.Should().Be(SessionStatus.Done);
        week[0].Sessions.Single().Completion.Should().NotBeNull();
        week[1].Sessions.Single().Status.Should().Be(SessionStatus.Done);
        week[1].Sessions.Single().Completion.Should().BeNull();
        week[2].Phase.Should().Be(PhaseType.Pause);
        week[2].Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Paused, SessionStatus.Paused);
        week[3].Sessions.Single().Status.Should().Be(SessionStatus.Paused);
        week[4].Sessions.Single().Status.Should().Be(SessionStatus.Paused);
        week[5].Sessions.Single().Status.Should().Be(SessionStatus.Paused);
        week[6].Sessions.Single().Status.Should().Be(SessionStatus.Rest);

        goals.WeekStart.Should().Be(Mon16Nov);
        goals.LiftsPlanned.Should().Be(2);
        goals.LiftsDone.Should().Be(2);
        goals.CardioPlanned.Should().Be(0);
        goals.CardioDone.Should().Be(0);
        goals.MobilityPlanned.Should().Be(0);
        goals.MobilityDone.Should().Be(0);
    }

    [Fact]
    public void ReviewFocus2_ActivityAt2330UtcOn24OctLandsOnThe25th()
    {
        var service = ServiceAt("2026-10-26T09:00:00Z");
        var plan = TrainingPlan.Create(Owner, "Weekend lifter", new DateOnly(2026, 10, 19),
        [
            ScheduledSession.Lift(DayOfWeek.Saturday, DayNumber.Day1, "Saturday"),
            ScheduledSession.Lift(DayOfWeek.Sunday, DayNumber.Day2, "Sunday")
        ], 95m);
        var history = new[] { Activity(DayNumber.Day2, "2026-10-24T23:30:00Z") };

        var saturday = service.DayFor(plan, new DateOnly(2026, 10, 24), history);
        var sunday = service.DayFor(plan, new DateOnly(2026, 10, 25), history);

        sunday.Sessions.Single().Status.Should().Be(SessionStatus.Done);
        saturday.Sessions.Single().Status.Should().Be(SessionStatus.Missed);
    }

    [Fact]
    public void DayFor_YesterdaysUndoneLiftIsMissed_TodaysAndTomorrowsArePlanned()
    {
        var service = ServiceAt("2026-11-17T08:00:00Z");
        var plan = BriefPlan();

        service.DayFor(plan, Mon16Nov, NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
        service.DayFor(plan, Tue17Nov, NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Planned);
        service.DayFor(plan, Thu19Nov, NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Planned);
    }

    [Fact]
    public void DayFor_TodayIsTheLocalDate_NotTheUtcDate()
    {
        var service = ServiceAt("2026-06-30T23:30:00Z");
        var plan = TrainingPlan.Create(Owner, "Summer", new DateOnly(2026, 6, 29),
            [ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day1, "Tue"), ScheduledSession.Lift(DayOfWeek.Wednesday, DayNumber.Day2, "Wed")], 95m);

        service.DayFor(plan, new DateOnly(2026, 6, 30), NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
        service.DayFor(plan, new DateOnly(2026, 7, 1), NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Planned);
    }

    [Fact]
    public void DayFor_Sunday_IsRest()
    {
        var service = ServiceAt("2026-11-30T08:00:00Z");

        var day = service.DayFor(BriefPlan(), Sun22Nov, NoHistory);

        day.Sessions.Single().Status.Should().Be(SessionStatus.Rest);
        day.Sessions.Single().Session.Kind.Should().Be(SessionKind.Rest);
        day.Phase.Should().BeNull();
    }

    [Fact]
    public void DayFor_WednesdayWithTwoSessions_ReturnsBoth()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");

        var day = service.DayFor(BriefPlan(), Wed18Nov, NoHistory);

        day.Sessions.Select(s => s.Session.Label).Should().Equal("Norwegian 4×4", "Mobility B");
        day.Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Planned, SessionStatus.Planned);
    }

    [Fact]
    public void DayFor_BeforeStart_HasNoSessions()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");

        service.DayFor(BriefPlan(), new DateOnly(2026, 11, 15), NoHistory).Sessions.Should().BeEmpty();
    }

    [Fact]
    public void DayFor_CompletedInsideAPause_IsDoneNotPaused()
    {
        var service = ServiceAt("2026-11-21T12:00:00Z");
        var plan = BriefPlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Wed18Nov, Sun22Nov, "Away"));
        plan.CompleteSession(Thu19Nov, SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day3, recordedAt: new DateTimeOffset(2026, 11, 19, 19, 0, 0, TimeSpan.Zero));

        var day = service.DayFor(plan, Thu19Nov, NoHistory);

        day.Phase.Should().Be(PhaseType.Pause);
        day.Sessions.Single().Status.Should().Be(SessionStatus.Done);
    }

    [Fact]
    public void DayFor_LiftHistoryOnTheRightDateButWrongDayNumber_DoesNotCount()
    {
        var service = ServiceAt("2026-11-21T12:00:00Z");
        var history = new[] { Activity(DayNumber.Day4, "2026-11-16T18:30:00Z") };

        service.DayFor(BriefPlan(), Mon16Nov, history).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
    }

    [Fact]
    public void DayFor_OpenCardioSlot_IsDoneByAnyCardioCompletionThatDay()
    {
        var service = ServiceAt("2026-11-22T12:00:00Z");
        var plan = BriefPlan();
        plan.CompleteSession(Sat21Nov, SessionKind.Cardio, CompletionSource.Manual, cardio: Liss, recordedAt: new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero));

        var session = service.DayFor(plan, Sat21Nov, NoHistory).Sessions.Single();

        session.Status.Should().Be(SessionStatus.Done);
        session.Completion!.CardioProtocolId.Should().Be(Liss);
    }

    [Fact]
    public void DayFor_MobilityCompletion_MarksTheMobilitySessionOnly()
    {
        var service = ServiceAt("2026-11-19T12:00:00Z");
        var plan = BriefPlan();
        plan.CompleteSession(Wed18Nov, SessionKind.Mobility, CompletionSource.Manual, mobility: MobilityB, recordedAt: new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero));

        var day = service.DayFor(plan, Wed18Nov, NoHistory);

        day.Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Missed, SessionStatus.Done);
    }

    [Fact]
    public void Month_ReturnsEveryDayOfTheMonth()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");

        var month = service.Month(BriefPlan(), 2026, 11, NoHistory);

        month.Should().HaveCount(30);
        month.First().Date.Should().Be(new DateOnly(2026, 11, 1));
        month.Last().Date.Should().Be(new DateOnly(2026, 11, 30));
        month.Where(d => d.Date < Mon16Nov).Should().AllSatisfy(d => d.Sessions.Should().BeEmpty());
        month.Single(d => d.Date == Mon16Nov).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
    }

    [Fact]
    public void Week_StartsOnMondayWhateverDayIsAsked()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");

        var week = service.Week(BriefPlan(), Sun22Nov, NoHistory);

        week.Select(d => d.Date).Should().Equal(Mon16Nov, Tue17Nov, Wed18Nov, Thu19Nov, Fri20Nov, Sat21Nov, Sun22Nov);
    }

    [Fact]
    public void ForWeek_CountsPlannedDoneAndAllowancesFromBothSources()
    {
        var service = ServiceAt("2026-11-20T12:00:00Z");
        var plan = BriefPlan();
        plan.CompleteSession(Tue17Nov, SessionKind.Lift, CompletionSource.Hevy, liftDay: DayNumber.Day2, hevyWorkoutId: "h-1", recordedAt: new DateTimeOffset(2026, 11, 17, 19, 0, 0, TimeSpan.Zero));
        plan.CompleteSession(Wed18Nov, SessionKind.Cardio, CompletionSource.Manual, cardio: FourByFour, recordedAt: new DateTimeOffset(2026, 11, 18, 19, 0, 0, TimeSpan.Zero));
        plan.UseAllowance(Fri20Nov, AllowanceKind.FlexibleMeal);
        plan.UseAllowance(Fri20Nov, AllowanceKind.FlexibleMeal);
        plan.UseAllowance(new DateOnly(2026, 11, 13), AllowanceKind.Alcohol);
        var history = new[] { Activity(DayNumber.Day1, "2026-11-16T18:30:00Z") };

        var goals = service.ForWeek(plan, Wed18Nov, history);

        goals.Should().Be(new WeeklyGoals(
            WeekStart: Mon16Nov,
            LiftsPlanned: 4, LiftsDone: 2,
            CardioPlanned: 2, CardioDone: 1,
            MobilityPlanned: 1, MobilityDone: 0,
            FlexibleMealsUsed: 2, FlexibleMealsAllowed: 1,
            AlcoholOccasions: 0, AlcoholAllowed: 1,
            HighIntensityMinutesDone: 0, HighIntensityMinutesMin: 15, HighIntensityMinutesMax: 30));
    }

    [Fact]
    public void ForWeek_BeforeThePlanStarts_HasNothingPlanned()
    {
        var service = ServiceAt("2026-11-10T12:00:00Z");

        var goals = service.ForWeek(BriefPlan(), new DateOnly(2026, 11, 10), NoHistory);

        goals.WeekStart.Should().Be(new DateOnly(2026, 11, 9));
        goals.LiftsPlanned.Should().Be(0);
        goals.CardioPlanned.Should().Be(0);
        goals.MobilityPlanned.Should().Be(0);
    }
}
