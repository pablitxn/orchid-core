using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when a social identity is unlinked from a user account.
/// </summary>
public record SocialIdentityUnlinkedEvent(
    Guid UserId,
    Guid IdentityId,
    string Provider) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
