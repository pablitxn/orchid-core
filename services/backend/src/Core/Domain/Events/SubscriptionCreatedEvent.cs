using Domain.Common;

namespace Domain.Events;

public record SubscriptionCreatedEvent(Guid SubscriptionId, Guid UserId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}