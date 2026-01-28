using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when an existing user is linked to an Auth0 account.
/// </summary>
public record Auth0UserLinkedEvent(
    Guid UserId,
    string Auth0UserId,
    string? Auth0Connection) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
