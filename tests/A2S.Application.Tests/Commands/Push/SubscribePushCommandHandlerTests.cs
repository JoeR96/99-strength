using A2S.Application.Commands.Push;
using A2S.Application.Common;
using A2S.Application.Queries.Push;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Commands.Push;

public class SubscribePushCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private const string Endpoint = "https://push.example.com/abc";

    private readonly IPushSubscriptionRepository _pushes = Substitute.For<IPushSubscriptionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly LocalClock _clock = new(new FakeTimeProvider(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero)));

    public SubscribePushCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
    }

    [Fact]
    public async Task NewEndpoint_AddsASubscription_WithDefaultTime()
    {
        var handler = new SubscribePushCommandHandler(_pushes, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new SubscribePushCommand(Endpoint, "p256", "auth", "Safari", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Endpoint.Should().Be(Endpoint);
        result.Value.SessionStartTime.Should().Be(new TimeOnly(17, 30));
        await _pushes.Received(1).AddAsync(Arg.Is<PushSubscription>(s => s.UserId == Joe && s.P256dh == "p256"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KnownEndpoint_RefreshesKeysAndTime_InsteadOfAddingARow()
    {
        var existing = PushSubscription.Create(Joe, Endpoint, "old", "old", null, _clock.NowUtc());
        _pushes.GetByEndpointAsync(Joe, Endpoint, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new SubscribePushCommandHandler(_pushes, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new SubscribePushCommand(Endpoint, "new", "new2", "Chrome", new TimeOnly(6, 45)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id.Value);
        result.Value.SessionStartTime.Should().Be(new TimeOnly(6, 45));
        existing.P256dh.Should().Be("new");
        existing.Auth.Should().Be("new2");
        await _pushes.DidNotReceive().AddAsync(Arg.Any<PushSubscription>(), Arg.Any<CancellationToken>());
        _pushes.Received(1).Update(existing);
    }

    [Fact]
    public async Task BadEndpoint_IsADomainRuleViolation()
    {
        var handler = new SubscribePushCommandHandler(_pushes, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new SubscribePushCommand("http://plain.example.com", "p", "a", null, null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);
        var handler = new SubscribePushCommandHandler(_pushes, _unitOfWork, _currentUser, _clock);

        var result = await handler.Handle(new SubscribePushCommand(Endpoint, "p", "a", null, null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Fact]
    public async Task Unsubscribe_RemovesTheRow_AndIsFineWhenUnknown()
    {
        var existing = PushSubscription.Create(Joe, Endpoint, "p", "a", null, _clock.NowUtc());
        _pushes.GetByEndpointAsync(Joe, Endpoint, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new UnsubscribePushCommandHandler(_pushes, _unitOfWork, _currentUser);

        var known = await handler.Handle(new UnsubscribePushCommand(Endpoint), CancellationToken.None);
        var unknown = await handler.Handle(new UnsubscribePushCommand("https://push.example.com/other"), CancellationToken.None);

        known.IsSuccess.Should().BeTrue();
        unknown.IsSuccess.Should().BeTrue();
        _pushes.Received(1).Remove(existing);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_ReturnsTheCallersSubscriptions_WithoutKeys()
    {
        var sub = PushSubscription.Create(Joe, Endpoint, "p", "a", "Safari", _clock.NowUtc(), new TimeOnly(7, 0));
        _pushes.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<PushSubscription> { sub });
        var handler = new ListPushSubscriptionsQueryHandler(_pushes, _currentUser);

        var result = await handler.Handle(new ListPushSubscriptionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Endpoint, UserAgent = "Safari", SessionStartTime = new TimeOnly(7, 0) });
    }
}
