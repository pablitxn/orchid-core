using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when a new user is created from Auth0 authentication.
/// Triggers the same onboarding flow as UserCreatedEvent.
/// </summary>
public record UserCreatedFromAuth0Event(
    Guid UserId,
    string Email,
    string Auth0UserId,
    string? Auth0Connection) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
