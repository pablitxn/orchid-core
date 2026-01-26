using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Port for Stripe payment operations.
/// </summary>
public interface IStripeService
{
    /// <summary>
    /// Creates or retrieves a Stripe customer for the given user.
    /// </summary>
    Task<string> GetOrCreateCustomerAsync(Guid userId, string email, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Checkout session for subscribing to a plan.
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string customerId,
        string priceId,
        string successUrl,
        string cancelUrl,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a billing portal session for the customer to manage their subscription.
    /// </summary>
    Task<string> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the subscription to a different plan.
    /// </summary>
    Task<StripeSubscriptionInfo> ChangePlanAsync(
        string subscriptionId,
        string newPriceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the subscription at the end of the current billing period.
    /// </summary>
    Task<StripeSubscriptionInfo> CancelSubscriptionAsync(
        string subscriptionId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a subscription that was scheduled for cancellation.
    /// </summary>
    Task<StripeSubscriptionInfo> ReactivateSubscriptionAsync(
        string subscriptionId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current subscription details from Stripe.
    /// </summary>
    Task<StripeSubscriptionInfo?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a webhook signature and parses the event.
    /// </summary>
    StripeWebhookEvent? ValidateAndParseWebhook(string payload, string signature);

    /// <summary>
    /// Creates a Stripe Checkout session for a one-time credit package purchase.
    /// </summary>
    Task<CheckoutSessionResult> CreateCreditPurchaseCheckoutSessionAsync(
        string customerId,
        string priceId,
        Guid creditPurchaseId,
        string successUrl,
        string cancelUrl,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of creating a checkout session.
/// </summary>
public record CheckoutSessionResult(
    string SessionId,
    string Url);

/// <summary>
/// Subscription information from Stripe.
/// </summary>
public record StripeSubscriptionInfo(
    string SubscriptionId,
    string CustomerId,
    string Status,
    string PriceId,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? CanceledAt,
    DateTime? TrialEnd);

/// <summary>
/// Parsed Stripe webhook event with typed data.
/// </summary>
public record StripeWebhookEvent(
    string EventId,
    string EventType,
    StripeWebhookData Data);

/// <summary>
/// Parsed webhook data - the adapter converts Stripe types to this.
/// </summary>
public record StripeWebhookData(
    string? CustomerId,
    string? SubscriptionId,
    string? PriceId,
    string? Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? CanceledAt,
    DateTime? TrialEnd,
    string? PaymentIntentId = null,
    long? AmountReceived = null,
    string? Currency = null,
    string? InvoiceId = null,
    string? SessionId = null,
    string? Mode = null,
    Dictionary<string, string>? Metadata = null);
