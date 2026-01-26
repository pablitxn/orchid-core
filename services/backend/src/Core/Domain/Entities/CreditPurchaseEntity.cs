using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents a credit package purchase transaction.
/// </summary>
public class CreditPurchaseEntity
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }
    public UserEntity? User { get; private set; }

    public Guid CreditPackageId { get; init; }
    public CreditPackageEntity? CreditPackage { get; private set; }

    /// <summary>
    /// Number of credits granted from this purchase.
    /// </summary>
    public int CreditsGranted { get; init; }

    /// <summary>
    /// Amount paid in cents.
    /// </summary>
    public int PricePaidCents { get; init; }

    /// <summary>
    /// Currency of the payment.
    /// </summary>
    public string Currency { get; init; } = "usd";

    /// <summary>
    /// Stripe Checkout Session ID for tracking.
    /// </summary>
    public string? StripeCheckoutSessionId { get; set; }

    /// <summary>
    /// Stripe Payment Intent ID after successful payment.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Current status of the purchase.
    /// </summary>
    public CreditPurchaseStatus Status { get; set; } = CreditPurchaseStatus.Pending;

    /// <summary>
    /// When the purchase was initiated.
    /// </summary>
    public DateTime PurchasedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the purchased credits expire (if applicable).
    /// </summary>
    public DateTime? CreditsExpiresAt { get; init; }

    /// <summary>
    /// Marks the purchase as completed after successful payment.
    /// </summary>
    public void MarkCompleted(string paymentIntentId)
    {
        if (Status != CreditPurchaseStatus.Pending)
            return;

        Status = CreditPurchaseStatus.Completed;
        StripePaymentIntentId = paymentIntentId;
    }

    /// <summary>
    /// Marks the purchase as failed.
    /// </summary>
    public void MarkFailed()
    {
        if (Status != CreditPurchaseStatus.Pending)
            return;

        Status = CreditPurchaseStatus.Failed;
    }

    /// <summary>
    /// Marks the purchase as refunded.
    /// </summary>
    public void MarkRefunded()
    {
        if (Status != CreditPurchaseStatus.Completed)
            return;

        Status = CreditPurchaseStatus.Refunded;
    }
}
