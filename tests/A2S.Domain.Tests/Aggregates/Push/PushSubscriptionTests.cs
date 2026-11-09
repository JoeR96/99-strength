using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Push;

public class PushSubscriptionTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsAndDefaultsTheNudgeTimeTo1730()
    {
        var sub = PushSubscription.Create(Joe, " https://push.example.com/abc ", " p256 ", " auth ", "  ", At);

        sub.Endpoint.Should().Be("https://push.example.com/abc");
        sub.P256dh.Should().Be("p256");
        sub.Auth.Should().Be("auth");
        sub.UserAgent.Should().BeNull();
        sub.SessionStartTime.Should().Be(new TimeOnly(17, 30));
        sub.CreatedAtUtc.Should().Be(At);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://insecure.example.com/x")]
    [InlineData("not a url")]
    public void Create_RejectsBadEndpoints(string endpoint)
    {
        var act = () => PushSubscription.Create(Joe, endpoint, "p", "a", null, At);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Create_RejectsMissingKeys()
    {
        var noP256 = () => PushSubscription.Create(Joe, "https://push.example.com/abc", "", "a", null, At);
        var noAuth = () => PushSubscription.Create(Joe, "https://push.example.com/abc", "p", " ", null, At);
        noP256.Should().Throw<BusinessRuleViolationException>();
        noAuth.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void RefreshKeys_And_SetSessionStartTime_ChangeOnlyThoseFields()
    {
        var sub = PushSubscription.Create(Joe, "https://push.example.com/abc", "p1", "a1", "Safari", At, new TimeOnly(6, 0));

        sub.RefreshKeys("p2", "a2", "Chrome");
        sub.SetSessionStartTime(new TimeOnly(18, 15));

        sub.P256dh.Should().Be("p2");
        sub.Auth.Should().Be("a2");
        sub.UserAgent.Should().Be("Chrome");
        sub.SessionStartTime.Should().Be(new TimeOnly(18, 15));
        sub.Endpoint.Should().Be("https://push.example.com/abc");
    }

    [Fact]
    public void NudgeLog_StoresUtc()
    {
        var log = PushNudgeLog.Create(Joe, new DateOnly(2026, 11, 9), PushNudgeKind.HabitsUnderMinimum, new DateTimeOffset(2026, 11, 9, 21, 0, 0, TimeSpan.FromHours(1)));
        log.SentAtUtc.Should().Be(new DateTimeOffset(2026, 11, 9, 20, 0, 0, TimeSpan.Zero));
        log.Kind.Should().Be(PushNudgeKind.HabitsUnderMinimum);
    }
}
