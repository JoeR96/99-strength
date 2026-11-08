using A2S.Domain.Aggregates.Cardio;
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
/// Weekly zone 4–5 minutes: logged per cardio session, summed Mon–Sun (rest-day extras included),
/// shown against the plan's 15–30 min band.
/// </summary>
public class HighIntensityGoalTests
{
    private static readonly UserId Owner = new("user_zone_0001");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly IReadOnlyCollection<WorkoutActivity> NoHistory = Array.Empty<WorkoutActivity>();

    private static PlanCalendarService ServiceAt(string utc) =>
        new(new LocalClock(new FakeTimeProvider(DateTimeOffset.Parse(utc, null, System.Globalization.DateTimeStyles.AssumeUniversal))));

    private static TrainingPlan Plan() => TrainingPlan.Create(Owner, "Cut", Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A"),
            ScheduledSession.Cardio(DayOfWeek.Wednesday, null, "Norwegian 4x4"),
            ScheduledSession.Rest(DayOfWeek.Sunday),
        ], 95m);

    [Fact]
    public void Target_DefaultsTo15To30()
    {
        Plan().HighIntensityTarget.Should().Be(new HighIntensityTarget(15, 30));
        HighIntensityTarget.Default.Should().Be(new HighIntensityTarget(15, 30));
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(31, 30)]
    [InlineData(0, 601)]
    public void Target_RejectsImpossibleBands(int min, int max)
    {
        var act = () => new HighIntensityTarget(min, max);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Plan_CanChangeItsTarget()
    {
        var plan = Plan();

        plan.SetHighIntensityTarget(new HighIntensityTarget(20, 40));

        plan.HighIntensityTarget.Should().Be(new HighIntensityTarget(20, 40));
    }

    [Fact]
    public void NorwegianFourByFour_DefaultsTo16HighIntensityMinutes_OthersToNone()
    {
        CardioProtocol.NorwegianFourByFour(Owner).DefaultHighIntensityMinutes.Should().Be(16);
        CardioProtocol.Liss(Owner, 35, null).DefaultHighIntensityMinutes.Should().BeNull();
        CardioProtocol.CouchToFiveK(Owner, 1).DefaultHighIntensityMinutes.Should().BeNull("C25K runs are not zone 4–5 work; the watch decides");
    }

    [Fact]
    public void CardioLog_HighIntensityMinutes_CannotExceedTheSessionOrBeNegative()
    {
        var tooMany = () => new CardioLog(1200, null, null, null, null, highIntensityMinutes: 21);
        var negative = () => new CardioLog(1200, null, null, null, null, highIntensityMinutes: -1);

        tooMany.Should().Throw<BusinessRuleViolationException>();
        negative.Should().Throw<BusinessRuleViolationException>();
        new CardioLog(1200, null, null, null, null, highIntensityMinutes: 20).HighIntensityMinutes.Should().Be(20);
        new CardioLog(1200, null, null, null, null).HighIntensityMinutes.Should().BeNull();
    }

    [Fact]
    public void ForWeek_SumsHighIntensityMinutes_IncludingARestDayExtra_AgainstTheBand()
    {
        var service = ServiceAt("2026-11-19T12:00:00Z");
        var plan = Plan();
        plan.CompleteSession(new DateOnly(2026, 11, 18), SessionKind.Cardio, CompletionSource.Manual,
            log: new CardioLog(2400, 165, 8, null, null, highIntensityMinutes: 16),
            recordedAt: new DateTimeOffset(2026, 11, 18, 19, 0, 0, TimeSpan.Zero));
        plan.CompleteSession(new DateOnly(2026, 11, 22), SessionKind.Cardio, CompletionSource.Manual,
            log: new CardioLog(1800, 150, 6, 4000m, "five-a-side", highIntensityMinutes: 9),
            recordedAt: new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero));
        // last week's session must not count
        plan.CompleteSession(new DateOnly(2026, 11, 15), SessionKind.Cardio, CompletionSource.Manual,
            log: new CardioLog(2400, null, null, null, null, highIntensityMinutes: 16),
            recordedAt: new DateTimeOffset(2026, 11, 15, 19, 0, 0, TimeSpan.Zero));

        var goals = service.ForWeek(plan, new DateOnly(2026, 11, 19), NoHistory);

        goals.HighIntensityMinutesDone.Should().Be(25);
        goals.HighIntensityMinutesMin.Should().Be(15);
        goals.HighIntensityMinutesMax.Should().Be(30);
    }

    [Fact]
    public void ForWeek_WithNoLoggedZoneMinutes_ReportsZeroNotNull()
    {
        var service = ServiceAt("2026-11-19T12:00:00Z");
        var plan = Plan();
        plan.CompleteSession(new DateOnly(2026, 11, 18), SessionKind.Cardio, CompletionSource.Manual,
            recordedAt: new DateTimeOffset(2026, 11, 18, 19, 0, 0, TimeSpan.Zero));

        service.ForWeek(plan, new DateOnly(2026, 11, 19), NoHistory).HighIntensityMinutesDone.Should().Be(0);
    }
}
