using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when user profile is updated from Auth0 data.
/// </summary>
public record UserProfileSyncedEvent(
    Guid UserId,
    string? Name,
    string? AvatarUrl) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
