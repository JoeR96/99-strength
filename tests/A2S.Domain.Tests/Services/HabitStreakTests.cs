using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Services;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Services;

/// <summary>
/// "Never miss twice": a day counts when every active habit reached at least its minimum; one missed
/// day is forgiven, two in a row reset; today not yet logged never breaks the streak.
/// </summary>
public class HabitStreakTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset At = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly HabitDefinition Water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
    private static readonly HabitDefinition Walks = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 1);

    private static readonly DateOnly Nov30 = new(2026, 11, 30);
    private static readonly DateOnly Dec1 = new(2026, 12, 1);
    private static readonly DateOnly Dec2 = new(2026, 12, 2);
    private static readonly DateOnly Dec3 = new(2026, 12, 3);

    private readonly HabitStreakService _service = new();

    private static HabitLog Log(HabitDefinition habit, DateOnly date, decimal value) =>
        HabitLog.Create(habit.Id, Joe, date, value, At);

    /// <summary>Both habits at target on each of the given days.</summary>
    private static List<HabitLog> FullDays(params DateOnly[] days) =>
        days.SelectMany(d => new[] { Log(Water, d, 6), Log(Walks, d, 3) }).ToList();

    [Fact]
    public void OneMissAcrossMonthBoundary_StreakContinues()
    {
        var logs = FullDays(Nov30, Dec2);

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(2);
        streak.GraceUsed.Should().BeTrue();
        streak.TodayCounted.Should().BeTrue();
        streak.LastCountedDay.Should().Be(Dec2);
    }

    [Fact]
    public void TwoMissesInARow_StreakResets()
    {
        var logs = FullDays(Nov30, Dec3);

        var streak = _service.Compute([Water, Walks], logs, today: Dec3);

        streak.Current.Should().Be(1);
        streak.GraceUsed.Should().BeFalse();
        streak.Best.Should().Be(1);
    }

    [Fact]
    public void TwoMissesInARow_WithTodayPending_IsZero()
    {
        var logs = FullDays(Nov30);

        var streak = _service.Compute([Water, Walks], logs, today: Dec3);

        streak.Current.Should().Be(0);
        streak.TodayCounted.Should().BeFalse();
        streak.LastCountedDay.Should().Be(Nov30);
    }

    [Fact]
    public void MinimumButNotTarget_DayCounts()
    {
        var logs = new List<HabitLog>
        {
            Log(Water, Dec1, 4), Log(Walks, Dec1, 1),
            Log(Water, Dec2, 6), Log(Walks, Dec2, 3)
        };

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(2);
        streak.GraceUsed.Should().BeFalse();
    }

    [Fact]
    public void OneHabitBelowMinimum_DayDoesNotCount()
    {
        var logs = new List<HabitLog>
        {
            Log(Water, Nov30, 6), Log(Walks, Nov30, 3),
            Log(Water, Dec1, 3), Log(Walks, Dec1, 3),
            Log(Water, Dec2, 6), Log(Walks, Dec2, 3)
        };

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(2);
        streak.GraceUsed.Should().BeTrue();
    }

    [Fact]
    public void OneHabitUnlogged_DayDoesNotCount()
    {
        var logs = new List<HabitLog> { Log(Water, Dec1, 6), Log(Walks, Dec1, 3), Log(Water, Dec2, 6) };

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.TodayCounted.Should().BeFalse();
        streak.Current.Should().Be(1);
        streak.LastCountedDay.Should().Be(Dec1);
    }

    [Fact]
    public void TodayPending_KeepsCurrent()
    {
        var logs = FullDays(Nov30, Dec1);

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(2);
        streak.TodayCounted.Should().BeFalse();
        streak.GraceUsed.Should().BeFalse();
        streak.LastCountedDay.Should().Be(Dec1);
    }

    [Fact]
    public void TodayPending_AfterYesterdayMissed_UsesGrace()
    {
        var logs = FullDays(Nov30);

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(1);
        streak.TodayCounted.Should().BeFalse();
        streak.GraceUsed.Should().BeTrue();
    }

    [Fact]
    public void ArchivedHabit_IsIgnored()
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 2);
        creatine.Archive();
        var logs = FullDays(Dec1, Dec2);

        var streak = _service.Compute([Water, Walks, creatine], logs, today: Dec2);

        streak.Current.Should().Be(2);
    }

    [Fact]
    public void EmptyLogs_AreZeros()
    {
        var streak = _service.Compute([Water, Walks], [], today: Dec2);

        streak.Should().Be(new HabitStreak(0, 0, null, false, false));
    }

    [Fact]
    public void NoActiveHabits_AreZeros()
    {
        var streak = _service.Compute([], FullDays(Dec1, Dec2), today: Dec2);

        streak.Should().Be(new HabitStreak(0, 0, null, false, false));
    }

    [Fact]
    public void Best_IsTheLongestRunInHistory()
    {
        var nov = Enumerable.Range(20, 6).Select(d => new DateOnly(2026, 11, d)).ToArray(); // 20–25 Nov
        var logs = FullDays(nov).Concat(FullDays(Nov30, Dec1)).ToList();

        var streak = _service.Compute([Water, Walks], logs, today: Dec1);

        streak.Best.Should().Be(6);
        streak.Current.Should().Be(2);
    }

    [Fact]
    public void Best_ForgivesSingleMissesLikeCurrent()
    {
        var logs = FullDays(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 22), new DateOnly(2026, 11, 23));

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Best.Should().Be(3);
        streak.Current.Should().Be(0);
    }

    [Fact]
    public void FirstDay_LimitsTheHistory()
    {
        var logs = FullDays(Nov30, Dec1, Dec2);

        var streak = _service.Compute([Water, Walks], logs, today: Dec2, firstDay: Dec1);

        streak.Current.Should().Be(2);
        streak.Best.Should().Be(2);
    }
}
