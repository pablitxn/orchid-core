namespace Domain.Exceptions;

public class UnauthorizedDomainAccessException : Exception
{
    public UnauthorizedDomainAccessException(string message) : base(message)
    {
    }

    public UnauthorizedDomainAccessException(string entityType, Guid entityId, Guid userId)
        : base($"User '{userId}' is not authorized to access {entityType} '{entityId}'.")
    {
        EntityType = entityType;
        EntityId = entityId;
        UserId = userId;
    }

    public string? EntityType { get; }
    public Guid EntityId { get; }
    public Guid UserId { get; }
}
