using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when MFA is disabled for a user account.
/// </summary>
public record MfaDisabledEvent(
    Guid UserId) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
