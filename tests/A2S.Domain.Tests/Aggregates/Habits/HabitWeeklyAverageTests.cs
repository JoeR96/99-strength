using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates.Habits;

public class HabitWeeklyAverageTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 7, 0, 0, TimeSpan.Zero);

    // Friday 20 November 2026; the week runs Monday 16 – Sunday 22.
    private static readonly DateOnly Friday = new(2026, 11, 20);

    private static HabitDefinition Steps(HabitTargetMode mode = HabitTargetMode.WeeklyAverage)
    {
        var habit = HabitDefinition.Create(Joe, "Steps", HabitType.Numeric, 8000, 3000, "steps", 0);
        habit.SetTargetMode(mode);
        return habit;
    }

    [Fact]
    public void WeeklyAverage_AveragesTheLoggedDaysFromMonday()
    {
        var steps = Steps();
        HabitLog[] logs =
        [
            HabitLog.Create(steps.Id, Joe, new DateOnly(2026, 11, 15), 20000, T0), // Sunday before: not this week
            HabitLog.Create(steps.Id, Joe, new DateOnly(2026, 11, 16), 6000, T0),
            HabitLog.Create(steps.Id, Joe, new DateOnly(2026, 11, 17), 9000, T0),
            HabitLog.Create(steps.Id, Joe, new DateOnly(2026, 11, 19), 10001, T0)
        ];

        var week = HabitRules.WeeklyAverage(steps, logs, Friday);

        week.WeekStart.Should().Be(new DateOnly(2026, 11, 16));
        week.DaysLogged.Should().Be(3);
        week.Average.Should().Be(8334);
        week.OnTarget.Should().BeTrue();
    }

    [Fact]
    public void WeeklyAverage_UnderTarget_IsNotOnTarget()
    {
        var steps = Steps();
        HabitLog[] logs = [HabitLog.Create(steps.Id, Joe, Friday, 7999, T0)];

        HabitRules.WeeklyAverage(steps, logs, Friday).OnTarget.Should().BeFalse();
    }

    [Fact]
    public void WeeklyAverage_NothingLogged_IsZero()
    {
        var week = HabitRules.WeeklyAverage(Steps(), [], Friday);

        week.Average.Should().Be(0);
        week.DaysLogged.Should().Be(0);
        week.OnTarget.Should().BeFalse();
    }

    [Fact]
    public void TargetMode_DefaultsToDailyMinimum()
    {
        Steps(HabitTargetMode.DailyMinimum).TargetMode.Should().Be(HabitTargetMode.DailyMinimum);
        HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0).TargetMode.Should().Be(HabitTargetMode.DailyMinimum);
    }

    [Fact]
    public void WeeklyAverage_OnlyForNumericHabits()
    {
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);

        var act = () => water.SetTargetMode(HabitTargetMode.WeeklyAverage);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
