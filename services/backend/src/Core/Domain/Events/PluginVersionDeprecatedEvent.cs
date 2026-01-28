using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginVersionDeprecatedEvent(
    Guid VersionId,
    Guid PluginId,
    string Version,
    DateTime DeprecationEndsAt,
    Guid? SuccessorVersionId
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
