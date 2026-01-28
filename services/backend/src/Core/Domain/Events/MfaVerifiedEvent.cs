using Domain.Common;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised when MFA is successfully verified during login.
/// </summary>
public record MfaVerifiedEvent(
    Guid UserId,
    string Method) : INotification, IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
