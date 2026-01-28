using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when a user's password is successfully migrated to Auth0.
/// </summary>
public record Auth0MigrationCompletedEvent(
    Guid UserId,
    string Email,
    string Auth0UserId) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
