using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when MFA is enabled for a user account.
/// </summary>
public record MfaEnabledEvent(
    Guid UserId,
    string Method) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
