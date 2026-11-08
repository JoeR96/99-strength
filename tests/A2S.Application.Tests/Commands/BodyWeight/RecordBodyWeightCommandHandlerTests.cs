using A2S.Application.Commands.BodyWeight;
using A2S.Application.Common;
using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.BodyWeight;

public class RecordBodyWeightCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Day = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 7, 0, 0, TimeSpan.Zero);

    private readonly IBodyWeightRepository _entries = Substitute.For<IBodyWeightRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private readonly RecordBodyWeightCommandHandler _handler;

    public RecordBodyWeightCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _clock.NowUtc().Returns(Now);
        _handler = new RecordBodyWeightCommandHandler(_entries, _unitOfWork, _currentUser, _clock);
    }

    [Fact]
    public async Task FirstWeighIn_CreatesEntry()
    {
        _entries.GetForDateAsync(Joe, Day, Arg.Any<CancellationToken>()).Returns((BodyWeightEntry?)null);

        var result = await _handler.Handle(new RecordBodyWeightCommand(Day, 95m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new A2S.Application.DTOs.BodyWeightEntryDto(Day, 95m, Now));
        await _entries.Received(1).AddAsync(Arg.Is<BodyWeightEntry>(e => e.UserId == Joe && e.Date == Day && e.Kg == 95m), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondWeighInSameDate_UpdatesTheEntry()
    {
        var existing = BodyWeightEntry.Create(Joe, Day, 95m, Now.AddHours(-1));
        _entries.GetForDateAsync(Joe, Day, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _handler.Handle(new RecordBodyWeightCommand(Day, 94.6m), CancellationToken.None);

        result.Value.Kg.Should().Be(94.6m);
        result.Value.RecordedAtUtc.Should().Be(Now);
        existing.Kg.Should().Be(94.6m);
        _entries.Received(1).Update(existing);
        await _entries.DidNotReceive().AddAsync(Arg.Any<BodyWeightEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutOfRange_IsADomainRuleViolation()
    {
        _entries.GetForDateAsync(Joe, Day, Arg.Any<CancellationToken>()).Returns((BodyWeightEntry?)null);

        var result = await _handler.Handle(new RecordBodyWeightCommand(Day, 12m), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new RecordBodyWeightCommand(Day, 95m), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
