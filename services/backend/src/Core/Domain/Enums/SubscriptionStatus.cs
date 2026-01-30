namespace Domain.Enums;

/// <summary>
/// Represents the status of a subscription, aligned with Stripe subscription statuses.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Subscription has not been created with a payment provider yet.
    /// This is the default value (CLR default = 0) to match EF Core's database default.
    /// </summary>
    Incomplete,

    /// <summary>
    /// Subscription is active and in good standing.
    /// </summary>
    Active,

    /// <summary>
    /// Subscription is in trial period.
    /// </summary>
    Trialing,

    /// <summary>
    /// Payment failed but subscription not yet canceled.
    /// </summary>
    PastDue,

    /// <summary>
    /// Subscription has been canceled and is no longer active.
    /// </summary>
    Canceled,

    /// <summary>
    /// Subscription ended due to non-payment after retries.
    /// </summary>
    Unpaid,

    /// <summary>
    /// Subscription is paused (if enabled in Stripe).
    /// </summary>
    Paused
}
