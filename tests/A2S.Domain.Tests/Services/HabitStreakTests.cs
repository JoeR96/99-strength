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

    // --- Brief § 6: minimum version, over target, never-miss-twice across week/month/year boundaries ---

    [Fact]
    public void OneMissAcrossWeekBoundary_SundayMissed_StreakContinuesOnMonday()
    {
        var sat5Dec = new DateOnly(2026, 12, 5);
        var mon7Dec = new DateOnly(2026, 12, 7);
        var tue8Dec = new DateOnly(2026, 12, 8);
        var logs = FullDays(sat5Dec, mon7Dec, tue8Dec); // Sunday 6 Dec has nothing logged

        var streak = _service.Compute([Water, Walks], logs, today: tue8Dec);

        streak.Current.Should().Be(3);
        streak.GraceUsed.Should().BeTrue();
        streak.Best.Should().Be(3);
    }

    [Fact]
    public void TwoMissesAcrossWeekBoundary_SundayAndMondayMissed_StreakResets()
    {
        var sat5Dec = new DateOnly(2026, 12, 5);
        var tue8Dec = new DateOnly(2026, 12, 8);
        var logs = FullDays(new DateOnly(2026, 12, 4), sat5Dec, tue8Dec);

        var streak = _service.Compute([Water, Walks], logs, today: tue8Dec);

        streak.Current.Should().Be(1);
        streak.GraceUsed.Should().BeFalse();
        streak.Best.Should().Be(2, "Friday and Saturday were the longest run");
    }

    [Fact]
    public void OneMissAcrossYearBoundary_NewYearsDayMissed_StreakContinues()
    {
        var dec30 = new DateOnly(2026, 12, 30);
        var dec31 = new DateOnly(2026, 12, 31);
        var jan2 = new DateOnly(2027, 1, 2);
        var logs = FullDays(dec30, dec31, jan2);

        var streak = _service.Compute([Water, Walks], logs, today: jan2);

        streak.Current.Should().Be(3);
        streak.GraceUsed.Should().BeTrue();
        streak.LastCountedDay.Should().Be(jan2);
    }

    [Fact]
    public void TwoMissesAcrossMonthBoundary_LastOfNovemberAndFirstOfDecember_StreakResets()
    {
        var nov29 = new DateOnly(2026, 11, 29);
        var logs = FullDays(new DateOnly(2026, 11, 28), nov29, Dec2); // 30 Nov and 1 Dec missed

        var streak = _service.Compute([Water, Walks], logs, today: Dec2);

        streak.Current.Should().Be(1);
        streak.Best.Should().Be(2);
    }

    [Fact]
    public void OverTarget_DayCounts_AndTheLoggedValueIsKeptAsIs()
    {
        var logs = new List<HabitLog> { Log(Water, Dec1, 9), Log(Walks, Dec1, 5) };

        var streak = _service.Compute([Water, Walks], logs, today: Dec1);

        streak.Current.Should().Be(1);
        logs[0].Value.Should().Be(9, "9/6 is shown, not clamped to 6");
        HabitRules.MetTarget(Water, logs[0]).Should().BeTrue();
    }

    [Fact]
    public void ExactlyTheMinimum_Counts_OneBelowDoesNot()
    {
        var atMinimum = new List<HabitLog> { Log(Water, Dec1, 4), Log(Walks, Dec1, 1) };
        var oneBelow = new List<HabitLog> { Log(Water, Dec1, 3), Log(Walks, Dec1, 1) };

        _service.Compute([Water, Walks], atMinimum, today: Dec1).TodayCounted.Should().BeTrue();
        _service.Compute([Water, Walks], oneBelow, today: Dec1).TodayCounted.Should().BeFalse();
    }

    [Fact]
    public void AWeekOfMinimumDays_KeepsTheStreakAlive()
    {
        var days = Enumerable.Range(0, 7).Select(Nov30.AddDays).ToArray();
        var logs = days.SelectMany(d => new[] { Log(Water, d, 4), Log(Walks, d, 1) }).ToList();

        var streak = _service.Compute([Water, Walks], logs, today: days[6]);

        streak.Current.Should().Be(7);
        streak.Best.Should().Be(7);
        streak.GraceUsed.Should().BeFalse();
    }

    [Fact]
    public void CheckboxHabit_UntickedDoesNotCount_TickedDoes()
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 2);
        var unticked = new List<HabitLog> { Log(Water, Dec1, 6), Log(Walks, Dec1, 3), Log(creatine, Dec1, 0) };
        var ticked = new List<HabitLog> { Log(Water, Dec1, 6), Log(Walks, Dec1, 3), Log(creatine, Dec1, 1) };

        _service.Compute([Water, Walks, creatine], unticked, today: Dec1).TodayCounted.Should().BeFalse();
        _service.Compute([Water, Walks, creatine], ticked, today: Dec1).TodayCounted.Should().BeTrue();
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
