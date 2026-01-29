using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginReviewCreatedEvent(
    Guid PluginId,
    Guid ReviewId,
    Guid UserId,
    int Rating
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
