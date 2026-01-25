using Domain.Enums;

namespace Domain.Entities;

public class SubscriptionPlanEntity
{
    public Guid Id { get; set; }
    public required SubscriptionPlanEnum PlanEnum { get; set; }
    public required decimal Price { get; set; }
    public required int Credits { get; set; }
    public PaymentUnit PaymentUnit { get; set; } = PaymentUnit.PerMessage;

    /// <summary>
    /// Stripe Price ID for this plan (e.g., "price_xxxxx").
    /// Required for Stripe checkout and subscription management.
    /// </summary>
    public string? StripePriceId { get; set; }

    /// <summary>
    /// Whether this plan is a recurring subscription or one-time purchase.
    /// </summary>
    public bool IsRecurring { get; set; } = true;

    /// <summary>
    /// Gets the display name for this subscription plan.
    /// </summary>
    public string Name => PlanEnum.ToString();
}