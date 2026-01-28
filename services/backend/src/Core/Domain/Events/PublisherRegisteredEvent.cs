using Domain.Common;
using MediatR;

namespace Domain.Events;

public record PublisherRegisteredEvent(Guid PublisherId, Guid UserId, string PublisherName) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
