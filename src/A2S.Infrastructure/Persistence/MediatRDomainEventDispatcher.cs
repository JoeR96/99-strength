using A2S.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Wraps an IDomainEvent as a MediatR INotification for publishing.
/// </summary>
public sealed record DomainEventNotification(IDomainEvent DomainEvent) : INotification;

/// <summary>
/// Dispatches domain events via MediatR's notification pipeline.
/// </summary>
public sealed class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;
    private readonly ILogger<MediatRDomainEventDispatcher> _logger;

    public MediatRDomainEventDispatcher(IPublisher publisher, ILogger<MediatRDomainEventDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(logger);
        _publisher = publisher;
        _logger = logger;
    }

    public async Task DispatchEventsAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            _logger.LogDebug("Dispatching domain event {EventType}", domainEvent.GetType().Name);
            await _publisher.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }
}
