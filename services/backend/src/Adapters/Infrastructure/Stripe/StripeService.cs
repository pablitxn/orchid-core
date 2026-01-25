using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using BillingPortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;

namespace Infrastructure.Stripe;

/// <summary>
/// Stripe payment service adapter implementing IStripeService.
/// </summary>
public class StripeService : IStripeService
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeService> _logger;
    private readonly CustomerService _customerService;
    private readonly SessionService _sessionService;
    private readonly SubscriptionService _subscriptionService;
    private readonly BillingPortalSessionService _portalSessionService;

    public StripeService(
        IOptions<StripeSettings> settings,
        ILogger<StripeService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        StripeConfiguration.ApiKey = _settings.SecretKey;

        _customerService = new CustomerService();
        _sessionService = new SessionService();
        _subscriptionService = new SubscriptionService();
        _portalSessionService = new BillingPortalSessionService();
    }

    public async Task<string> GetOrCreateCustomerAsync(
        Guid userId,
        string email,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var listOptions = new CustomerListOptions
        {
            Email = email,
            Limit = 1
        };

        var customers = await _customerService.ListAsync(listOptions, cancellationToken: cancellationToken);

        if (customers.Data.Count > 0)
        {
            _logger.LogInformation("Found existing Stripe customer for user {UserId}", userId);
            return customers.Data[0].Id;
        }

        var createOptions = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.ToString()
            }
        };

        var customer = await _customerService.CreateAsync(createOptions, cancellationToken: cancellationToken);

        _logger.LogInformation("Created new Stripe customer {CustomerId} for user {UserId}", customer.Id, userId);

        return customer.Id;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string customerId,
        string priceId,
        string successUrl,
        string cancelUrl,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1
                }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["customerId"] = customerId
                }
            }
        };

        var requestOptions = new RequestOptions();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            requestOptions.IdempotencyKey = idempotencyKey;
        }

        var session = await _sessionService.CreateAsync(options, requestOptions, cancellationToken);

        _logger.LogInformation(
            "Created checkout session {SessionId} for customer {CustomerId} with price {PriceId}",
            session.Id, customerId, priceId);

        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<string> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var options = new BillingPortalSessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        };

        var session = await _portalSessionService.CreateAsync(options, cancellationToken: cancellationToken);

        _logger.LogInformation("Created billing portal session for customer {CustomerId}", customerId);

        return session.Url;
    }

    public async Task<StripeSubscriptionInfo> ChangePlanAsync(
        string subscriptionId,
        string newPriceId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionService.GetAsync(subscriptionId, cancellationToken: cancellationToken);

        var options = new SubscriptionUpdateOptions
        {
            Items = new List<SubscriptionItemOptions>
            {
                new()
                {
                    Id = subscription.Items.Data[0].Id,
                    Price = newPriceId
                }
            },
            ProrationBehavior = "create_prorations"
        };

        var requestOptions = new RequestOptions();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            requestOptions.IdempotencyKey = idempotencyKey;
        }

        var updatedSubscription = await _subscriptionService.UpdateAsync(
            subscriptionId, options, requestOptions, cancellationToken);

        _logger.LogInformation(
            "Changed subscription {SubscriptionId} to price {PriceId}",
            subscriptionId, newPriceId);

        return MapToSubscriptionInfo(updatedSubscription);
    }

    public async Task<StripeSubscriptionInfo> CancelSubscriptionAsync(
        string subscriptionId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        };

        var requestOptions = new RequestOptions();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            requestOptions.IdempotencyKey = idempotencyKey;
        }

        var subscription = await _subscriptionService.UpdateAsync(
            subscriptionId, options, requestOptions, cancellationToken);

        _logger.LogInformation(
            "Scheduled cancellation for subscription {SubscriptionId} at period end",
            subscriptionId);

        return MapToSubscriptionInfo(subscription);
    }

    public async Task<StripeSubscriptionInfo> ReactivateSubscriptionAsync(
        string subscriptionId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var options = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        };

        var requestOptions = new RequestOptions();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            requestOptions.IdempotencyKey = idempotencyKey;
        }

        var subscription = await _subscriptionService.UpdateAsync(
            subscriptionId, options, requestOptions, cancellationToken);

        _logger.LogInformation("Reactivated subscription {SubscriptionId}", subscriptionId);

        return MapToSubscriptionInfo(subscription);
    }

    public async Task<StripeSubscriptionInfo?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionService.GetAsync(subscriptionId, cancellationToken: cancellationToken);
            return MapToSubscriptionInfo(subscription);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            _logger.LogWarning("Subscription {SubscriptionId} not found in Stripe", subscriptionId);
            return null;
        }
    }

    public StripeWebhookEvent? ValidateAndParseWebhook(string payload, string signature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                _settings.WebhookSecret);

            _logger.LogInformation(
                "Validated webhook event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            var webhookData = ParseWebhookData(stripeEvent.Type, stripeEvent.Data.Object);

            return new StripeWebhookEvent(
                stripeEvent.Id,
                stripeEvent.Type,
                webhookData);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to validate webhook signature");
            return null;
        }
    }

    private static StripeWebhookData ParseWebhookData(string eventType, IHasObject dataObject)
    {
        return eventType switch
        {
            "checkout.session.completed" when dataObject is Session session => new StripeWebhookData(
                session.CustomerId,
                session.SubscriptionId,
                null,
                null,
                null,
                null,
                false,
                null,
                null),

            "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted"
                when dataObject is Subscription sub => new StripeWebhookData(
                    sub.CustomerId,
                    sub.Id,
                    sub.Items.Data.FirstOrDefault()?.Price.Id,
                    sub.Status,
                    sub.CurrentPeriodStart,
                    sub.CurrentPeriodEnd,
                    sub.CancelAtPeriodEnd,
                    sub.CanceledAt,
                    sub.TrialEnd),

            "invoice.paid" or "invoice.payment_failed" when dataObject is Invoice invoice => new StripeWebhookData(
                invoice.CustomerId,
                invoice.SubscriptionId,
                null,
                invoice.Status,
                null,
                null,
                false,
                null,
                null),

            _ => new StripeWebhookData(null, null, null, null, null, null, false, null, null)
        };
    }

    private static StripeSubscriptionInfo MapToSubscriptionInfo(Subscription subscription)
    {
        var priceId = subscription.Items.Data.FirstOrDefault()?.Price.Id ?? string.Empty;

        return new StripeSubscriptionInfo(
            subscription.Id,
            subscription.CustomerId,
            subscription.Status,
            priceId,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            subscription.CanceledAt,
            subscription.TrialEnd);
    }
}
