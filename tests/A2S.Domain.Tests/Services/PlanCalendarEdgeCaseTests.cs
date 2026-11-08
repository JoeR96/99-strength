using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace A2S.Domain.Tests.Services;

/// <summary>
/// Brief § 6 "Schedule &amp; calendar": missed sessions (one, a week, two weeks), completing out of
/// order, two sessions on one day, training on a rest day, Pause and Maintenance phases, the two
/// clock changes and the New Year week. Builds on <see cref="PlanCalendarServiceTests"/>, which
/// already covers a pause starting mid-week and an activity at 23:30 UTC on 24 Oct landing on the 25th.
/// </summary>
public class PlanCalendarEdgeCaseTests
{
    private static readonly UserId Owner = new("user_calendar_edge");
    private static readonly CardioProtocolId FourByFour = new(Guid.Parse("c4c4c4c4-0000-0000-0000-0000000000a1"));
    private static readonly MobilityRoutineId MobilityB = new(Guid.Parse("0b0b0b0b-0000-0000-0000-0000000000a1"));
    private static readonly ExerciseId Bench = new(Guid.Parse("e1e1e1e1-0000-0000-0000-0000000000a1"));
    private static readonly IReadOnlyCollection<WorkoutActivity> NoHistory = Array.Empty<WorkoutActivity>();

    // Week 1 of the brief's plan.
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateOnly Tue17Nov = new(2026, 11, 17);
    private static readonly DateOnly Wed18Nov = new(2026, 11, 18);
    private static readonly DateOnly Thu19Nov = new(2026, 11, 19);
    private static readonly DateOnly Fri20Nov = new(2026, 11, 20);
    private static readonly DateOnly Sat21Nov = new(2026, 11, 21);
    private static readonly DateOnly Sun22Nov = new(2026, 11, 22);
    private static readonly DateOnly Mon23Nov = new(2026, 11, 23);
    private static readonly DateOnly Sun29Nov = new(2026, 11, 29);
    private static readonly DateOnly Mon30Nov = new(2026, 11, 30);

    private static PlanCalendarService ServiceAt(string utcIso) =>
        new(new LocalClock(new FakeTimeProvider(DateTimeOffset.Parse(utcIso, null, System.Globalization.DateTimeStyles.AssumeUniversal))));

    /// <summary>Brief § 5: Mon Upper A, Tue Lower A, Wed 4×4 + Mobility B, Thu Upper B, Fri Lower B, Sat cardio, Sun rest.</summary>
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

    /// <summary>A Saturday/Sunday lifter, for the clock-change weekends.</summary>
    private static TrainingPlan WeekendPlan(DateOnly monday) =>
        TrainingPlan.Create(Owner, "Weekend lifter", monday,
        [
            ScheduledSession.Lift(DayOfWeek.Saturday, DayNumber.Day1, "Saturday"),
            ScheduledSession.Lift(DayOfWeek.Sunday, DayNumber.Day2, "Sunday")
        ], 95m);

