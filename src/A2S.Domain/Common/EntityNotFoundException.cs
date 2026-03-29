namespace A2S.Domain.Common;

/// <summary>
/// Thrown when a requested entity cannot be found.
/// </summary>
public class EntityNotFoundException : DomainException
{
    public string EntityType { get; }
    public object? EntityId { get; }

    public EntityNotFoundException(string entityType, object? entityId)
        : base($"{entityType} with ID '{entityId}' was not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public EntityNotFoundException(string message) : base(message)
    {
        EntityType = string.Empty;
    }
}
