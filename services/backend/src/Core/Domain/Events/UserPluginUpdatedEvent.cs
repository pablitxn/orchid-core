using Domain.Common;
using MediatR;

namespace Domain.Events;

public record UserPluginUpdatedEvent(
    Guid UserId,
    Guid PluginId,
    string PreviousVersion,
    string NewVersion,
    bool WasRollback
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
