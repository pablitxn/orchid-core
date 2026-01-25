using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.ProcessWebhook;

public class ProcessStripeWebhookHandler(
    IStripeService stripeService,
    IStripeWebhookEventRepository webhookEventRepository,
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    IEventPublisher eventPublisher,
    INotificationService notificationService,
    ILogger<ProcessStripeWebhookHandler> logger)
    : IRequestHandler<ProcessStripeWebhookCommand, WebhookProcessingResult>
{
    public async Task<WebhookProcessingResult> Handle(ProcessStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        // Validate and parse the webhook
        var webhookEvent = stripeService.ValidateAndParseWebhook(request.Payload, request.Signature);
        if (webhookEvent is null)
        {
            logger.LogWarning("Invalid webhook signature");
            return new WebhookProcessingResult(false, Message: "Invalid signature");
        }

        // Check for duplicate event (idempotency)
        if (await webhookEventRepository.ExistsAsync(webhookEvent.EventId, cancellationToken))
        {
            logger.LogInformation("Webhook event {EventId} already processed (duplicate)", webhookEvent.EventId);
            return new WebhookProcessingResult(true, webhookEvent.EventId, "Duplicate event");
        }

        // Create webhook event record
        var eventRecord = new StripeWebhookEventEntity
        {
            Id = Guid.NewGuid(),
            StripeEventId = webhookEvent.EventId,
            EventType = webhookEvent.EventType,
            Payload = request.Payload,
            Status = WebhookEventStatus.Processing
        };

        await webhookEventRepository.CreateAsync(eventRecord, cancellationToken);

        try
        {
            await ProcessEventAsync(webhookEvent, cancellationToken);

            eventRecord.MarkProcessed();
            await webhookEventRepository.UpdateAsync(eventRecord, cancellationToken);

            logger.LogInformation("Successfully processed webhook event {EventId} of type {EventType}",
                webhookEvent.EventId, webhookEvent.EventType);

            return new WebhookProcessingResult(true, webhookEvent.EventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process webhook event {EventId} of type {EventType}",
                webhookEvent.EventId, webhookEvent.EventType);

            eventRecord.MarkFailed(ex.Message);
            await webhookEventRepository.UpdateAsync(eventRecord, cancellationToken);

            return new WebhookProcessingResult(false, webhookEvent.EventId, ex.Message);
        }
    }

    private async Task ProcessEventAsync(StripeWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        var data = webhookEvent.Data;

        switch (webhookEvent.EventType)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(data, cancellationToken);
                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(data, cancellationToken);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(data, cancellationToken);
                break;

            case "invoice.paid":
                await HandleInvoicePaidAsync(data, cancellationToken);
                break;

            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(data, cancellationToken);
                break;

            default:
                logger.LogInformation("Unhandled webhook event type: {EventType}", webhookEvent.EventType);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.CustomerId) || string.IsNullOrEmpty(data.SubscriptionId))
            return;

        // Find subscription by Stripe customer ID
        var subscription = await subscriptionRepository.GetByStripeCustomerIdAsync(data.CustomerId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("No subscription found for Stripe customer {CustomerId}", data.CustomerId);
            return;
        }

        // Link subscription to Stripe
        subscription.LinkToStripe(data.CustomerId, data.SubscriptionId);

        // Get subscription details from Stripe
        var stripeInfo = await stripeService.GetSubscriptionAsync(data.SubscriptionId, cancellationToken);
        if (stripeInfo is not null)
        {
            subscription.UpdateFromStripe(
                MapStripeStatus(stripeInfo.Status),
                stripeInfo.CurrentPeriodStart,
                stripeInfo.CurrentPeriodEnd,
                stripeInfo.CancelAtPeriodEnd,
                stripeInfo.CanceledAt);

            // Update plan if we can match the price ID
            var plan = await planRepository.GetByStripePriceIdAsync(stripeInfo.PriceId, cancellationToken);
            if (plan is not null)
            {
                subscription.SubscriptionPlanId = plan.Id;
                subscription.Credits = plan.Credits;
            }
        }

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await eventPublisher.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));

        logger.LogInformation("Checkout completed for subscription {SubscriptionId}, linked to Stripe subscription {StripeSubscriptionId}",
            subscription.Id, data.SubscriptionId);
    }

    private async Task HandleSubscriptionUpdatedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("No subscription found for Stripe subscription {StripeSubscriptionId}", data.SubscriptionId);
            return;
        }

        subscription.UpdateFromStripe(
            MapStripeStatus(data.Status ?? "incomplete"),
            data.CurrentPeriodStart,
            data.CurrentPeriodEnd,
            data.CancelAtPeriodEnd,
            data.CanceledAt);

        // Update plan if price changed
        if (!string.IsNullOrEmpty(data.PriceId))
        {
            var plan = await planRepository.GetByStripePriceIdAsync(data.PriceId, cancellationToken);
            if (plan is not null && subscription.SubscriptionPlanId != plan.Id)
            {
                subscription.SubscriptionPlanId = plan.Id;
                subscription.Credits = plan.Credits;
            }
        }

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await eventPublisher.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
    }

    private async Task HandleSubscriptionDeletedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
            return;

        subscription.UpdateFromStripe(
            SubscriptionStatus.Canceled,
            data.CurrentPeriodStart,
            data.CurrentPeriodEnd,
            true,
            data.CanceledAt ?? DateTime.UtcNow);

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await eventPublisher.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));

        await notificationService.SendSubscriptionExpiredAlertAsync(subscription.UserId, cancellationToken);

        logger.LogInformation("Subscription {SubscriptionId} deleted (canceled)", subscription.Id);
    }

    private async Task HandleInvoicePaidAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
            return;

        // Refresh credits on successful payment (for recurring subscriptions)
        if (subscription.SubscriptionPlan is not null)
        {
            subscription.Credits = subscription.SubscriptionPlan.Credits;
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }

        logger.LogInformation("Invoice paid for subscription {SubscriptionId}", subscription.Id);
    }

    private async Task HandleInvoicePaymentFailedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
            return;

        // Update status to past_due
        subscription.Status = SubscriptionStatus.PastDue;
        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        // Notify user of payment failure
        await notificationService.SendSubscriptionExpiringAlertAsync(
            subscription.UserId,
            subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddDays(3),
            cancellationToken);

        logger.LogWarning("Payment failed for subscription {SubscriptionId}", subscription.Id);
    }

    private static SubscriptionStatus MapStripeStatus(string status) => status switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" => SubscriptionStatus.Canceled,
        "unpaid" => SubscriptionStatus.Unpaid,
        "paused" => SubscriptionStatus.Paused,
        _ => SubscriptionStatus.Incomplete
    };
}
