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
    public void StartOfWeek_IsMonday(string date, string expected)
    {
        LocalDates.StartOfWeek(DateOnly.Parse(date)).Should().Be(DateOnly.Parse(expected));
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
