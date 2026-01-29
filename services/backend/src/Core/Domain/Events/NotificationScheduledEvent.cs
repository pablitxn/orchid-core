using Domain.Common;

namespace Domain.Events;

public record NotificationScheduledEvent(
    Guid NotificationId,
    Guid UserId,
    string Type,
    DateTime ScheduledFor,
    string? SourceService = null) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
