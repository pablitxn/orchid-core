using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Event raised when credits are purchased via a credit package.
/// </summary>
public record CreditsPurchasedEvent(
    Guid PurchaseId,
    Guid UserId,
    Guid PackageId,
    int CreditsGranted,
    int PricePaidCents) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
