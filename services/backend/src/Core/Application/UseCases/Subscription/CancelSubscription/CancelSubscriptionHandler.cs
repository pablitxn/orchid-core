using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.CancelSubscription;

public class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IStripeService stripeService,
    ILogger<CancelSubscriptionHandler> logger)
    : IRequestHandler<CancelSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new ValidationException("Subscription is not linked to Stripe.", nameof(subscription.StripeSubscriptionId));

        if (!subscription.IsActive())
            throw new ValidationException("Cannot cancel an inactive subscription.", nameof(subscription.Status));

        if (subscription.CancelAtPeriodEnd)
            throw new ValidationException("Subscription is already scheduled for cancellation.", nameof(subscription.CancelAtPeriodEnd));

        var idempotencyKey = $"cancel_{subscription.StripeSubscriptionId}_{DateTime.UtcNow:yyyyMMddHH}";

        var stripeInfo = await stripeService.CancelSubscriptionAsync(
            subscription.StripeSubscriptionId,
            idempotencyKey,
            cancellationToken);

        // Update local subscription
        subscription.UpdateFromStripe(
            MapStripeStatus(stripeInfo.Status),
            stripeInfo.CurrentPeriodStart,
            stripeInfo.CurrentPeriodEnd,
            stripeInfo.CancelAtPeriodEnd,
            stripeInfo.CanceledAt);

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        logger.LogInformation(
            "Canceled subscription {SubscriptionId} for user {UserId} at period end {PeriodEnd}",
            subscription.Id, request.UserId, subscription.CurrentPeriodEnd);

        // Reload with plan details
        subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return SubscriptionDto.FromEntity(subscription!);
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
