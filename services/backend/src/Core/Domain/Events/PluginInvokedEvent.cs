using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginInvokedEvent(
    Guid PluginId,
    Guid UserId,
    Guid? AgentId,
    string PluginName,
    string FunctionName,
    long DurationMs,
    bool Success
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
