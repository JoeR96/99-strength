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
