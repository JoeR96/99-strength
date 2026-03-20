namespace A2S.Domain.Common;

/// <summary>
/// Dispatches domain events after aggregate persistence.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a collection of domain events.
    /// </summary>
    Task DispatchEventsAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken = default);
}