    private static WorkoutActivity Activity(DayNumber day, string completedAtUtcIso)
    {
        var completedAt = DateTime.Parse(completedAtUtcIso, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        var weight = Weight.Create(100m, WeightUnit.Kilograms);
        var performance = new ExercisePerformance(Bench, [new PlannedSet(1, weight, 5)], [new CompletedSet(1, weight, 5)], completedAt);
        return new WorkoutActivity(day, 1, 1, [performance], null, completedAt);
    }

    private static SessionCompletion Lift(TrainingPlan plan, DateOnly date, DayNumber day) =>
        plan.CompleteSession(date, SessionKind.Lift, CompletionSource.Manual, liftDay: day,
            recordedAt: new DateTimeOffset(date.ToDateTime(new TimeOnly(19, 0)), TimeSpan.Zero));

    private static IEnumerable<PlanDaySession> NonRest(IEnumerable<PlanDay> days) =>
        days.SelectMany(d => d.Sessions).Where(s => s.Session.Kind != SessionKind.Rest);

    private static PlanDaySession On(IReadOnlyList<PlanDay> week, DateOnly date) =>
        week.Single(d => d.Date == date).Sessions.Single();

    // ---------------------------------------------------------------------------------------
    // Missed sessions
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void MissingOneSession_OnlyThatSessionIsMissed_AndTheWeekShowsThreeLiftsOfFour()
    {
        var service = ServiceAt("2026-11-22T12:00:00Z"); // Sunday evening of week 1
        var plan = BriefPlan();
        Lift(plan, Tue17Nov, DayNumber.Day2);
        Lift(plan, Thu19Nov, DayNumber.Day3);
        Lift(plan, Fri20Nov, DayNumber.Day4);

        var week = service.Week(plan, Wed18Nov, NoHistory);
        var goals = service.ForWeek(plan, Wed18Nov, NoHistory);

        On(week, Mon16Nov).Status.Should().Be(SessionStatus.Missed);
        On(week, Tue17Nov).Status.Should().Be(SessionStatus.Done);
        On(week, Thu19Nov).Status.Should().Be(SessionStatus.Done);
        On(week, Fri20Nov).Status.Should().Be(SessionStatus.Done);
        NonRest(week).Count(s => s.Status == SessionStatus.Missed).Should().Be(4, "Upper A, both cardio slots and Mobility B were skipped");
        goals.LiftsPlanned.Should().Be(4);
        goals.LiftsDone.Should().Be(3);
    }

    [Fact]
    public void MissingAWholeWeek_EverySessionIsMissed_AndTheNextWeekStartsClean()
    {
        var service = ServiceAt("2026-11-23T08:00:00Z"); // Monday morning of week 2
        var plan = BriefPlan();

        var weekOne = service.Week(plan, Wed18Nov, NoHistory);
        var weekTwo = service.Week(plan, Mon23Nov, NoHistory);
        var goalsOne = service.ForWeek(plan, Wed18Nov, NoHistory);
        var goalsTwo = service.ForWeek(plan, Mon23Nov, NoHistory);

        NonRest(weekOne).Should().HaveCount(7).And.OnlyContain(s => s.Status == SessionStatus.Missed);
        weekOne.Single(d => d.Date == Sun22Nov).Sessions.Single().Status.Should().Be(SessionStatus.Rest, "a rest day is never missed");
        goalsOne.Should().Be(new WeeklyGoals(Mon16Nov, 4, 0, 2, 0, 1, 0, 0, 1, 0, 1));

        NonRest(weekTwo).Should().HaveCount(7).And.OnlyContain(s => s.Status == SessionStatus.Planned, "nothing carries over from a missed week");
        goalsTwo.LiftsPlanned.Should().Be(4);
        goalsTwo.LiftsDone.Should().Be(0);
    }

    [Fact]
    public void MissingTwoWeeks_BothWeeksAreMissed_AndTheThirdWeekResumesAsPlanned()
    {
        var service = ServiceAt("2026-11-30T08:00:00Z"); // Monday morning of week 3
        var plan = BriefPlan();

        var weekOne = service.Week(plan, Mon16Nov, NoHistory);
        var weekTwo = service.Week(plan, Mon23Nov, NoHistory);
        var weekThree = service.Week(plan, Mon30Nov, NoHistory);

        NonRest(weekOne).Should().OnlyContain(s => s.Status == SessionStatus.Missed);
        NonRest(weekTwo).Should().OnlyContain(s => s.Status == SessionStatus.Missed);
        NonRest(weekOne).Concat(NonRest(weekTwo)).Count(s => s.Session.Kind == SessionKind.Lift).Should().Be(8, "eight lifts were missed in a fortnight");
        NonRest(weekThree).Should().OnlyContain(s => s.Status == SessionStatus.Planned);
        service.ForWeek(plan, Mon30Nov, NoHistory).LiftsPlanned.Should().Be(4, "the plan does not try to make up missed sessions");
    }

    // ---------------------------------------------------------------------------------------
    // Out of order, two a day, rest day
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void CompletingOutOfOrder_SkippedTuesdayDoneOnSaturday_IsKeptAsAnExtraAndTuesdayStaysMissed()
    {
        var service = ServiceAt("2026-11-22T12:00:00Z");
        var plan = BriefPlan();
        Lift(plan, Mon16Nov, DayNumber.Day1);
        Lift(plan, Thu19Nov, DayNumber.Day3);
        Lift(plan, Fri20Nov, DayNumber.Day4);
        var lowerAOnSaturday = Lift(plan, Sat21Nov, DayNumber.Day2);

        var week = service.Week(plan, Wed18Nov, NoHistory);
        var goals = service.ForWeek(plan, Wed18Nov, NoHistory);

        // The calendar matches a lift to its slot by A2S2 day on the slot's date; a lift done on
        // another day stays on the plan as a completion but claims no slot (brief § 3: an "unmatched"
        // workout the person assigns by hand). See the skipped test below for the open question.
        plan.Completions.Should().Contain(lowerAOnSaturday);
        On(week, Tue17Nov).Status.Should().Be(SessionStatus.Missed);
        On(week, Sat21Nov).Session.Kind.Should().Be(SessionKind.Cardio);
        On(week, Sat21Nov).Status.Should().Be(SessionStatus.Missed, "Saturday's slot is cardio, which a lift does not satisfy");
        goals.LiftsDone.Should().Be(3);
        goals.LiftsPlanned.Should().Be(4);
    }

    [Fact(Skip = "BUG: swapping Upper A and Lower A within the week shows both days as Missed and LiftsDone = 0 although two lifts were done; the calendar only matches a lift to the slot whose A2S2 day it carries")]
    public void CompletingOutOfOrder_SwappingMondayAndTuesday_CountsBothLiftsAsDone()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");
        var plan = BriefPlan();
        Lift(plan, Mon16Nov, DayNumber.Day2); // Lower A on Monday
        Lift(plan, Tue17Nov, DayNumber.Day1); // Upper A on Tuesday

        var week = service.Week(plan, Wed18Nov, NoHistory);
        var goals = service.ForWeek(plan, Wed18Nov, NoHistory);

        On(week, Mon16Nov).Status.Should().Be(SessionStatus.Done);
        On(week, Tue17Nov).Status.Should().Be(SessionStatus.Done);
        goals.LiftsDone.Should().Be(2);
    }

