using A2S.Application.Common;
using A2S.Application.Queries.Habits;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.Habits;

public class GetHabitsTodayQueryHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Today = new(2026, 12, 2);
    private static readonly DateTimeOffset At = new(2026, 12, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private readonly HabitDefinition _water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0, "droplet");
    private readonly HabitDefinition _walks = HabitDefinition.Create(Joe, "Walks", HabitType.Counter, 3, 1, null, 1);
    private readonly GetHabitsTodayQueryHandler _handler;

    public GetHabitsTodayQueryHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _clock.Today().Returns(Today);
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>())
            .Returns(new List<HabitDefinition> { _walks, _water });
        _handler = new GetHabitsTodayQueryHandler(_habits, _currentUser, _clock, new HabitStreakService());
    }

    private HabitLog Log(HabitDefinition habit, DateOnly date, decimal value) => HabitLog.Create(habit.Id, Joe, date, value, At);

    [Fact]
    public async Task ReportsValuesMinimumTargetAndStreak()
    {
        var nov30 = new DateOnly(2026, 11, 30);
        _habits.GetLogsAsync(Joe, Arg.Any<DateOnly>(), Today, Arg.Any<CancellationToken>()).Returns(new List<HabitLog>
        {
            Log(_water, nov30, 6), Log(_walks, nov30, 3),
            Log(_water, Today, 4), Log(_walks, Today, 3)
        });

        var result = await _handler.Handle(new GetHabitsTodayQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Date.Should().Be(Today);
        dto.Habits.Select(h => h.Name).Should().Equal("Water", "Walks");

        var water = dto.Habits[0];
        water.Value.Should().Be(4);
        water.MetMinimum.Should().BeTrue();
        water.MetTarget.Should().BeFalse();
        water.Icon.Should().Be("droplet");
        water.Unit.Should().Be("bottles");

        var walks = dto.Habits[1];
        walks.Value.Should().Be(3);
        walks.MetTarget.Should().BeTrue();

        dto.Streak.Current.Should().Be(2);
        dto.Streak.GraceUsed.Should().BeTrue();
        dto.Streak.TodayCounted.Should().BeTrue();
        dto.Streak.LastCountedDay.Should().Be(Today);
    }

    [Fact]
    public async Task UnloggedHabit_IsZeroAndNotMet()
    {
        _habits.GetLogsAsync(Joe, Arg.Any<DateOnly>(), Today, Arg.Any<CancellationToken>())
            .Returns(new List<HabitLog> { Log(_water, Today, 7) });

        var result = await _handler.Handle(new GetHabitsTodayQuery(Today), CancellationToken.None);

        var walks = result.Value.Habits.Single(h => h.Name == "Walks");
        walks.Value.Should().Be(0);
        walks.MetMinimum.Should().BeFalse();
        result.Value.Habits.Single(h => h.Name == "Water").Value.Should().Be(7);
        result.Value.Streak.TodayCounted.Should().BeFalse();
    }

    [Fact]
    public async Task ExplicitDate_IsUsedInsteadOfToday()
    {
        var yesterday = Today.AddDays(-1);
        _habits.GetLogsAsync(Joe, Arg.Any<DateOnly>(), yesterday, Arg.Any<CancellationToken>())
            .Returns(new List<HabitLog> { Log(_water, yesterday, 6), Log(_walks, yesterday, 1) });

        var result = await _handler.Handle(new GetHabitsTodayQuery(yesterday), CancellationToken.None);

        result.Value.Date.Should().Be(yesterday);
        result.Value.Habits.Should().OnlyContain(h => h.MetMinimum);
        result.Value.Streak.Current.Should().Be(1);
    }

    [Fact]
    public async Task NoHabits_IsEmptyWithZeroStreak()
    {
        _habits.ListForUserAsync(Joe, false, Arg.Any<CancellationToken>()).Returns(new List<HabitDefinition>());
        _habits.GetLogsAsync(Joe, Arg.Any<DateOnly>(), Today, Arg.Any<CancellationToken>()).Returns(new List<HabitLog>());

        var result = await _handler.Handle(new GetHabitsTodayQuery(), CancellationToken.None);

        result.Value.Habits.Should().BeEmpty();
        result.Value.Streak.Should().Be(new A2S.Application.DTOs.HabitStreakDto(0, 0, null, false, false));
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetHabitsTodayQuery(), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
