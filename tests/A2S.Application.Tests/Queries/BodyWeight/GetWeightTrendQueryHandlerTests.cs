using A2S.Application.Common;
using A2S.Application.Interfaces;
using A2S.Application.Queries.BodyWeight;
using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries.BodyWeight;

public class GetWeightTrendQueryHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Today = new(2026, 12, 7);
    private static readonly DateTimeOffset At = new(2026, 12, 7, 7, 0, 0, TimeSpan.Zero);

    private readonly IBodyWeightRepository _entries = Substitute.For<IBodyWeightRepository>();
    private readonly IPlanCheckpointSource _checkpoints = Substitute.For<IPlanCheckpointSource>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private readonly GetWeightTrendQueryHandler _handler;

    public GetWeightTrendQueryHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _clock.Today().Returns(Today);
        _handler = new GetWeightTrendQueryHandler(_entries, _checkpoints, _currentUser, _clock, new TrendService());
    }

    private static BodyWeightEntry Entry(DateOnly date, decimal kg) => BodyWeightEntry.Create(Joe, date, kg, At);

    [Fact]
    public async Task WithoutAPlan_PointsOnlyAndNoPace()
    {
        var from = new DateOnly(2026, 12, 5);
        _entries.ListAsync(Joe, from.AddDays(-6), Today, Arg.Any<CancellationToken>())
            .Returns(new List<BodyWeightEntry> { Entry(new DateOnly(2026, 12, 5), 93m), Entry(new DateOnly(2026, 12, 6), 92.5m), Entry(Today, 92m) });
        _checkpoints.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((PlanCheckpoints?)null);

        var result = await _handler.Handle(new GetWeightTrendQuery(from, Today), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Points.Select(p => p.Kg).Should().Equal(93m, 92.5m, 92m);
        result.Value.Points.Select(p => p.Ewma).Should().Equal(93m, 92.95m, 92.86m);
        result.Value.Pace.Should().BeNull();
    }

    [Fact]
    public async Task WithAPlan_PaceUsesTheLatestEwmaAgainstTodaysTarget()
    {
        var from = new DateOnly(2026, 12, 7);
        _entries.ListAsync(Joe, from.AddDays(-6), Today, Arg.Any<CancellationToken>())
            .Returns(new List<BodyWeightEntry> { Entry(Today, 92.5m) });
        _checkpoints.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new PlanCheckpoints(
            new DateOnly(2026, 11, 9), 95m,
            [(new DateOnly(2026, 12, 7), 92m), (new DateOnly(2026, 12, 21), 91m)]));

        var result = await _handler.Handle(new GetWeightTrendQuery(from, Today), CancellationToken.None);

        result.Value.Pace.Should().Be(new A2S.Application.DTOs.PaceDto(Pace.Behind, 92m, 92.5m, 0.5m));
    }

    [Fact]
    public async Task EntriesBeforeTheRange_WarmTheTrendButAreNotReturned()
    {
        var from = new DateOnly(2026, 12, 7);
        _entries.ListAsync(Joe, from.AddDays(-6), Today, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, 7).Select(i => Entry(from.AddDays(i - 6), 93m)).ToList());
        _checkpoints.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((PlanCheckpoints?)null);

        var result = await _handler.Handle(new GetWeightTrendQuery(from, Today), CancellationToken.None);

        result.Value.Points.Should().ContainSingle();
        result.Value.Points[0].Date.Should().Be(Today);
        result.Value.Points[0].MovingAverage7.Should().Be(93m);
    }

    [Fact]
    public async Task NoEntries_IsEmptyAndDoesNotAskForCheckpoints()
    {
        _entries.ListAsync(Joe, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new List<BodyWeightEntry>());

        var result = await _handler.Handle(new GetWeightTrendQuery(Today.AddDays(-30), Today), CancellationToken.None);

        result.Value.Points.Should().BeEmpty();
        result.Value.Pace.Should().BeNull();
        await _checkpoints.DidNotReceive().ForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetWeightTrendQuery(Today.AddDays(-30), Today), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }
}