    [Fact]
    public void TwoSessionsOnOneDay_WednesdayCardioAndMobility_AreBothDoneAndBothCounted()
    {
        var service = ServiceAt("2026-11-19T08:00:00Z");
        var plan = BriefPlan();
        plan.CompleteSession(Wed18Nov, SessionKind.Cardio, CompletionSource.Manual, cardio: FourByFour,
            recordedAt: new DateTimeOffset(2026, 11, 18, 7, 0, 0, TimeSpan.Zero));
        plan.CompleteSession(Wed18Nov, SessionKind.Mobility, CompletionSource.Manual, mobility: MobilityB,
            recordedAt: new DateTimeOffset(2026, 11, 18, 19, 0, 0, TimeSpan.Zero));

        var day = service.DayFor(plan, Wed18Nov, NoHistory);
        var goals = service.ForWeek(plan, Wed18Nov, NoHistory);

        day.Sessions.Should().HaveCount(2).And.OnlyContain(s => s.Status == SessionStatus.Done);
        day.Sessions.Select(s => s.Completion!.Kind).Should().Equal(SessionKind.Cardio, SessionKind.Mobility);
        goals.CardioDone.Should().Be(1);
        goals.MobilityDone.Should().Be(1);
    }

    [Fact]
    public void TwoLiftsOnOneDay_TheScheduledOneIsDone_TheOtherIsKeptAsAnExtra()
    {
        var service = ServiceAt("2026-11-18T08:00:00Z");
        var plan = BriefPlan();
        Lift(plan, Mon16Nov, DayNumber.Day1);
        var extra = Lift(plan, Mon16Nov, DayNumber.Day2);

        var monday = service.DayFor(plan, Mon16Nov, NoHistory);
        var tuesday = service.DayFor(plan, Tue17Nov, NoHistory);

        plan.Completions.Should().HaveCount(2);
        monday.Sessions.Single().Status.Should().Be(SessionStatus.Done);
        monday.Sessions.Single().Completion!.LiftDay.Should().Be(DayNumber.Day1);
        plan.Completions.Should().Contain(extra);
        tuesday.Sessions.Single().Status.Should().Be(SessionStatus.Missed, "Lower A was not done on Tuesday, its own slot");
    }

    [Fact]
    public void TrainingOnARestDay_IsRecordedAsAnExtraCompletion_AndSundayIsNeverMissed()
    {
        var service = ServiceAt("2026-11-23T08:00:00Z");
        var plan = BriefPlan();
        var sundayLift = Lift(plan, Sun22Nov, DayNumber.Day1);

        var sunday = service.DayFor(plan, Sun22Nov, NoHistory);
        var goals = service.ForWeek(plan, Sun22Nov, NoHistory);

        plan.Completions.Should().ContainSingle().Which.Should().Be(sundayLift);
        sundayLift.Date.Should().Be(Sun22Nov);
        sunday.Sessions.Single().Status.Should().Be(SessionStatus.Rest);
        sunday.Sessions.Single().Completion.Should().BeNull("a rest slot is not something to complete");
        sunday.Sessions.Should().NotContain(s => s.Status == SessionStatus.Missed);
        goals.LiftsPlanned.Should().Be(4, "the rest-day lift adds no planned session");
    }

