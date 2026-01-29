using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginUninstalledEvent(
    Guid PluginId,
    Guid UserId,
    string PluginName
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
