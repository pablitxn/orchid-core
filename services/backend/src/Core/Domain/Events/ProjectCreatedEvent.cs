using Domain.Common;

namespace Domain.Events;

public record ProjectCreatedEvent(Guid ProjectId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}