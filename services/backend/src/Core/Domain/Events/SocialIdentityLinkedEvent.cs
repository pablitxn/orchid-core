using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when a new social identity is linked to a user account.
/// </summary>
public record SocialIdentityLinkedEvent(
    Guid UserId,
    Guid IdentityId,
    string Provider,
    bool IsPrimary) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
