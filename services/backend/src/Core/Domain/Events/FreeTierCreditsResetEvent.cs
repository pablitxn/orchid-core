using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Event raised when free tier credits are reset for a user.
/// </summary>
public record FreeTierCreditsResetEvent(
    Guid UserId,
    int CreditsGranted) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
