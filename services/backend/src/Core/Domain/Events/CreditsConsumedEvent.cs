using Domain.Common;

namespace Domain.Events;

public record CreditsConsumedEvent(
    Guid SubscriptionId, 
    Guid UserId, 
    int Amount,
    string? ResourceType = null,
    string? ResourceName = null) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}