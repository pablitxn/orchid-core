using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Event raised when a user's credit balance falls below the configured threshold.
/// </summary>
public record LowCreditBalanceEvent(
    Guid UserId,
    int CurrentBalance,
    int Threshold) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
