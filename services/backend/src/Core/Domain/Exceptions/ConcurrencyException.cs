namespace Domain.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string entityType, Guid entityId, int expectedVersion, int actualVersion)
        : base($"Concurrency conflict on {entityType} with ID {entityId}. Expected version {expectedVersion}, but found {actualVersion}.")
    {
        EntityType = entityType;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public string EntityType { get; }
    public Guid EntityId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }
}