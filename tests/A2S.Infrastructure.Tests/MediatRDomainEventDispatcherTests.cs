using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace A2S.Infrastructure.Tests;

public class MediatRDomainEventDispatcherTests
{
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly ILogger<MediatRDomainEventDispatcher> _logger = Substitute.For<ILogger<MediatRDomainEventDispatcher>>();
    private readonly MediatRDomainEventDispatcher _sut;

    public MediatRDomainEventDispatcherTests()
    {
        _sut = new MediatRDomainEventDispatcher(_publisher, _logger);
    }

    [Fact]
    public async Task WhenEventsProvidedThenAllArePublished()
    {
        var event1 = new TestDomainEvent("Event1");
        var event2 = new TestDomainEvent("Event2");
        var events = new List<IDomainEvent> { event1, event2 };

        await _sut.DispatchEventsAsync(events, CancellationToken.None);

        await _publisher.Received(2).Publish(
            Arg.Any<DomainEventNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenEventsProvidedThenPublishedInOrder()
    {
        var event1 = new TestDomainEvent("First");
        var event2 = new TestDomainEvent("Second");
        var events = new List<IDomainEvent> { event1, event2 };
        var publishedEvents = new List<IDomainEvent>();

        _publisher.Publish(Arg.Any<DomainEventNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => publishedEvents.Add(ci.Arg<DomainEventNotification>().DomainEvent));

        await _sut.DispatchEventsAsync(events, CancellationToken.None);

        publishedEvents.Should().HaveCount(2);
        publishedEvents[0].Should().BeSameAs(event1);
        publishedEvents[1].Should().BeSameAs(event2);
    }

    [Fact]
    public async Task WhenNoEventsThenNothingPublished()
    {
        var events = new List<IDomainEvent>();

        await _sut.DispatchEventsAsync(events, CancellationToken.None);

        await _publisher.DidNotReceive().Publish(
            Arg.Any<DomainEventNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenCancellationTokenProvidedThenPassedThrough()
    {
        var cts = new CancellationTokenSource();
        var events = new List<IDomainEvent> { new TestDomainEvent("Test") };

        await _sut.DispatchEventsAsync(events, cts.Token);

        await _publisher.Received(1).Publish(
            Arg.Any<DomainEventNotification>(),
            cts.Token);
    }

    private sealed record TestDomainEvent(string Name) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
    }
}
