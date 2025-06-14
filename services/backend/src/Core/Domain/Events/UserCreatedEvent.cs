using Domain.Common;
using MediatR;

namespace Domain.Events;

public record UserCreatedEvent(Guid UserId, string Email) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; }
}