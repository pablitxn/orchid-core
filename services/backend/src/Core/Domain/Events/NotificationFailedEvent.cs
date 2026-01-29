using Domain.Common;

namespace Domain.Events;

public record NotificationFailedEvent(
    Guid NotificationId,
    Guid UserId,
    string Type,
    string Channel,
    string ErrorMessage) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
