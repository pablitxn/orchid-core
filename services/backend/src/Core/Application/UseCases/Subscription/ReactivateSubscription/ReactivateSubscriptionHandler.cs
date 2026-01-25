using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.ReactivateSubscription;

public class ReactivateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IStripeService stripeService,
    ILogger<ReactivateSubscriptionHandler> logger)
    : IRequestHandler<ReactivateSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(ReactivateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new ValidationException("Subscription is not linked to Stripe.", nameof(subscription.StripeSubscriptionId));

        if (!subscription.CanReactivate())
            throw new ValidationException("Subscription cannot be reactivated. It must be scheduled for cancellation and the period must not have ended.", nameof(subscription.CancelAtPeriodEnd));

        var idempotencyKey = $"reactivate_{subscription.StripeSubscriptionId}_{DateTime.UtcNow:yyyyMMddHH}";

        var stripeInfo = await stripeService.ReactivateSubscriptionAsync(
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
            "Reactivated subscription {SubscriptionId} for user {UserId}",
            subscription.Id, request.UserId);

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
