using A2S.Application.Commands.Push;
using A2S.Application.Common;
using A2S.Application.Push;
using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Push;

/// <summary>The finisher wizard's schedule commands and the nudge tick that sends due phase changes.</summary>
public class FinisherNudgeTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Start = new(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly IFinisherScheduleRepository _schedules = Substitute.For<IFinisherScheduleRepository>();
    private readonly IPushSubscriptionRepository _pushes = Substitute.For<IPushSubscriptionRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly LocalClock _clock;
    private readonly List<FinisherSchedule> _stored = new();

    public FinisherNudgeTests()
    {
        _clock = new LocalClock(_time);
        _currentUser.UserId.Returns(Joe.Value);
        _schedules.GetAsync(Joe, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => _stored.FirstOrDefault(s => s.ClientSessionId == c.ArgAt<string>(1)));
        _schedules.When(r => r.AddAsync(Arg.Any<FinisherSchedule>(), Arg.Any<CancellationToken>())).Do(c => _stored.Add(c.Arg<FinisherSchedule>()));
        _schedules.When(r => r.Remove(Arg.Any<FinisherSchedule>())).Do(c => _stored.Remove(c.Arg<FinisherSchedule>()));
        _schedules.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(c => _stored.Where(s => s.NextDueAtUtc <= c.Arg<DateTimeOffset>()).ToList());
        _pushes.ListForUserAsync(Joe, Arg.Any<CancellationToken>())
            .Returns(new List<PushSubscription> { PushSubscription.Create(Joe, "https://push.example.com/a", "p", "a", null, Start) });
        _sender.SendAsync(Arg.Any<PushTarget>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>()).Returns(PushSendStatus.Sent);
    }

    private Task<Result<FinisherScheduleDto>> Post(params (int Seconds, string Body)[] nudges) =>
        new ReplaceFinisherScheduleCommandHandler(_schedules, _unitOfWork, _currentUser, _clock).Handle(
            new ReplaceFinisherScheduleCommand("gym-1", 2, null, nudges.Select(n => new FinisherNudgeInput(Start.AddSeconds(n.Seconds), n.Body)).ToList()),
            CancellationToken.None);

    private FinisherNudgeService Service(bool configured = true) => new(
        _schedules, _pushes, _clock, _sender, _unitOfWork,
        Options.Create(configured ? new PushOptions { VapidPublicKey = "pub", VapidPrivateKey = "priv", Subject = "mailto:t@example.com" } : new PushOptions()),
        NullLogger<FinisherNudgeService>.Instance);

    [Fact]
    public async Task Replace_CreatesThenReplacesOneSchedule()
    {
        (await Post((180, "Hard: level 6 for 1 min"))).Value.Revision.Should().Be(1);

        var second = await Post((200, "Hard: level 6 for 1 min"), (260, "Easy: level 3 for 1 min"));

        second.Value.Revision.Should().Be(2);
        second.Value.Pending.Should().Be(2);
        _stored.Should().ContainSingle().Which.NextDueAtUtc.Should().Be(Start.AddSeconds(200));
    }

    [Fact]
    public async Task Replace_WithTooFarAhead_IsADomainFailure()
    {
        var result = await Post((60 * 60 * 7, "x"));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Tick_SendsTheDueNudgeOnce_WithTagAndDeepLink()
    {
        await Post((180, "Hard: level 6 for 1 min"), (240, "Easy: level 3 for 1 min"));
        _time.Advance(TimeSpan.FromSeconds(181));

        (await Service().RunOnceAsync()).Should().Be(1);
        (await Service().RunOnceAsync()).Should().Be(0);

        await _sender.Received(1).SendAsync(
            Arg.Any<PushTarget>(),
            new PushMessage("Stair finisher", "Hard: level 6 for 1 min", "/gym/2?finisher=1", "finisher-gym-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tick_NeverSendsAReplacedNudge()
    {
        await Post((180, "Hard: level 6 for 1 min"));
        await Post((300, "Hard: level 6 for 1 min"));
        _time.Advance(TimeSpan.FromSeconds(200));

        (await Service().RunOnceAsync()).Should().Be(0);
        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Tick_AfterDelete_SendsNothing()
    {
        await Post((180, "Hard"));
        (await new DeleteFinisherScheduleCommandHandler(_schedules, _unitOfWork, _currentUser).Handle(new DeleteFinisherScheduleCommand("gym-1"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await new DeleteFinisherScheduleCommandHandler(_schedules, _unitOfWork, _currentUser).Handle(new DeleteFinisherScheduleCommand("gym-1"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        _time.Advance(TimeSpan.FromSeconds(181));

        (await Service().RunOnceAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Tick_WithoutSubscriptions_StillMarksSent_AndRemovesExhausted()
    {
        _pushes.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(new List<PushSubscription>());
        await Post((180, "Hard"));
        _time.Advance(TimeSpan.FromSeconds(181));

        await Service().RunOnceAsync();

        _stored.Should().BeEmpty();
        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Tick_WithoutVapidKeys_IsANoOp()
    {
        await Post((180, "Hard"));
        _time.Advance(TimeSpan.FromSeconds(181));

        (await Service(configured: false).RunOnceAsync()).Should().Be(0);
        _stored.Single().NextDueAtUtc.Should().NotBeNull();
    }
}
