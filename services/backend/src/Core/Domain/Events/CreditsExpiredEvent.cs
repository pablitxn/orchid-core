using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Event raised when credits expire for a user.
/// </summary>
public record CreditsExpiredEvent(
    Guid UserId,
    int ExpiredCredits) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
