using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginApprovedEvent(Guid PluginId, Guid SubmissionId, Guid ReviewedBy) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