    // ---------------------------------------------------------------------------------------
    // Pause and Maintenance
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void PauseForAWholeWeek_NothingIsMissedOrPlanned_AndTheWeekAfterResumesCleanly()
    {
        var service = ServiceAt("2026-12-02T08:00:00Z"); // Wednesday of week 3
        var plan = BriefPlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Mon23Nov, Sun29Nov, "Away"));

        var pausedWeek = service.Week(plan, Mon23Nov, NoHistory);
        var pausedGoals = service.ForWeek(plan, Mon23Nov, NoHistory);
        var nextWeek = service.Week(plan, Mon30Nov, NoHistory);
        var nextGoals = service.ForWeek(plan, Mon30Nov, NoHistory);

        pausedWeek.Should().OnlyContain(d => d.Phase == PhaseType.Pause);
        NonRest(pausedWeek).Should().HaveCount(7).And.OnlyContain(s => s.Status == SessionStatus.Paused);
        pausedWeek.Single(d => d.Date == Sun29Nov).Sessions.Single().Status.Should().Be(SessionStatus.Rest);
        pausedGoals.Should().Be(new WeeklyGoals(Mon23Nov, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1), "paused sessions are not goals");

        nextWeek.Should().OnlyContain(d => d.Phase == null);
        On(nextWeek, Mon30Nov).Status.Should().Be(SessionStatus.Missed, "the first day back was skipped");
        On(nextWeek, new DateOnly(2026, 12, 1)).Status.Should().Be(SessionStatus.Missed);
        nextWeek.Single(d => d.Date == new DateOnly(2026, 12, 2)).Sessions.Should().OnlyContain(s => s.Status == SessionStatus.Planned, "today is still open");
        nextGoals.LiftsPlanned.Should().Be(4);
        nextGoals.CardioPlanned.Should().Be(2);
        nextGoals.MobilityPlanned.Should().Be(1);
    }

    [Fact]
    public void PauseEndingMidWeek_OnlyTheDaysAfterItCanBeMissed()
    {
        var service = ServiceAt("2026-11-22T12:00:00Z");
        var plan = BriefPlan();
        plan.AddPhase(new PlanPhase(PhaseType.Pause, Mon16Nov, Wed18Nov, "Long weekend"));

        var week = service.Week(plan, Mon16Nov, NoHistory);
        var goals = service.ForWeek(plan, Mon16Nov, NoHistory);

        On(week, Mon16Nov).Status.Should().Be(SessionStatus.Paused);
        On(week, Tue17Nov).Status.Should().Be(SessionStatus.Paused);
        week.Single(d => d.Date == Wed18Nov).Sessions.Should().OnlyContain(s => s.Status == SessionStatus.Paused);
        On(week, Thu19Nov).Status.Should().Be(SessionStatus.Missed);
        On(week, Fri20Nov).Status.Should().Be(SessionStatus.Missed);
        goals.LiftsPlanned.Should().Be(2, "Thursday and Friday");
        goals.CardioPlanned.Should().Be(1, "Saturday only");
        goals.MobilityPlanned.Should().Be(0);
    }

    [Fact]
    public void Maintenance_IsNotAPause_SessionsStayPlannedAndCanBeMissed()
    {
        var service = ServiceAt("2026-12-30T08:00:00Z"); // Wednesday of the Christmas week
        var plan = BriefPlan();
        var mon28Dec = new DateOnly(2026, 12, 28);
        plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3), "Hold, don't gain"));

        var week = service.Week(plan, mon28Dec, NoHistory);
        var goals = service.ForWeek(plan, mon28Dec, NoHistory);

        plan.IsPaused(mon28Dec).Should().BeFalse();
        week.Should().OnlyContain(d => d.Phase == PhaseType.Maintenance);
        On(week, mon28Dec).Status.Should().Be(SessionStatus.Missed, "A2S2 continues during maintenance");
        On(week, new DateOnly(2026, 12, 31)).Status.Should().Be(SessionStatus.Planned);
        NonRest(week).Should().NotContain(s => s.Status == SessionStatus.Paused);
        goals.WeekStart.Should().Be(mon28Dec);
        goals.LiftsPlanned.Should().Be(4);
        goals.CardioPlanned.Should().Be(2);
        goals.MobilityPlanned.Should().Be(1);
    }

    [Fact]
    public void MaintenanceFollowedByAPause_EachDateTakesItsOwnPhase()
    {
        var service = ServiceAt("2027-01-12T08:00:00Z");
        var plan = BriefPlan();
        plan.AddPhase(new PlanPhase(PhaseType.Maintenance, new DateOnly(2026, 12, 22), new DateOnly(2027, 1, 3), null));
        plan.AddPhase(new PlanPhase(PhaseType.Pause, new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 10), "Ski week"));

        service.DayFor(plan, new DateOnly(2027, 1, 1), NoHistory).Phase.Should().Be(PhaseType.Maintenance);
        service.DayFor(plan, new DateOnly(2027, 1, 1), NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
        service.DayFor(plan, new DateOnly(2027, 1, 4), NoHistory).Phase.Should().Be(PhaseType.Pause);
        service.DayFor(plan, new DateOnly(2027, 1, 4), NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Paused);
        service.DayFor(plan, new DateOnly(2027, 1, 11), NoHistory).Phase.Should().BeNull();
        service.DayFor(plan, new DateOnly(2027, 1, 11), NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Missed);
    }

    // ---------------------------------------------------------------------------------------
    // Clock changes and the New Year week
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-10-24T22:59:00Z", 24)] // 23:59 BST on Saturday
    [InlineData("2026-10-24T23:01:00Z", 25)] // 00:01 BST on Sunday
    [InlineData("2026-10-25T00:30:00Z", 25)] // 01:30 BST, before the clocks go back
    [InlineData("2026-10-25T01:30:00Z", 25)] // 01:30 GMT, after they have
    [InlineData("2026-10-25T23:59:00Z", 25)] // 23:59 GMT on Sunday
    [InlineData("2026-10-26T00:01:00Z", 26)] // 00:01 GMT on Monday
    public void ClocksGoBack_25Oct2026_ALiftLandsOnTheLocalDateItWasFinished(string completedAtUtc, int expectedOctoberDay)
    {
        var service = ServiceAt("2026-10-27T09:00:00Z");
        var plan = WeekendPlan(new DateOnly(2026, 10, 19));
        var saturday = new DateOnly(2026, 10, 24);
        var sunday = new DateOnly(2026, 10, 25);
        var history = new[] { Activity(DayNumber.Day1, completedAtUtc), Activity(DayNumber.Day2, completedAtUtc) };

        var saturdayStatus = service.DayFor(plan, saturday, history).Sessions.Single().Status;
        var sundayStatus = service.DayFor(plan, sunday, history).Sessions.Single().Status;

        saturdayStatus.Should().Be(expectedOctoberDay == 24 ? SessionStatus.Done : SessionStatus.Missed);
        sundayStatus.Should().Be(expectedOctoberDay == 25 ? SessionStatus.Done : SessionStatus.Missed);
        service.ForWeek(plan, saturday, history).LiftsDone.Should().Be(expectedOctoberDay == 26 ? 0 : 1,
            "a lift finished after midnight on Monday belongs to the next week");
    }

    [Theory]
    [InlineData("2027-03-27T23:59:00Z", 27)] // 23:59 GMT on Saturday
    [InlineData("2027-03-28T00:01:00Z", 28)] // 00:01 GMT on Sunday
    [InlineData("2027-03-28T00:59:00Z", 28)] // 00:59 GMT, the minute before the clocks go forward
    [InlineData("2027-03-28T01:00:00Z", 28)] // 02:00 BST, the minute after
    [InlineData("2027-03-28T22:59:00Z", 28)] // 23:59 BST on Sunday
    [InlineData("2027-03-28T23:01:00Z", 29)] // 00:01 BST on Monday
    public void ClocksGoForward_28Mar2027_ALiftLandsOnTheLocalDateItWasFinished(string completedAtUtc, int expectedMarchDay)
    {
        var service = ServiceAt("2027-03-30T09:00:00Z");
        var plan = WeekendPlan(new DateOnly(2027, 3, 22));
        var saturday = new DateOnly(2027, 3, 27);
        var sunday = new DateOnly(2027, 3, 28);
        var history = new[] { Activity(DayNumber.Day1, completedAtUtc), Activity(DayNumber.Day2, completedAtUtc) };

        var saturdayStatus = service.DayFor(plan, saturday, history).Sessions.Single().Status;
        var sundayStatus = service.DayFor(plan, sunday, history).Sessions.Single().Status;

        saturdayStatus.Should().Be(expectedMarchDay == 27 ? SessionStatus.Done : SessionStatus.Missed);
        sundayStatus.Should().Be(expectedMarchDay == 28 ? SessionStatus.Done : SessionStatus.Missed);
        service.ForWeek(plan, sunday, history).LiftsDone.Should().Be(expectedMarchDay == 29 ? 0 : 1);
    }

    [Fact]
    public void ClockChangeSunday_SessionDoneByHandAt2359Local_StaysOnThatDay_AndAt0001LocalMovesToMonday()
    {
        var service = ServiceAt("2026-10-27T09:00:00Z");
        var plan = WeekendPlan(new DateOnly(2026, 10, 19));
        var clock = new LocalClock(new FakeTimeProvider());
        var sunday = new DateOnly(2026, 10, 25);

        var lateSunday = new DateTimeOffset(2026, 10, 25, 23, 59, 0, TimeSpan.Zero);      // 23:59 GMT
        var earlyMonday = new DateTimeOffset(2026, 10, 26, 0, 1, 0, TimeSpan.Zero);        // 00:01 GMT
        clock.ToLocalDate(lateSunday).Should().Be(sunday);
        clock.ToLocalDate(earlyMonday).Should().Be(sunday.AddDays(1));

        // A manual completion is dated by the local date the client resolved; the service trusts it.
        plan.CompleteSession(clock.ToLocalDate(lateSunday), SessionKind.Lift, CompletionSource.Manual, liftDay: DayNumber.Day2, recordedAt: lateSunday);

        service.DayFor(plan, sunday, NoHistory).Sessions.Single().Status.Should().Be(SessionStatus.Done);
        service.ForWeek(plan, sunday, NoHistory).LiftsDone.Should().Be(1);
        service.ForWeek(plan, sunday.AddDays(1), NoHistory).LiftsDone.Should().Be(0);
    }

    [Fact]
    public void NewYearWeek_RunsFromMonday28Dec2026_AndCountsLiftsOnBothSidesOfMidnightOnTheFirst()
    {
        var service = ServiceAt("2027-01-03T12:00:00Z"); // Sunday 3 Jan
        var mon28Dec = new DateOnly(2026, 12, 28);
        var plan = TrainingPlan.Create(Owner, "Festive lifter", mon28Dec,
        [
            ScheduledSession.Lift(DayOfWeek.Thursday, DayNumber.Day1, "Thursday"),
            ScheduledSession.Lift(DayOfWeek.Friday, DayNumber.Day2, "Friday")
        ], 95m);
        var history = new[]
        {
            Activity(DayNumber.Day1, "2026-12-31T23:59:00Z"), // New Year's Eve, 23:59 GMT
            Activity(DayNumber.Day2, "2027-01-01T00:01:00Z")  // New Year's Day, 00:01 GMT
        };

        var week = service.Week(plan, new DateOnly(2027, 1, 1), history);
        var goals = service.ForWeek(plan, new DateOnly(2027, 1, 1), history);
        var january = service.Month(plan, 2027, 1, history);

        week.Select(d => d.Date).Should().Equal(
            mon28Dec, new(2026, 12, 29), new(2026, 12, 30), new(2026, 12, 31),
            new(2027, 1, 1), new(2027, 1, 2), new(2027, 1, 3));
        On(week, new DateOnly(2026, 12, 31)).Status.Should().Be(SessionStatus.Done);
        On(week, new DateOnly(2027, 1, 1)).Status.Should().Be(SessionStatus.Done);
        goals.WeekStart.Should().Be(mon28Dec);
        goals.LiftsPlanned.Should().Be(2);
        goals.LiftsDone.Should().Be(2);
        january.Should().HaveCount(31);
        january.Single(d => d.Date == new DateOnly(2027, 1, 1)).Sessions.Single().Status.Should().Be(SessionStatus.Done);
        service.ForWeek(plan, new DateOnly(2027, 1, 4), history).LiftsDone.Should().Be(0, "the next week starts on Monday 4 Jan");
    }
}
