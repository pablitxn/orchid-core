using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PluginRejectedEvent(Guid SubmissionId, Guid ReviewedBy, string Reason) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
