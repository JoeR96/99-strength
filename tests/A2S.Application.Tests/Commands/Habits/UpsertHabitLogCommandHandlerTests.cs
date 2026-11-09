using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Habits;

public class UpsertHabitLogCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly HabitDefinition _water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
    private readonly UpsertHabitLogCommandHandler _handler;

    public UpsertHabitLogCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _habits.GetByIdAsync(_water.Id, Arg.Any<CancellationToken>()).Returns(_water);
        _handler = new UpsertHabitLogCommandHandler(_habits, _unitOfWork, _currentUser);
    }

    [Fact]
    public async Task NoLogYet_CreatesOne()
    {
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns((HabitLog?)null);

        var result = await _handler.Handle(new UpsertHabitLogCommand(_water.Id.Value, Day, 3, T0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(3);
        result.Value.UpdatedAtUtc.Should().Be(T0);
        await _habits.Received(1).AddLogAsync(Arg.Is<HabitLog>(l => l.Value == 3 && l.Date == Day && l.UserId == Joe), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewerTimestamp_OverwritesAndSaves()
    {
        var existing = HabitLog.Create(_water.Id, Joe, Day, 3, T0);
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new UpsertHabitLogCommand(_water.Id.Value, Day, 5, T0.AddMinutes(1)), CancellationToken.None);

        result.Value.Value.Should().Be(5);
        _habits.Received(1).UpdateLog(existing);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OlderTimestamp_LeavesTheNewerValueAndReturnsIt()
    {
        var existing = HabitLog.Create(_water.Id, Joe, Day, 5, T0);
        _habits.GetLogAsync(_water.Id, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new UpsertHabitLogCommand(_water.Id.Value, Day, 2, T0.AddMinutes(-5)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(5);
        result.Value.UpdatedAtUtc.Should().Be(T0);
        _habits.DidNotReceive().UpdateLog(Arg.Any<HabitLog>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SomeoneElsesHabit_IsForbidden()
    {
        var theirs = HabitDefinition.Create(new UserId("user_other"), "Water", HabitType.Counter, 6, 4, null, 0);
        _habits.GetByIdAsync(theirs.Id, Arg.Any<CancellationToken>()).Returns(theirs);

        var result = await _handler.Handle(new UpsertHabitLogCommand(theirs.Id.Value, Day, 1, T0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownHabit_IsNotFound()
    {
        var result = await _handler.Handle(new UpsertHabitLogCommand(Guid.Parse("99999999-9999-9999-9999-999999999999"), Day, 1, T0), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new UpsertHabitLogCommand(_water.Id.Value, Day, 1, T0), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
