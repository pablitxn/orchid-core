using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.ChangePlan;

public class ChangePlanHandler(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    IStripeService stripeService,
    ILogger<ChangePlanHandler> logger)
    : IRequestHandler<ChangePlanCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new ValidationException("Subscription is not linked to Stripe.", nameof(subscription.StripeSubscriptionId));

        if (!subscription.IsActive())
            throw new ValidationException("Cannot change plan on an inactive subscription.", nameof(subscription.Status));

        var newPlan = await planRepository.GetByIdAsync(request.NewPlanId, cancellationToken);
        if (newPlan is null)
            throw new SubscriptionPlanNotFoundException(request.NewPlanId);

        if (string.IsNullOrEmpty(newPlan.StripePriceId))
            throw new ValidationException($"Plan '{newPlan.Name}' is not configured for Stripe payments.", nameof(newPlan.StripePriceId));

        if (subscription.SubscriptionPlanId == request.NewPlanId)
            throw new ValidationException("Already subscribed to this plan.", nameof(request.NewPlanId));

        var idempotencyKey = $"change_plan_{subscription.StripeSubscriptionId}_{request.NewPlanId}_{DateTime.UtcNow:yyyyMMddHH}";

        var stripeInfo = await stripeService.ChangePlanAsync(
            subscription.StripeSubscriptionId,
            newPlan.StripePriceId,
            idempotencyKey,
            cancellationToken);

        // Update local subscription
        subscription.SubscriptionPlanId = request.NewPlanId;
        subscription.UpdateFromStripe(
            MapStripeStatus(stripeInfo.Status),
            stripeInfo.CurrentPeriodStart,
            stripeInfo.CurrentPeriodEnd,
            stripeInfo.CancelAtPeriodEnd,
            stripeInfo.CanceledAt);

        // Update credits based on new plan
        subscription.Credits = newPlan.Credits;

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        logger.LogInformation(
            "Changed subscription {SubscriptionId} for user {UserId} to plan {PlanId}",
            subscription.Id, request.UserId, request.NewPlanId);

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
