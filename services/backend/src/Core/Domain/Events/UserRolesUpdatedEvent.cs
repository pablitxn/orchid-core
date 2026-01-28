using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when user roles are updated from Auth0 permissions.
/// </summary>
public record UserRolesUpdatedEvent(
    Guid UserId,
    IReadOnlyList<string> NewRoles) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
