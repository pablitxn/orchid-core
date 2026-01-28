using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginVersionPublishedEvent(
    Guid VersionId,
    Guid PluginId,
    string Version,
    bool IsBreakingChange
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
