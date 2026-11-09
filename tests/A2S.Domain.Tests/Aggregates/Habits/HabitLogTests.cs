using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Habits;

public class HabitLogTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly HabitId Water = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_RecordsValueAndTimestamp()
    {
        var log = HabitLog.Create(Water, Joe, Day, 2, T0);

        log.HabitId.Should().Be(Water);
        log.UserId.Should().Be(Joe);
        log.Date.Should().Be(Day);
        log.Value.Should().Be(2);
        log.UpdatedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void Create_NegativeValue_Throws()
    {
        var act = () => HabitLog.Create(Water, Joe, Day, -1, T0);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Decrement_FloorsAtZero()
    {
        var log = HabitLog.Create(Water, Joe, Day, 0, T0);

        log.Decrement(1, T0.AddMinutes(1));

        log.Value.Should().Be(0);
        log.UpdatedAtUtc.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void Decrement_ByMoreThanTheValue_FloorsAtZero_AndStaysThere()
    {
        var log = HabitLog.Create(Water, Joe, Day, 1, T0);

        log.Decrement(3, T0.AddMinutes(1));
        log.Decrement(T0.AddMinutes(2));

        log.Value.Should().Be(0);
        log.UpdatedAtUtc.Should().Be(T0.AddMinutes(2), "a floored tap still counts as an edit");
    }

    [Fact]
    public void Increment_WellPastTarget_IsReportedAsIs()
    {
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        var log = HabitLog.Create(water.Id, Joe, Day, 6, T0);

        for (var i = 1; i <= 4; i++)
        {
            log.Increment(T0.AddMinutes(i));
        }

        log.Value.Should().Be(10, "10/6 is shown, never clamped");
        HabitRules.MetTarget(water, log).Should().BeTrue();
    }

    [Fact]
    public void Increment_PastTarget_IsKept()
    {
        var log = HabitLog.Create(Water, Joe, Day, 6, T0);

        log.Increment(1, T0.AddMinutes(1));

        log.Value.Should().Be(7);
    }

    [Fact]
    public void SetValue_RejectsNegative()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        var act = () => log.SetValue(-0.5m, T0.AddMinutes(1));

        act.Should().Throw<BusinessRuleViolationException>();
        log.Value.Should().Be(3);
    }

    [Fact]
    public void Toggle_FlipsBetweenZeroAndOne()
    {
        var log = HabitLog.Create(Water, Joe, Day, 0, T0);

        log.Toggle(T0.AddMinutes(1));
        var afterFirst = log.Value;
        log.Toggle(T0.AddMinutes(2));

        afterFirst.Should().Be(1);
        log.Value.Should().Be(0);
    }

    [Fact]
    public void Apply_NewerTimestamp_Wins()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        var applied = log.Apply(5, T0.AddSeconds(1));

        applied.Should().BeTrue();
        log.Value.Should().Be(5);
        log.UpdatedAtUtc.Should().Be(T0.AddSeconds(1));
    }

    [Fact]
    public void Apply_OlderTimestamp_IsIgnored()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        var applied = log.Apply(5, T0.AddSeconds(-1));

        applied.Should().BeFalse();
        log.Value.Should().Be(3);
        log.UpdatedAtUtc.Should().Be(T0);
    }

    [Fact]
    public void Apply_EqualTimestamp_Wins()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        log.Apply(4, T0).Should().BeTrue();
        log.Value.Should().Be(4);
    }

    // --- The +n / -n tap: the first tap of a day behaves exactly like a later one ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(-1, 0)]
    [InlineData(-3, 0)]
    public void CreateFromTap_Counter_StartsAtZeroAndTakesTheWholeDelta(int delta, int expected)
    {
        var log = HabitLog.CreateFromTap(Water, Joe, Day, HabitType.Counter, delta, T0);

        log.Value.Should().Be(expected);
        log.UpdatedAtUtc.Should().Be(T0);
    }

    [Theory]
    [InlineData(HabitType.Counter, 1)]
    [InlineData(HabitType.Counter, 3)]
    [InlineData(HabitType.Counter, -2)]
    [InlineData(HabitType.Numeric, 3)]
    [InlineData(HabitType.Checkbox, 1)]
    [InlineData(HabitType.Checkbox, 3)]
    [InlineData(HabitType.Checkbox, -1)]
    public void CreateFromTap_EndsWhereTheSameTapOnAnEmptyLogEnds(HabitType type, int delta)
    {
        var first = HabitLog.CreateFromTap(Water, Joe, Day, type, delta, T0);
        var later = HabitLog.Create(Water, Joe, Day, 0, T0.AddMinutes(-1));
        later.ApplyTap(type, delta, T0);

        first.Value.Should().Be(later.Value);
        first.UpdatedAtUtc.Should().Be(later.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyTap_Counter_AddsTheDelta_PastTheTarget_AndNeverBelowZero()
    {
        var log = HabitLog.Create(Water, Joe, Day, 5, T0);

        log.ApplyTap(HabitType.Counter, 3, T0.AddMinutes(1));
        var up = log.Value;
        log.ApplyTap(HabitType.Counter, -1, T0.AddMinutes(2));
        var down = log.Value;
        log.ApplyTap(HabitType.Counter, -20, T0.AddMinutes(3));

        up.Should().Be(8);
        down.Should().Be(7);
        log.Value.Should().Be(0);
        log.UpdatedAtUtc.Should().Be(T0.AddMinutes(3));
    }

    [Fact]
    public void ApplyTap_Checkbox_PlusTicks_MinusUnticks_NeverAboveOne()
    {
        var log = HabitLog.Create(Water, Joe, Day, 1, T0);

        log.ApplyTap(HabitType.Checkbox, 1, T0.AddMinutes(1));
        var tickedTwice = log.Value;
        log.ApplyTap(HabitType.Checkbox, -1, T0.AddMinutes(2));

        tickedTwice.Should().Be(1);
        log.Value.Should().Be(0);
    }

    [Fact]
    public void ApplyTap_OlderThanTheStoredWrite_StillCounts_ButTheTimestampNeverMovesBack()
    {
        var log = HabitLog.Create(Water, Joe, Day, 2, T0);

        log.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(-10));

        log.Value.Should().Be(3, "counter taps from two devices must both count");
        log.UpdatedAtUtc.Should().Be(T0, "an older tap must not let an even older full-value write back in");
    }

    // --- A tap delivered twice (the response was lost and the client sent it again) counts once ---

    [Fact]
    public void ApplyTap_WithTheTimestampOfTheStoredWrite_IsARedelivery_AndChangesNothing()
    {
        var log = HabitLog.Create(Water, Joe, Day, 2, T0);
        var tappedAt = T0.AddMinutes(1);

        var first = log.ApplyTap(HabitType.Counter, 1, tappedAt);
        var again = log.ApplyTap(HabitType.Counter, 1, tappedAt);

        first.Should().BeTrue();
        again.Should().BeFalse();
        log.Value.Should().Be(3);
        log.UpdatedAtUtc.Should().Be(tappedAt);
    }

    [Fact]
    public void ApplyTap_RedeliveryOfTheTapThatCreatedTheLog_ChangesNothing()
    {
        var log = HabitLog.CreateFromTap(Water, Joe, Day, HabitType.Counter, 1, T0);

        log.ApplyTap(HabitType.Counter, 1, T0).Should().BeFalse();

        log.Value.Should().Be(1);
    }

    [Fact]
    public void ApplyTap_RedeliveredDecrement_TakesOneOffOnce()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        log.ApplyTap(HabitType.Counter, -1, T0.AddMinutes(1));
        log.ApplyTap(HabitType.Counter, -1, T0.AddMinutes(1)).Should().BeFalse();

        log.Value.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]       // the next millisecond a browser can stamp
    [InlineData(-1)]
    [InlineData(60_000)]
    [InlineData(-60_000)]
    public void ApplyTap_WithAnyOtherTimestamp_OlderOrNewer_StillCounts(int millisecondsFromTheStoredWrite)
    {
        var log = HabitLog.Create(Water, Joe, Day, 2, T0);

        var applied = log.ApplyTap(HabitType.Counter, 1, T0.AddMilliseconds(millisecondsFromTheStoredWrite));

        applied.Should().BeTrue("taps from two devices must both count");
        log.Value.Should().Be(3);
    }

    [Fact]
    public void IsRedeliveryOf_ComparesTheInstant_WhateverOffsetTheDeviceSends()
    {
        var log = HabitLog.Create(Water, Joe, Day, 2, T0); // 07:00Z

        log.IsRedeliveryOf(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.FromHours(1))).Should().BeTrue();
        log.IsRedeliveryOf(new DateTimeOffset(2026, 11, 9, 7, 0, 0, TimeSpan.FromHours(1))).Should().BeFalse();
    }

    [Fact]
    public void IsRedeliveryOf_ComparesWholeMicroseconds_BecauseThatIsAllTheDatabaseKeeps()
    {
        // A .NET client can stamp 100 ns ticks; Postgres keeps microseconds and drops the last digit.
        var sent = new DateTimeOffset(T0.Ticks + 1_234_567, TimeSpan.Zero);       // 07:00:00.1234567
        var stored = new DateTimeOffset(T0.Ticks + 1_234_560, TimeSpan.Zero);     // 07:00:00.123456
        var log = HabitLog.Create(Water, Joe, Day, 2, stored);

        log.IsRedeliveryOf(sent).Should().BeTrue();
        log.IsRedeliveryOf(sent.AddTicks(10)).Should().BeFalse("the next microsecond is a different tap");
        log.IsRedeliveryOf(stored.AddTicks(-1)).Should().BeFalse("the previous microsecond is a different tap");
    }

    [Fact]
    public void ApplyTap_WithoutATimestamp_IsNeverARedelivery()
    {
        // A client that sends no clientTimestamp arrives as the zero instant; such taps always count.
        var log = HabitLog.CreateFromTap(Water, Joe, Day, HabitType.Counter, 1, default);

        log.IsRedeliveryOf(default).Should().BeFalse();
        log.ApplyTap(HabitType.Counter, 1, default).Should().BeTrue();
        log.ApplyTap(HabitType.Counter, 1, default).Should().BeTrue();

        log.Value.Should().Be(3);
        log.UpdatedAtUtc.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void ApplyTap_KnownGap_OnlyTheLatestWriteIsRemembered_SoARedeliveryBehindAnotherWriteCountsAgain()
    {
        // One stored timestamp can recognise one write. Once another write has landed after the tap,
        // or the tap was older than the stored write to begin with, its re-delivery looks like a new tap.
        var interleaved = HabitLog.Create(Water, Joe, Day, 0, T0);
        interleaved.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(1));   // phone, response lost
        interleaved.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(2));   // widget
        interleaved.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(1)).Should().BeTrue(); // phone retries

        var late = HabitLog.Create(Water, Joe, Day, 0, T0);
        late.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(-5));          // made offline before T0, response lost
        late.ApplyTap(HabitType.Counter, 1, T0.AddMinutes(-5)).Should().BeTrue();

        interleaved.Value.Should().Be(3);
        late.Value.Should().Be(2);
    }

    // --- Brief § 6: conflicting edits from two devices ---

    [Fact]
    public void TwoDevices_PhoneOfflineThenTabletOnline_TheLaterClientTimestampWins_WhicheverSyncsFirst()
    {
        var phoneEdit = (Value: 5m, At: T0.AddMinutes(10));  // made offline on the phone, synced late
        var tabletEdit = (Value: 4m, At: T0.AddMinutes(5));  // made online on the tablet, synced at once

        var tabletFirst = HabitLog.Create(Water, Joe, Day, 3, T0);
        tabletFirst.Apply(tabletEdit.Value, tabletEdit.At).Should().BeTrue();
        tabletFirst.Apply(phoneEdit.Value, phoneEdit.At).Should().BeTrue();

        var phoneFirst = HabitLog.Create(Water, Joe, Day, 3, T0);
        phoneFirst.Apply(phoneEdit.Value, phoneEdit.At).Should().BeTrue();
        phoneFirst.Apply(tabletEdit.Value, tabletEdit.At).Should().BeFalse("the tablet's edit is older than what is stored");

        tabletFirst.Value.Should().Be(5);
        phoneFirst.Value.Should().Be(5);
        tabletFirst.UpdatedAtUtc.Should().Be(phoneEdit.At);
        phoneFirst.UpdatedAtUtc.Should().Be(phoneEdit.At);
    }

    [Fact]
    public void TwoDevices_SameClientTimestamp_TheSecondArrivalApplies()
    {
        var at = T0.AddMinutes(10);
        var log = HabitLog.Create(Water, Joe, Day, 3, T0);

        log.Apply(5, at).Should().BeTrue();
        log.Apply(4, at).Should().BeTrue("an equal timestamp is not older");

        log.Value.Should().Be(4);
    }

    [Fact]
    public void Apply_ComparesClientTimestampsInUtc_WhateverOffsetTheDeviceSends()
    {
        var log = HabitLog.Create(Water, Joe, Day, 3, T0); // 07:00Z

        // 07:30 at +01:00 is 06:30Z, so it is older than the stored 07:00Z edit.
        log.Apply(5, new DateTimeOffset(2026, 11, 9, 7, 30, 0, TimeSpan.FromHours(1))).Should().BeFalse();
        // 08:30 at +01:00 is 07:30Z: newer.
        log.Apply(5, new DateTimeOffset(2026, 11, 9, 8, 30, 0, TimeSpan.FromHours(1))).Should().BeTrue();

        log.Value.Should().Be(5);
        log.UpdatedAtUtc.Should().Be(T0.AddMinutes(30));
        log.UpdatedAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void HabitRules_OverTarget_MeetsMinimumAndTarget()
    {
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        var seven = HabitLog.Create(water.Id, Joe, Day, 7, T0);
        var four = HabitLog.Create(water.Id, Joe, Day, 4, T0);
        var three = HabitLog.Create(water.Id, Joe, Day, 3, T0);

        HabitRules.MetMinimum(water, seven).Should().BeTrue();
        HabitRules.MetTarget(water, seven).Should().BeTrue();
        HabitRules.MetMinimum(water, four).Should().BeTrue();
        HabitRules.MetTarget(water, four).Should().BeFalse();
        HabitRules.MetMinimum(water, three).Should().BeFalse();
        HabitRules.MetMinimum(water, null).Should().BeFalse();
        HabitRules.MetTarget(water, null).Should().BeFalse();
    }
}
