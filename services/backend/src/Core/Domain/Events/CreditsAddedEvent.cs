using Domain.Common;

namespace Domain.Events;

public record CreditsAddedEvent(Guid UserCreditsId, Guid UserId, int Amount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
