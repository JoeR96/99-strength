using A2S.Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class LocalClockTests
{
    private static LocalClock ClockAt(string utcIso)
    {
        var provider = new FakeTimeProvider(DateTimeOffset.Parse(utcIso, null, System.Globalization.DateTimeStyles.AssumeUniversal));
        return new LocalClock(provider);
    }

    [Theory]
    // Night the clocks go back (25 Oct 2026, 01:00 UTC): 23:30Z is already 00:30 BST on the 25th
    [InlineData("2026-10-24T23:30:00Z", "2026-10-25")]
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25")]
    // Night the clocks go forward (28 Mar 2027, 01:00 UTC): GMT until then, so 23:30Z is still the 27th
    [InlineData("2027-03-27T23:30:00Z", "2027-03-27")]
    [InlineData("2027-03-28T00:30:00Z", "2027-03-28")]
    // Winter: UTC == local
    [InlineData("2026-12-31T23:59:00Z", "2026-12-31")]
    // Summer: 23:30Z is already tomorrow locally
    [InlineData("2026-06-30T23:30:00Z", "2026-07-01")]
    // Brief § 6: a session completed at 23:59 vs 00:01 local, either side of each clock change.
    [InlineData("2026-10-24T22:59:00Z", "2026-10-24")] // 23:59 BST, Saturday before the clocks go back
    [InlineData("2026-10-24T23:01:00Z", "2026-10-25")] // 00:01 BST, Sunday 25 Oct
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25")] // 01:30 BST, the first pass through 1 am
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25")] // 01:30 GMT, the second pass through 1 am
    [InlineData("2026-10-25T23:59:00Z", "2026-10-25")] // 23:59 GMT Sunday
    [InlineData("2026-10-26T00:01:00Z", "2026-10-26")] // 00:01 GMT Monday: a new week
    [InlineData("2027-03-27T23:59:00Z", "2027-03-27")] // 23:59 GMT, Saturday before the clocks go forward
    [InlineData("2027-03-28T00:01:00Z", "2027-03-28")] // 00:01 GMT, Sunday 28 Mar
    [InlineData("2027-03-28T00:59:00Z", "2027-03-28")] // 00:59 GMT, the minute before the jump
    [InlineData("2027-03-28T01:00:00Z", "2027-03-28")] // 02:00 BST, the minute after it
    [InlineData("2027-03-28T22:59:00Z", "2027-03-28")] // 23:59 BST Sunday
    [InlineData("2027-03-28T23:01:00Z", "2027-03-29")] // 00:01 BST Monday: a new week
    [InlineData("2026-12-31T23:59:00Z", "2026-12-31")] // New Year's Eve, 23:59 GMT
    [InlineData("2027-01-01T00:01:00Z", "2027-01-01")] // New Year's Day, 00:01 GMT
    public void ToLocalDate_UsesEuropeLondon(string utcIso, string expectedLocalDate)
    {
        var clock = ClockAt("2026-01-01T12:00:00Z");
        var utc = DateTime.Parse(utcIso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        clock.ToLocalDate(utc).Should().Be(DateOnly.Parse(expectedLocalDate));
    }

    [Fact]
    public void Today_FollowsTheTimeProvider()
    {
        ClockAt("2026-10-24T23:30:00Z").Today().Should().Be(new DateOnly(2026, 10, 25));
        ClockAt("2026-12-31T23:59:00Z").Today().Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void ToLocalDate_TreatsUnspecifiedKindAsUtc()
    {
        var clock = ClockAt("2026-01-01T12:00:00Z");
        var unspecified = new DateTime(2026, 6, 30, 23, 30, 0, DateTimeKind.Unspecified);

        clock.ToLocalDate(unspecified).Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void BoundsOf_TheDayTheClocksGoBack_Is25HoursLong()
    {
        var (start, end) = ClockAt("2026-01-01T12:00:00Z").BoundsOf(new DateOnly(2026, 10, 25));

        start.Should().Be(new DateTime(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc));
        (end - start).Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void BoundsOf_TheDayTheClocksGoForward_Is23HoursLong()
    {
        var (start, end) = ClockAt("2026-01-01T12:00:00Z").BoundsOf(new DateOnly(2027, 3, 28));

        start.Should().Be(new DateTime(2027, 3, 28, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2027, 3, 28, 23, 0, 0, DateTimeKind.Utc));
        (end - start).Should().Be(TimeSpan.FromHours(23));
    }

    [Fact]
    public void BoundsOf_AnOrdinaryDay_RoundTripsThroughToLocalDate()
    {
        var clock = ClockAt("2026-01-01T12:00:00Z");
        var day = new DateOnly(2026, 11, 9);
        var (start, end) = clock.BoundsOf(day);

        clock.ToLocalDate(start).Should().Be(day);
        clock.ToLocalDate(end.AddTicks(-1)).Should().Be(day);
        clock.ToLocalDate(end).Should().Be(day.AddDays(1));
    }
}

public class LocalDatesTests
{
    [Theory]
    [InlineData("2026-11-09", "2026-11-09")] // Monday
    [InlineData("2026-11-11", "2026-11-09")] // Wednesday
    [InlineData("2026-11-15", "2026-11-09")] // Sunday
    [InlineData("2026-12-28", "2026-12-28")] // the Monday that starts the New Year week
    [InlineData("2026-12-31", "2026-12-28")] // New Year's Eve
    [InlineData("2027-01-01", "2026-12-28")] // New Year's Day is a Friday: its week began on 28 Dec 2026
    [InlineData("2027-01-03", "2026-12-28")] // the Sunday that ends that week
    [InlineData("2027-01-04", "2027-01-04")] // the first full week of 2027
    [InlineData("2026-10-25", "2026-10-19")] // the Sunday the clocks go back
    [InlineData("2027-03-28", "2027-03-22")] // the Sunday the clocks go forward
    public void StartOfWeek_IsMonday(string date, string expected)
    {
        LocalDates.StartOfWeek(DateOnly.Parse(date)).Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void TheWeekContainingNewYearsDay2027_RunsFromMonday28DecToSunday3Jan()
    {
        var newYearsDay = new DateOnly(2027, 1, 1);

        LocalDates.StartOfWeek(newYearsDay).Should().Be(new DateOnly(2026, 12, 28));
        LocalDates.EndOfWeek(newYearsDay).Should().Be(new DateOnly(2027, 1, 3));
        LocalDates.StartOfWeek(new DateOnly(2026, 12, 31)).Should().Be(LocalDates.StartOfWeek(newYearsDay),
            "New Year's Eve and New Year's Day fall in the same training week");
    }

    [Fact]
    public void EndOfWeek_IsSunday()
    {
        LocalDates.EndOfWeek(new DateOnly(2026, 11, 11)).Should().Be(new DateOnly(2026, 11, 15));
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-09")] // a Monday starts today
    [InlineData("2026-11-11", "2026-11-16")] // Wednesday -> next Monday
    [InlineData("2026-11-15", "2026-11-16")] // Sunday -> tomorrow
    public void ComingMonday_SnapsForwardNeverBack(string date, string expected)
    {
        LocalDates.ComingMonday(DateOnly.Parse(date)).Should().Be(DateOnly.Parse(expected));
    }
}
