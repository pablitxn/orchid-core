namespace Domain.Common;

public abstract class EntityBase
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public Guid Id { get; protected set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}