using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PublisherVerifiedEvent(Guid PublisherId, Guid VerifiedBy) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
