using A2S.Domain.Common;
using MediatR;

namespace A2S.Application.Common;

/// <summary>
/// Wraps an <see cref="IDomainEvent"/> as a MediatR notification. The infrastructure dispatcher publishes
/// one per event after a successful save; Application handlers subscribe to this type and pattern-match
/// the event they care about.
/// </summary>
public sealed record DomainEventNotification(IDomainEvent DomainEvent) : INotification;
