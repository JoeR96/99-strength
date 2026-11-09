using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Habits;

public class IncrementHabitCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly HabitDefinition _water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
    private readonly IncrementHabitCommandHandler _handler;

    public IncrementHabitCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _habits.GetByIdAsync(_water.Id, Arg.Any<CancellationToken>()).Returns(_water);
        _handler = new IncrementHabitCommandHandler(_habits, _unitOfWork, _currentUser);
    }

    [Fact]
    public async Task NoLogYet_PlusOne_CreatesLogAtOne()
    {
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, 1, T0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(1);
        await _habits.Received(1).AddLogAsync(Arg.Is<HabitLog>(l => l.Value == 1 && l.HabitId == _water.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoLogYet_MinusOne_CreatesLogAtZero()
    {
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, -1, T0), CancellationToken.None);

        result.Value.Value.Should().Be(0);
    }

    // The validator only lets +1 / -1 through, but the handler must not depend on that: the first tap of
    // a day takes its delta exactly as a later one does.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(-1, 0)]
    [InlineData(-3, 0)]
    public async Task NoLogYet_AppliesTheWholeDelta_NeverBelowZero(int delta, int expected)
    {
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, delta, T0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
        result.Value.UpdatedAtUtc.Should().Be(T0);
        await _habits.Received(1).AddLogAsync(Arg.Is<HabitLog>(l => l.Value == expected), Arg.Any<CancellationToken>());
        _habits.DidNotReceive().UpdateLog(Arg.Any<HabitLog>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(-3)]
    public async Task NoLogYet_EndsWhereTheSameTapOnAnEmptyLogEnds(int delta)
    {
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);
        var first = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, delta, T0), CancellationToken.None);

        var otherDay = Day.AddDays(1);
        _habits.GetLogAsync(_water.Id, otherDay, Arg.Any<CancellationToken>()).Returns(HabitLog.Create(_water.Id, Joe, otherDay, 0, T0.AddMinutes(-1)));
        var later = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, otherDay, delta, T0), CancellationToken.None);

        first.Value.Value.Should().Be(later.Value.Value);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, 0)]
    public async Task Checkbox_NoLogYet_PlusOneTicks_MinusOneLeavesItUnticked(int delta, int expected)
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 1);
        _habits.GetByIdAsync(creatine.Id, Arg.Any<CancellationToken>()).Returns(creatine);
        _habits.GetLogAsync(creatine.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);

        var result = await _handler.Handle(new IncrementHabitCommand(creatine.Id.Value, Day, delta, T0), CancellationToken.None);

        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Existing_PlusOne_PastTarget_IsKept()
    {
        var existing = HabitLog.Create(_water.Id, Joe, Day, 6, T0);
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, 1, T0.AddMinutes(1)), CancellationToken.None);

        result.Value.Value.Should().Be(7);
        result.Value.UpdatedAtUtc.Should().Be(T0.AddMinutes(1));
        _habits.Received(1).UpdateLog(existing);
    }

    [Fact]
    public async Task Existing_MinusOne_FloorsAtZero()
    {
        var existing = HabitLog.Create(_water.Id, Joe, Day, 0, T0);
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, -1, T0.AddMinutes(1)), CancellationToken.None);

        result.Value.Value.Should().Be(0);
    }

    [Fact]
    public async Task LateTap_DoesNotMoveTheTimestampBackwards()
    {
        var existing = HabitLog.Create(_water.Id, Joe, Day, 2, T0);
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, 1, T0.AddMinutes(-10)), CancellationToken.None);

        result.Value.Value.Should().Be(3);
        result.Value.UpdatedAtUtc.Should().Be(T0);
    }

    [Fact]
    public async Task Checkbox_PlusOneTicks_MinusOneUnticks()
    {
        var creatine = HabitDefinition.Create(Joe, "Creatine", HabitType.Checkbox, 1, 1, null, 1);
        _habits.GetByIdAsync(creatine.Id, Arg.Any<CancellationToken>()).Returns(creatine);
        var existing = HabitLog.Create(creatine.Id, Joe, Day, 1, T0);
        _habits.GetLogAsync(creatine.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var ticked = await _handler.Handle(new IncrementHabitCommand(creatine.Id.Value, Day, 1, T0.AddMinutes(1)), CancellationToken.None);
        var unticked = await _handler.Handle(new IncrementHabitCommand(creatine.Id.Value, Day, -1, T0.AddMinutes(2)), CancellationToken.None);

        ticked.Value.Value.Should().Be(1);
        unticked.Value.Value.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new IncrementHabitCommand(_water.Id.Value, Day, 1, T0), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
