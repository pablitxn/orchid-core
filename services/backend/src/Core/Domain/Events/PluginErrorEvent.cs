using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginErrorEvent(
    Guid PluginId,
    Guid UserId,
    Guid? AgentId,
    string PluginName,
    string FunctionName,
    string ErrorMessage,
    string? ErrorType
) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
