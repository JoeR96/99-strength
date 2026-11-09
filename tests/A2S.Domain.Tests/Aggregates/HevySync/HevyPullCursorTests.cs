using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.HevySync;

public class HevyPullCursorTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Since = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_SetsBothTimes_AndTheIdIsTheUser()
    {
        var cursor = HevyPullCursor.Start(Joe, Since, Now);

        cursor.Id.Should().Be(Joe);
        cursor.UserId.Should().Be(Joe);
        cursor.LastEventAtUtc.Should().Be(Since);
        cursor.LastPulledAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Advance_MovesForwardOnly_AndAlwaysStampsThePull()
    {
        var cursor = HevyPullCursor.Start(Joe, Since, Now);

        cursor.Advance(Now.AddMinutes(-5), Now.AddMinutes(1));
        cursor.LastEventAtUtc.Should().Be(Now.AddMinutes(-5));

        cursor.Advance(Since, Now.AddMinutes(2));
        cursor.LastEventAtUtc.Should().Be(Now.AddMinutes(-5), "older events never move the cursor back");
        cursor.LastPulledAtUtc.Should().Be(Now.AddMinutes(2));

        cursor.Advance(null, Now.AddMinutes(3));
        cursor.LastEventAtUtc.Should().Be(Now.AddMinutes(-5));
        cursor.LastPulledAtUtc.Should().Be(Now.AddMinutes(3));
    }

    [Fact]
    public void Rewind_GoesBackForAForcedPull()
    {
        var cursor = HevyPullCursor.Start(Joe, Since, Now);
        cursor.Advance(Now, Now);

        cursor.Rewind(Since);

        cursor.LastEventAtUtc.Should().Be(Since);
    }
}
