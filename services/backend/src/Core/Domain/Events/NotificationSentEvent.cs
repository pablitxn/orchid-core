using Domain.Common;

namespace Domain.Events;

public record NotificationSentEvent(
    Guid NotificationId,
    Guid UserId,
    string Type,
    string Channel) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
