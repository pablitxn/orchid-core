using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginSubmittedEvent(Guid SubmissionId, Guid PublisherId, string PluginName) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
