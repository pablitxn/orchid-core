using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.RetryWebhook;

public class RetryWebhookHandler(
    IStripeWebhookEventRepository webhookEventRepository,
    IStripeService stripeService,
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    ICreditPurchaseRepository creditPurchaseRepository,
    IEventPublisher eventPublisher,
    INotificationService notificationService,
    IAuditService auditService,
    ILogger<RetryWebhookHandler> logger)
    : IRequestHandler<RetryWebhookCommand, RetryWebhookDto>
{
    private const int MaxRetries = 5;

    public async Task<RetryWebhookDto> Handle(
        RetryWebhookCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Admin {AdminUserId} attempting to retry webhook {WebhookEventId}",
            request.AdminUserId, request.WebhookEventId);

        var webhookEvent = await webhookEventRepository.GetByIdAsync(
            request.WebhookEventId,
            cancellationToken);

        if (webhookEvent is null)
            throw new EntityNotFoundException($"Webhook event with ID {request.WebhookEventId} not found.");

        if (webhookEvent.Status != WebhookEventStatus.Failed &&
            webhookEvent.Status != WebhookEventStatus.DeadLetter)
        {
            throw new ValidationException(
                $"Cannot retry webhook with status {webhookEvent.Status}. Only Failed or DeadLetter events can be retried.",
                nameof(webhookEvent.Status));
        }

        // Parse the stored payload
        var parsedEvent = stripeService.ParseWebhookPayload(webhookEvent.Payload);
        if (parsedEvent is null)
        {
            throw new OperationFailedException(
                "Failed to parse stored webhook payload.");
        }

        // Mark as processing
        webhookEvent.StartRetryProcessing();
        await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);

        string? resultMessage = null;
        var success = false;

        try
        {
            await ProcessEventAsync(parsedEvent, cancellationToken);

            webhookEvent.MarkProcessed();
            success = true;
            resultMessage = "Webhook processed successfully on retry.";

            logger.LogInformation(
                "Admin {AdminUserId} successfully retried webhook {WebhookEventId}",
                request.AdminUserId, request.WebhookEventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to retry webhook {WebhookEventId}. Retry count: {RetryCount}",
                request.WebhookEventId, webhookEvent.RetryCount);

            webhookEvent.MarkFailed(ex.Message);
            webhookEvent.ScheduleRetry(MaxRetries);
            resultMessage = $"Retry failed: {ex.Message}";
        }

        await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);

        // Audit the retry attempt
        await auditService.LogSuccessAsync(
            AuditActionTypes.AdminWebhookRetry,
            nameof(StripeWebhookEventEntity),
            webhookEvent.Id.ToString(),
            oldValues: new { webhookEvent.Status, webhookEvent.RetryCount },
            newValues: new { Success = success, Message = resultMessage },
            metadata: new Dictionary<string, object>
            {
                ["AdminUserId"] = request.AdminUserId,
                ["StripeEventId"] = webhookEvent.StripeEventId,
                ["EventType"] = webhookEvent.EventType
            },
            cancellationToken);

        return new RetryWebhookDto
        {
            Id = webhookEvent.Id,
            StripeEventId = webhookEvent.StripeEventId,
            Success = success,
            Message = resultMessage,
            WebhookLog = WebhookLogDto.FromEntity(webhookEvent, MaxRetries)
        };
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

            case "payment_intent.succeeded":
                await HandlePaymentIntentSucceededAsync(data, cancellationToken);
                break;

            case "payment_intent.payment_failed":
                await HandlePaymentIntentFailedAsync(data, cancellationToken);
                break;

            default:
                logger.LogInformation("Unhandled webhook event type: {EventType}", webhookEvent.EventType);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (data.Mode == "payment" && data.Metadata?.TryGetValue("type", out var purchaseType) == true && purchaseType == "credit_purchase")
        {
            await HandleCreditPurchaseCompletedAsync(data, cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(data.CustomerId) || string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeCustomerIdAsync(data.CustomerId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("No subscription found for Stripe customer {CustomerId}", data.CustomerId);
            return;
        }

        subscription.LinkToStripe(data.CustomerId, data.SubscriptionId);

        var stripeInfo = await stripeService.GetSubscriptionAsync(data.SubscriptionId, cancellationToken);
        if (stripeInfo is not null)
        {
            subscription.UpdateFromStripe(
                MapStripeStatus(stripeInfo.Status),
                stripeInfo.CurrentPeriodStart,
                stripeInfo.CurrentPeriodEnd,
                stripeInfo.CancelAtPeriodEnd,
                stripeInfo.CanceledAt);

            var plan = await planRepository.GetByStripePriceIdAsync(stripeInfo.PriceId, cancellationToken);
            if (plan is not null)
            {
                subscription.SubscriptionPlanId = plan.Id;
                subscription.Credits = plan.Credits;
            }
        }

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await eventPublisher.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
    }

    private async Task HandleCreditPurchaseCompletedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (data.Metadata is null || !data.Metadata.TryGetValue("creditPurchaseId", out var purchaseIdStr) ||
            !Guid.TryParse(purchaseIdStr, out var purchaseId))
        {
            logger.LogWarning("Credit purchase webhook missing creditPurchaseId in metadata");
            return;
        }

        var purchase = await creditPurchaseRepository.GetByIdAsync(purchaseId, cancellationToken);
        if (purchase is null)
        {
            logger.LogWarning("Credit purchase {PurchaseId} not found", purchaseId);
            return;
        }

        if (purchase.Status != CreditPurchaseStatus.Pending)
        {
            logger.LogInformation("Credit purchase {PurchaseId} already processed with status {Status}", purchaseId, purchase.Status);
            return;
        }

        purchase.MarkCompleted(data.PaymentIntentId ?? string.Empty);
        await creditPurchaseRepository.UpdateAsync(purchase, cancellationToken);

        var subscription = await subscriptionRepository.GetByUserIdAsync(purchase.UserId, cancellationToken);
        if (subscription is not null)
        {
            subscription.AddCredits(purchase.CreditsGranted);
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

            await eventPublisher.PublishAsync(new CreditsAddedEvent(
                subscription.Id,
                purchase.UserId,
                purchase.CreditsGranted));
        }

        await eventPublisher.PublishAsync(new CreditsPurchasedEvent(
            purchase.Id,
            purchase.UserId,
            purchase.CreditPackageId,
            purchase.CreditsGranted,
            purchase.PricePaidCents));
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
    }

    private async Task HandleInvoicePaidAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
            return;

        if (subscription.SubscriptionPlan is not null)
        {
            subscription.Credits = subscription.SubscriptionPlan.Credits;
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }
    }

    private async Task HandleInvoicePaymentFailedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(data.SubscriptionId))
            return;

        var subscription = await subscriptionRepository.GetByStripeSubscriptionIdAsync(data.SubscriptionId, cancellationToken);
        if (subscription is null)
            return;

        subscription.Status = SubscriptionStatus.PastDue;
        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        await notificationService.SendSubscriptionExpiringAlertAsync(
            subscription.UserId,
            subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddDays(3),
            cancellationToken);
    }

    private async Task HandlePaymentIntentSucceededAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(data.CustomerId))
        {
            var subscription = await subscriptionRepository.GetByStripeCustomerIdAsync(data.CustomerId, cancellationToken);
            if (subscription is not null && subscription.Status == SubscriptionStatus.PastDue)
            {
                subscription.Status = SubscriptionStatus.Active;
                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
                await eventPublisher.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
            }
        }
    }

    private async Task HandlePaymentIntentFailedAsync(StripeWebhookData data, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(data.CustomerId))
        {
            var subscription = await subscriptionRepository.GetByStripeCustomerIdAsync(data.CustomerId, cancellationToken);
            if (subscription is not null && subscription.Status == SubscriptionStatus.Active)
            {
                subscription.Status = SubscriptionStatus.PastDue;
                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

                await notificationService.SendSubscriptionExpiringAlertAsync(
                    subscription.UserId,
                    subscription.CurrentPeriodEnd ?? DateTime.UtcNow.AddDays(3),
                    cancellationToken);
            }
        }
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
